# Database Schema Upgrade Guide (v2.2.0 to v2.3.0) - EtlAnalytics.RulesEngine

This guide provides idempotent database migration scripts and instructions to upgrade existing `EtlAnalytics.RulesEngine` databases to version **v2.3.0** (adding support for multiple **Categories** and **Tags** across Rules, Bundles, Connections, and Execution Tracker logs).

Note: this document focuses on the v2.3.0 categories and tags upgrade. For application-side authorization schema planning (RBAC/group/ACL and policy decision audit tables), see `RBAC_SCHEMA_DRAFT.md`.

---

## 1. Overview of Schema Changes in v2.3.0

The v2.3.0 update introduces `Categories` and `Tags` columns stored as JSON array strings (`NVARCHAR(MAX)` or `TEXT`).

### Added Columns:
- `dbo.DbConnections`: Added `Categories` and `Tags`.
- `dbo.BusinessRules`: Added `Categories` and `Tags`.
- `dbo.BusinessRuleHistory`: Added `Categories` and `Tags`.
- `dbo.BusinessRuleBundles`: Added `Categories` and `Tags`.
- `dbo.BundleExecutionLogs` *(Persistent Tracking)*: Added `Categories` and `Tags`.
- `dbo.RuleExecutionLogs` *(Persistent Tracking)*: Added `Categories` and `Tags`.

All new columns are nullable (`NULL`), ensuring **100% non-breaking backward compatibility** for existing records.

---

## 2. Idempotent Migration Scripts

Choose the script corresponding to your database provider. These scripts check for column existence before adding them, so they can safely be executed multiple times.

### SQL Server (T-SQL) Migration Script

```sql
-- 1. Upgrade DbConnections
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DbConnections') AND name = 'Categories')
BEGIN
    ALTER TABLE dbo.DbConnections ADD Categories NVARCHAR(MAX) NULL, Tags NVARCHAR(MAX) NULL;
    PRINT 'Added Categories and Tags to dbo.DbConnections';
END;

-- 2. Upgrade BusinessRules
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BusinessRules') AND name = 'Categories')
BEGIN
    ALTER TABLE dbo.BusinessRules ADD Categories NVARCHAR(MAX) NULL, Tags NVARCHAR(MAX) NULL;
    PRINT 'Added Categories and Tags to dbo.BusinessRules';
END;

-- 3. Upgrade BusinessRuleHistory
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BusinessRuleHistory') AND name = 'Categories')
BEGIN
    ALTER TABLE dbo.BusinessRuleHistory ADD Categories NVARCHAR(MAX) NULL, Tags NVARCHAR(MAX) NULL;
    PRINT 'Added Categories and Tags to dbo.BusinessRuleHistory';
END;

-- 4. Upgrade BusinessRuleBundles
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BusinessRuleBundles') AND name = 'Categories')
BEGIN
    ALTER TABLE dbo.BusinessRuleBundles ADD Categories NVARCHAR(MAX) NULL, Tags NVARCHAR(MAX) NULL;
    PRINT 'Added Categories and Tags to dbo.BusinessRuleBundles';
END;

-- 5. Upgrade Persistent Execution Tracker Logs (If Used)
IF OBJECT_ID('dbo.BundleExecutionLogs', 'U') IS NOT NULL AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BundleExecutionLogs') AND name = 'Categories')
BEGIN
    ALTER TABLE dbo.BundleExecutionLogs ADD Categories NVARCHAR(MAX) NULL, Tags NVARCHAR(MAX) NULL;
    PRINT 'Added Categories and Tags to dbo.BundleExecutionLogs';
END;

IF OBJECT_ID('dbo.RuleExecutionLogs', 'U') IS NOT NULL AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RuleExecutionLogs') AND name = 'Categories')
BEGIN
    ALTER TABLE dbo.RuleExecutionLogs ADD Categories NVARCHAR(MAX) NULL, Tags NVARCHAR(MAX) NULL;
    PRINT 'Added Categories and Tags to dbo.RuleExecutionLogs';
END;
```

---

### PostgreSQL Migration Script

```sql
-- 1. Upgrade DbConnections
ALTER TABLE DbConnections ADD COLUMN IF NOT EXISTS Categories TEXT NULL;
ALTER TABLE DbConnections ADD COLUMN IF NOT EXISTS Tags TEXT NULL;

-- 2. Upgrade BusinessRules
ALTER TABLE BusinessRules ADD COLUMN IF NOT EXISTS Categories TEXT NULL;
ALTER TABLE BusinessRules ADD COLUMN IF NOT EXISTS Tags TEXT NULL;

-- 3. Upgrade BusinessRuleHistory (If table exists)
DO $$
BEGIN
    IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'businessrulehistory') THEN
        ALTER TABLE BusinessRuleHistory ADD COLUMN IF NOT EXISTS Categories TEXT NULL;
        ALTER TABLE BusinessRuleHistory ADD COLUMN IF NOT EXISTS Tags TEXT NULL;
    END IF;
END $$;

-- 4. Upgrade BusinessRuleBundles
ALTER TABLE BusinessRuleBundles ADD COLUMN IF NOT EXISTS Categories TEXT NULL;
ALTER TABLE BusinessRuleBundles ADD COLUMN IF NOT EXISTS Tags TEXT NULL;

-- 5. Upgrade Persistent Execution Tracker Logs (If Used)
DO $$
BEGIN
    IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'bundleexecutionlogs') THEN
        ALTER TABLE BundleExecutionLogs ADD COLUMN IF NOT EXISTS Categories TEXT NULL;
        ALTER TABLE BundleExecutionLogs ADD COLUMN IF NOT EXISTS Tags TEXT NULL;
    END IF;
    IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'ruleexecutionlogs') THEN
        ALTER TABLE RuleExecutionLogs ADD COLUMN IF NOT EXISTS Categories TEXT NULL;
        ALTER TABLE RuleExecutionLogs ADD COLUMN IF NOT EXISTS Tags TEXT NULL;
    END IF;
END $$;
```

---

### MySQL Migration Script

```sql
-- Helper procedure for MySQL column addition if not exists
DROP PROCEDURE IF EXISTS UpgradeRulesEngineSchema_v2_3_0;
DELIMITER //
CREATE PROCEDURE UpgradeRulesEngineSchema_v2_3_0()
BEGIN
    -- 1. DbConnections
    IF NOT EXISTS (SELECT * FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'DbConnections' AND column_name = 'Categories') THEN
        ALTER TABLE DbConnections ADD COLUMN Categories TEXT NULL, ADD COLUMN Tags TEXT NULL;
    END IF;

    -- 2. BusinessRules
    IF NOT EXISTS (SELECT * FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'BusinessRules' AND column_name = 'Categories') THEN
        ALTER TABLE BusinessRules ADD COLUMN Categories TEXT NULL, ADD COLUMN Tags TEXT NULL;
    END IF;

    -- 3. BusinessRuleHistory
    IF EXISTS (SELECT * FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'BusinessRuleHistory') AND NOT EXISTS (SELECT * FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'BusinessRuleHistory' AND column_name = 'Categories') THEN
        ALTER TABLE BusinessRuleHistory ADD COLUMN Categories TEXT NULL, ADD COLUMN Tags TEXT NULL;
    END IF;

    -- 4. BusinessRuleBundles
    IF NOT EXISTS (SELECT * FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'BusinessRuleBundles' AND column_name = 'Categories') THEN
        ALTER TABLE BusinessRuleBundles ADD COLUMN Categories TEXT NULL, ADD COLUMN Tags TEXT NULL;
    END IF;

    -- 5. Persistent Execution Tracker Logs
    IF EXISTS (SELECT * FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'BundleExecutionLogs') AND NOT EXISTS (SELECT * FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'BundleExecutionLogs' AND column_name = 'Categories') THEN
        ALTER TABLE BundleExecutionLogs ADD COLUMN Categories TEXT NULL, ADD COLUMN Tags TEXT NULL;
    END IF;

    IF EXISTS (SELECT * FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'RuleExecutionLogs') AND NOT EXISTS (SELECT * FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'RuleExecutionLogs' AND column_name = 'Categories') THEN
        ALTER TABLE RuleExecutionLogs ADD COLUMN Categories TEXT NULL, ADD COLUMN Tags TEXT NULL;
    END IF;
END //
DELIMITER ;

CALL UpgradeRulesEngineSchema_v2_3_0();
DROP PROCEDURE UpgradeRulesEngineSchema_v2_3_0;
```

---

## 3. Automatic Application Startup Migration (C#)

If your host application uses `SqlDatabaseService` (or calls `CreateBusinessRuleTablesIfNotExistsAsync` on application startup), the database auto-migration script runs automatically when the host starts.

```csharp
// Program.cs startup initialization example
using var scope = app.Services.CreateScope();
var dbService = scope.ServiceProvider.GetRequiredService<SqlDatabaseService>();
await dbService.CreateBusinessRuleTablesIfNotExistsAsync();
```

---

## 4. Verification

After executing the migration script, verify that `Categories` and `Tags` columns exist:

```sql
-- SQL Server Verification
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE COLUMN_NAME IN ('Categories', 'Tags');
```
