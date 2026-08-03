# Persistent Database Execution Tracking Guide

While `EtlAnalytics.RulesEngine` defaults to `InMemoryBundleExecutionTracker` for fast, lightweight in-memory progress tracking, production applications often require **persistent database execution history**.

Persisting execution logs to a database provides:
- **Long-Term Audit History**: Retain complete records of every bundle run, step status, execution duration, and error traceback.
- **Server Restart Resilience**: Progress state survives application pool recycles, container restarts, or deployments.
- **Distributed Scale-Out**: Multiple API instances or worker nodes in a load-balanced cluster can share execution states.

For end-to-end security auditing, capture actor metadata from the consuming application (for example: `ExecutedBy`, `ExecutedByName`, `AuthMethod`, and a policy decision correlation id).

See `RBAC.md` and `RBAC_SCHEMA_DRAFT.md` for the recommended app-side authorization model.

---

## 1. Database Schemas

Create the following tracking tables in your database to store bundle, sequence, and rule-level execution histories.

### SQL Server Schema

```sql
-- Bundle-level execution run table
CREATE TABLE dbo.BundleExecutionLogs (
    ExecutionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    BundleId INT NOT NULL,
    BundleName NVARCHAR(255) NOT NULL,
    ExecutedBy NVARCHAR(255) NULL,
    ExecutedByName NVARCHAR(255) NULL,
    ActorType NVARCHAR(50) NULL,
    AuthMethod NVARCHAR(50) NULL,
    DecisionCorrelationId UNIQUEIDENTIFIER NULL,
    Categories NVARCHAR(MAX) NULL,
    Tags NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) NOT NULL, -- Pending, Starting, Completed, Failed, Skipped
    StartTime DATETIME2 NULL,
    EndTime DATETIME2 NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    Logs NVARCHAR(MAX) NULL, -- JSON array of log lines
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Sequence group level execution log table
CREATE TABLE dbo.SequenceExecutionLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ExecutionId UNIQUEIDENTIFIER NOT NULL,
    SequenceOrder INT NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    StartTime DATETIME2 NULL,
    EndTime DATETIME2 NULL,
    Message NVARCHAR(MAX) NULL,
    CONSTRAINT FK_SequenceExecutionLogs_Bundle FOREIGN KEY (ExecutionId) REFERENCES dbo.BundleExecutionLogs(ExecutionId) ON DELETE CASCADE
);

-- Individual rule level execution log table (includes parallel rules)
CREATE TABLE dbo.RuleExecutionLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ExecutionId UNIQUEIDENTIFIER NOT NULL,
    SequenceOrder INT NOT NULL,
    RuleId INT NOT NULL,
    RuleName NVARCHAR(255) NOT NULL,
    RuleType NVARCHAR(50) NOT NULL,
    Categories NVARCHAR(MAX) NULL,
    Tags NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) NOT NULL,
    StartTime DATETIME2 NULL,
    EndTime DATETIME2 NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    ResultJson NVARCHAR(MAX) NULL,
    CONSTRAINT FK_RuleExecutionLogs_Bundle FOREIGN KEY (ExecutionId) REFERENCES dbo.BundleExecutionLogs(ExecutionId) ON DELETE CASCADE
);

CREATE INDEX IX_SequenceExecutionLogs_ExecId ON dbo.SequenceExecutionLogs(ExecutionId);
CREATE INDEX IX_RuleExecutionLogs_ExecId ON dbo.RuleExecutionLogs(ExecutionId);
```

### PostgreSQL Schema

```sql
CREATE TABLE BundleExecutionLogs (
    ExecutionId UUID PRIMARY KEY,
    BundleId INT NOT NULL,
    BundleName VARCHAR(255) NOT NULL,
    ExecutedBy VARCHAR(255) NULL,
    ExecutedByName VARCHAR(255) NULL,
    ActorType VARCHAR(50) NULL,
    AuthMethod VARCHAR(50) NULL,
    DecisionCorrelationId UUID NULL,
    Status VARCHAR(50) NOT NULL,
    StartTime TIMESTAMP NULL,
    EndTime TIMESTAMP NULL,
    ErrorMessage TEXT NULL,
    Logs TEXT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE SequenceExecutionLogs (
    Id SERIAL PRIMARY KEY,
    ExecutionId UUID NOT NULL REFERENCES BundleExecutionLogs(ExecutionId) ON DELETE CASCADE,
    SequenceOrder INT NOT NULL,
    Status VARCHAR(50) NOT NULL,
    StartTime TIMESTAMP NULL,
    EndTime TIMESTAMP NULL,
    Message TEXT NULL
);

CREATE TABLE RuleExecutionLogs (
    Id SERIAL PRIMARY KEY,
    ExecutionId UUID NOT NULL REFERENCES BundleExecutionLogs(ExecutionId) ON DELETE CASCADE,
    SequenceOrder INT NOT NULL,
    RuleId INT NOT NULL,
    RuleName VARCHAR(255) NOT NULL,
    RuleType VARCHAR(50) NOT NULL,
    Status VARCHAR(50) NOT NULL,
    StartTime TIMESTAMP NULL,
    EndTime TIMESTAMP NULL,
    ErrorMessage TEXT NULL,
    ResultJson TEXT NULL
);

CREATE INDEX IX_SequenceExecutionLogs_ExecId ON SequenceExecutionLogs(ExecutionId);
CREATE INDEX IX_RuleExecutionLogs_ExecId ON RuleExecutionLogs(ExecutionId);
```

### MySQL Schema

```sql
CREATE TABLE BundleExecutionLogs (
    ExecutionId CHAR(36) PRIMARY KEY,
    BundleId INT NOT NULL,
    BundleName VARCHAR(255) NOT NULL,
    ExecutedBy VARCHAR(255) NULL,
    ExecutedByName VARCHAR(255) NULL,
    ActorType VARCHAR(50) NULL,
    AuthMethod VARCHAR(50) NULL,
    DecisionCorrelationId CHAR(36) NULL,
    Status VARCHAR(50) NOT NULL,
    StartTime DATETIME NULL,
    EndTime DATETIME NULL,
    ErrorMessage TEXT NULL,
    Logs TEXT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE SequenceExecutionLogs (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ExecutionId CHAR(36) NOT NULL,
    SequenceOrder INT NOT NULL,
    Status VARCHAR(50) NOT NULL,
    StartTime DATETIME NULL,
    EndTime DATETIME NULL,
    Message TEXT NULL,
    FOREIGN KEY (ExecutionId) REFERENCES BundleExecutionLogs(ExecutionId) ON DELETE CASCADE
);

CREATE TABLE RuleExecutionLogs (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ExecutionId CHAR(36) NOT NULL,
    SequenceOrder INT NOT NULL,
    RuleId INT NOT NULL,
    RuleName VARCHAR(255) NOT NULL,
    RuleType VARCHAR(50) NOT NULL,
    Status VARCHAR(50) NOT NULL,
    StartTime DATETIME NULL,
    EndTime DATETIME NULL,
    ErrorMessage TEXT NULL,
    ResultJson TEXT NULL,
    FOREIGN KEY (ExecutionId) REFERENCES BundleExecutionLogs(ExecutionId) ON DELETE CASCADE
);
```

---

## 2. Implementing `SqlBundleExecutionTracker`

Implement `IBundleExecutionTracker` using Dapper or your preferred database access library:

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Models;

namespace MyProject.Services;

public class SqlBundleExecutionTracker : IBundleExecutionTracker
{
    private readonly IRuleDbProvider _dbProvider;
    private readonly string _connectionString;

    public event EventHandler<BundleProgressEventArgs>? OnStatusChanged;

    public SqlBundleExecutionTracker(IRuleDbProvider dbProvider, string connectionString)
    {
        _dbProvider = dbProvider;
        _connectionString = connectionString;
    }

    public async Task<BundleExecutionState> CreateExecutionAsync(BusinessRuleBundle bundle, Guid? executionId = null)
    {
        return await CreateExecutionAsync(bundle, executionId, actorContext: null);
    }

    public async Task<BundleExecutionState> CreateExecutionAsync(BusinessRuleBundle bundle, Guid? executionId, ExecutionActorContext? actorContext)
    {
        Guid id = executionId ?? Guid.NewGuid();

        var state = new BundleExecutionState
        {
            ExecutionId = id,
            BundleId = bundle.Id,
            BundleName = bundle.Name,
            ExecutedBy = actorContext?.ActorId,
            ExecutedByName = actorContext?.ActorName,
            ActorType = actorContext?.ActorType,
            AuthMethod = actorContext?.AuthMethod,
            DecisionCorrelationId = actorContext?.DecisionCorrelationId,
            Status = ExecutionStatus.Pending
        };

        using var db = _dbProvider.CreateConnection(_connectionString);
        if (db.State != ConnectionState.Open) db.Open();
        using var tx = db.BeginTransaction();

        await db.ExecuteAsync(@"
            INSERT INTO dbo.BundleExecutionLogs
            (ExecutionId, BundleId, BundleName, ExecutedBy, ExecutedByName, ActorType, AuthMethod, DecisionCorrelationId, Status, Logs)
            VALUES
            (@ExecutionId, @BundleId, @BundleName, @ExecutedBy, @ExecutedByName, @ActorType, @AuthMethod, @DecisionCorrelationId, @Status, '[]')",
            new
            {
                ExecutionId = id,
                bundle.Id,
                BundleName = bundle.Name,
                ExecutedBy = actorContext?.ActorId,
                ExecutedByName = actorContext?.ActorName,
                ActorType = actorContext?.ActorType,
                AuthMethod = actorContext?.AuthMethod,
                DecisionCorrelationId = actorContext?.DecisionCorrelationId,
                Status = ExecutionStatus.Pending.ToString()
            }, tx);

        var groupedItems = bundle.Items.GroupBy(i => i.SequenceOrder).OrderBy(g => g.Key);

        foreach (var group in groupedItems)
        {
            var seqState = new SequenceExecutionState
            {
                SequenceOrder = group.Key,
                Status = ExecutionStatus.Pending,
                Message = $"Step sequence {group.Key} pending execution"
            };

            await db.ExecuteAsync(@"
                INSERT INTO dbo.SequenceExecutionLogs (ExecutionId, SequenceOrder, Status, Message)
                VALUES (@ExecutionId, @SequenceOrder, @Status, @Message)",
                new { ExecutionId = id, SequenceOrder = group.Key, Status = ExecutionStatus.Pending.ToString(), seqState.Message }, tx);

            foreach (var item in group)
            {
                var ruleName = !string.IsNullOrWhiteSpace(item.RuleName) ? item.RuleName : $"Rule #{item.RuleId}";
                var ruleType = item.RuleType ?? "Unknown";

                seqState.Rules.Add(new RuleExecutionState
                {
                    RuleId = item.RuleId,
                    RuleName = ruleName,
                    RuleType = ruleType,
                    SequenceOrder = group.Key,
                    Status = ExecutionStatus.Pending
                });

                await db.ExecuteAsync(@"
                    INSERT INTO dbo.RuleExecutionLogs (ExecutionId, SequenceOrder, RuleId, RuleName, RuleType, Status)
                    VALUES (@ExecutionId, @SequenceOrder, @RuleId, @RuleName, @RuleType, @Status)",
                    new { ExecutionId = id, SequenceOrder = group.Key, item.RuleId, RuleName = ruleName, RuleType = ruleType, Status = ExecutionStatus.Pending.ToString() }, tx);
            }

            state.Sequences.Add(seqState);
        }

        tx.Commit();

        RaiseStatusChanged(new BundleProgressEventArgs(id, bundle.Name, ExecutionStatus.Pending, "Bundle initialized in database", state));
        return state;
    }

    public async Task UpdateBundleStatusAsync(Guid executionId, ExecutionStatus status, string? message = null)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        await db.ExecuteAsync(@"
            UPDATE dbo.BundleExecutionLogs 
            SET Status = @Status, StartTime = COALESCE(StartTime, SYSUTCDATETIME())
            WHERE ExecutionId = @ExecutionId",
            new { ExecutionId = executionId, Status = status.ToString() });

        var state = await GetExecutionAsync(executionId);
        if (state != null)
        {
            RaiseStatusChanged(new BundleProgressEventArgs(executionId, state.BundleName, status, message ?? $"Bundle status updated to {status}", state));
        }
    }

    public async Task UpdateSequenceStatusAsync(Guid executionId, int sequenceOrder, ExecutionStatus status, string? message = null)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        await db.ExecuteAsync(@"
            UPDATE dbo.SequenceExecutionLogs 
            SET Status = @Status, 
                Message = COALESCE(@Message, Message),
                StartTime = CASE WHEN @Status = 'Starting' AND StartTime IS NULL THEN SYSUTCDATETIME() ELSE StartTime END,
                EndTime = CASE WHEN @Status IN ('Completed', 'Failed', 'Skipped') THEN SYSUTCDATETIME() ELSE EndTime END
            WHERE ExecutionId = @ExecutionId AND SequenceOrder = @SequenceOrder",
            new { ExecutionId = executionId, SequenceOrder = sequenceOrder, Status = status.ToString(), Message = message });

        var state = await GetExecutionAsync(executionId);
        if (state != null)
        {
            RaiseStatusChanged(new BundleProgressEventArgs(executionId, state.BundleName, status, message ?? $"Sequence #{sequenceOrder} updated to {status}", state, sequenceOrder: sequenceOrder));
        }
    }

    public async Task UpdateRuleStatusAsync(Guid executionId, int ruleId, int sequenceOrder, ExecutionStatus status, object? result = null, Exception? error = null)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        string? resultJson = result != null ? JsonSerializer.Serialize(result) : null;
        string? errorMsg = error?.Message;

        await db.ExecuteAsync(@"
            UPDATE dbo.RuleExecutionLogs 
            SET Status = @Status,
                ResultJson = COALESCE(@ResultJson, ResultJson),
                ErrorMessage = COALESCE(@ErrorMessage, ErrorMessage),
                StartTime = CASE WHEN @Status = 'Starting' AND StartTime IS NULL THEN SYSUTCDATETIME() ELSE StartTime END,
                EndTime = CASE WHEN @Status IN ('Completed', 'Failed', 'Skipped') THEN SYSUTCDATETIME() ELSE EndTime END
            WHERE ExecutionId = @ExecutionId AND SequenceOrder = @SequenceOrder AND RuleId = @RuleId",
            new { ExecutionId = executionId, SequenceOrder = sequenceOrder, RuleId = ruleId, Status = status.ToString(), ResultJson = resultJson, ErrorMessage = errorMsg });

        var state = await GetExecutionAsync(executionId);
        if (state != null)
        {
            RaiseStatusChanged(new BundleProgressEventArgs(executionId, state.BundleName, status, $"Rule #{ruleId} updated to {status}", state, sequenceOrder: sequenceOrder, ruleId: ruleId));
        }
    }

    public async Task CompleteExecutionAsync(Guid executionId, ExecutionStatus status, object? finalResult = null, Exception? error = null)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        await db.ExecuteAsync(@"
            UPDATE dbo.BundleExecutionLogs 
            SET Status = @Status, EndTime = SYSUTCDATETIME(), ErrorMessage = @ErrorMessage
            WHERE ExecutionId = @ExecutionId",
            new { ExecutionId = executionId, Status = status.ToString(), ErrorMessage = error?.Message });

        var state = await GetExecutionAsync(executionId);
        if (state != null)
        {
            RaiseStatusChanged(new BundleProgressEventArgs(executionId, state.BundleName, status, $"Bundle execution finished with status {status}", state));
        }
    }

    public async Task<BundleExecutionState?> GetExecutionAsync(Guid executionId)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        
        var bundleLog = await db.QueryFirstOrDefaultAsync(@"
            SELECT ExecutionId, BundleId, BundleName, ExecutedBy, ExecutedByName, ActorType, AuthMethod, DecisionCorrelationId, Status, StartTime, EndTime, ErrorMessage, Logs
            FROM dbo.BundleExecutionLogs WHERE ExecutionId = @ExecutionId", new { ExecutionId = executionId });

        if (bundleLog == null) return null;

        var state = new BundleExecutionState
        {
            ExecutionId = bundleLog.ExecutionId,
            BundleId = bundleLog.BundleId,
            BundleName = bundleLog.BundleName,
            ExecutedBy = bundleLog.ExecutedBy,
            ExecutedByName = bundleLog.ExecutedByName,
            ActorType = bundleLog.ActorType,
            AuthMethod = bundleLog.AuthMethod,
            DecisionCorrelationId = bundleLog.DecisionCorrelationId,
            Status = Enum.Parse<ExecutionStatus>((string)bundleLog.Status),
            StartTime = bundleLog.StartTime,
            EndTime = bundleLog.EndTime,
            ErrorMessage = bundleLog.ErrorMessage
        };

        var sequences = await db.QueryAsync(@"
            SELECT SequenceOrder, Status, StartTime, EndTime, Message
            FROM dbo.SequenceExecutionLogs WHERE ExecutionId = @ExecutionId ORDER BY SequenceOrder", new { ExecutionId = executionId });

        var rules = await db.QueryAsync(@"
            SELECT SequenceOrder, RuleId, RuleName, RuleType, Status, StartTime, EndTime, ErrorMessage, ResultJson
            FROM dbo.RuleExecutionLogs WHERE ExecutionId = @ExecutionId", new { ExecutionId = executionId });

        var ruleList = rules.ToList();

        foreach (var seq in sequences)
        {
            int seqOrder = seq.SequenceOrder;
            var seqState = new SequenceExecutionState
            {
                SequenceOrder = seqOrder,
                Status = Enum.Parse<ExecutionStatus>((string)seq.Status),
                StartTime = seq.StartTime,
                EndTime = seq.EndTime,
                Message = seq.Message,
                Rules = ruleList.Where(r => (int)r.SequenceOrder == seqOrder).Select(r => new RuleExecutionState
                {
                    RuleId = r.RuleId,
                    RuleName = r.RuleName,
                    RuleType = r.RuleType,
                    SequenceOrder = seqOrder,
                    Status = Enum.Parse<ExecutionStatus>((string)r.Status),
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    ErrorMessage = r.ErrorMessage,
                    Result = r.ResultJson != null ? JsonSerializer.Deserialize<object>((string)r.ResultJson) : null
                }).ToList()
            };

            state.Sequences.Add(seqState);
        }

        return state;
    }

    public async Task AppendLogAsync(Guid executionId, string logMessage)
    {
        // Optional: Appends plain text log line to bundle execution log array
    }

    private void RaiseStatusChanged(BundleProgressEventArgs args)
    {
        OnStatusChanged?.Invoke(this, args);
    }
}
```

---

## 3. Registering `SqlBundleExecutionTracker` in DI

Replace the default in-memory tracker with your custom persistent SQL tracker in `Program.cs`:

```csharp
// Register custom persistent SQL execution tracker
builder.Services.AddScoped<IBundleExecutionTracker>(sp =>
{
    var dbProvider = sp.GetRequiredService<IRuleDbProvider>();
    var config = sp.GetRequiredService<IConfiguration>();
    var connString = config.GetConnectionString("DefaultConnection");
    return new SqlBundleExecutionTracker(dbProvider, connString);
});
```

---

## 4. Migration Appendix: Add Actor Metadata Columns to Existing Tables

If you already have `BundleExecutionLogs` tables in production, apply one of the following idempotent migration scripts.

### SQL Server (Idempotent)

```sql
IF OBJECT_ID('dbo.BundleExecutionLogs', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.BundleExecutionLogs', 'ExecutedBy') IS NULL
        ALTER TABLE dbo.BundleExecutionLogs ADD ExecutedBy NVARCHAR(255) NULL;

    IF COL_LENGTH('dbo.BundleExecutionLogs', 'ExecutedByName') IS NULL
        ALTER TABLE dbo.BundleExecutionLogs ADD ExecutedByName NVARCHAR(255) NULL;

    IF COL_LENGTH('dbo.BundleExecutionLogs', 'ActorType') IS NULL
        ALTER TABLE dbo.BundleExecutionLogs ADD ActorType NVARCHAR(50) NULL;

    IF COL_LENGTH('dbo.BundleExecutionLogs', 'AuthMethod') IS NULL
        ALTER TABLE dbo.BundleExecutionLogs ADD AuthMethod NVARCHAR(50) NULL;

    IF COL_LENGTH('dbo.BundleExecutionLogs', 'DecisionCorrelationId') IS NULL
        ALTER TABLE dbo.BundleExecutionLogs ADD DecisionCorrelationId UNIQUEIDENTIFIER NULL;
END;
```

### PostgreSQL (Idempotent)

```sql
ALTER TABLE IF EXISTS BundleExecutionLogs ADD COLUMN IF NOT EXISTS ExecutedBy VARCHAR(255) NULL;
ALTER TABLE IF EXISTS BundleExecutionLogs ADD COLUMN IF NOT EXISTS ExecutedByName VARCHAR(255) NULL;
ALTER TABLE IF EXISTS BundleExecutionLogs ADD COLUMN IF NOT EXISTS ActorType VARCHAR(50) NULL;
ALTER TABLE IF EXISTS BundleExecutionLogs ADD COLUMN IF NOT EXISTS AuthMethod VARCHAR(50) NULL;
ALTER TABLE IF EXISTS BundleExecutionLogs ADD COLUMN IF NOT EXISTS DecisionCorrelationId UUID NULL;
```

### MySQL (Idempotent)

```sql
DROP PROCEDURE IF EXISTS UpgradeBundleExecutionActorColumns;
DELIMITER //
CREATE PROCEDURE UpgradeBundleExecutionActorColumns()
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_name = 'BundleExecutionLogs'
    ) THEN
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = DATABASE() AND table_name = 'BundleExecutionLogs' AND column_name = 'ExecutedBy'
        ) THEN
            ALTER TABLE BundleExecutionLogs ADD COLUMN ExecutedBy VARCHAR(255) NULL;
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = DATABASE() AND table_name = 'BundleExecutionLogs' AND column_name = 'ExecutedByName'
        ) THEN
            ALTER TABLE BundleExecutionLogs ADD COLUMN ExecutedByName VARCHAR(255) NULL;
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = DATABASE() AND table_name = 'BundleExecutionLogs' AND column_name = 'ActorType'
        ) THEN
            ALTER TABLE BundleExecutionLogs ADD COLUMN ActorType VARCHAR(50) NULL;
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = DATABASE() AND table_name = 'BundleExecutionLogs' AND column_name = 'AuthMethod'
        ) THEN
            ALTER TABLE BundleExecutionLogs ADD COLUMN AuthMethod VARCHAR(50) NULL;
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = DATABASE() AND table_name = 'BundleExecutionLogs' AND column_name = 'DecisionCorrelationId'
        ) THEN
            ALTER TABLE BundleExecutionLogs ADD COLUMN DecisionCorrelationId CHAR(36) NULL;
        END IF;
    END IF;
END //
DELIMITER ;

CALL UpgradeBundleExecutionActorColumns();
DROP PROCEDURE IF EXISTS UpgradeBundleExecutionActorColumns;
```

---

## 5. Log Retention & Automated Cleanup Strategy

To prevent tracking tables from growing indefinitely, set up a recurring SQL job or background service to purge execution logs older than a target retention window (e.g. 30 days):

### SQL Server Retention Cleanup Query

```sql
-- Purge execution logs older than 30 days
DELETE FROM dbo.BundleExecutionLogs 
WHERE CreatedAt < DATEADD(DAY, -30, SYSUTCDATETIME());
```

Because foreign keys on `SequenceExecutionLogs` and `RuleExecutionLogs` specify `ON DELETE CASCADE`, deleting old bundle records automatically cleans up all associated sequence and rule detail logs!
