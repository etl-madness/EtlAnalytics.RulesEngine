# EtlAnalytics.RulesEngine - Version 2.1.0 (Multi-Targeted) 🚀

This package targets both **.NET 8** and **.NET 10**, allowing you to use the latest framework features while maintaining compatibility with stable environments.

Welcome to the **EtlAnalytics.RulesEngine**! If you're new to the concept of a "Rules Engine," think of this library as a way to let your users (or yourself) change how your program behaves without having to rewrite or redeploy the whole application.


---
| If the user wants to... | Use the Core Package | Use the Dapper Package |
| :--- | :---: | :---: |
| Only run C# Script rules | ✅ | ❌ |
| Run Javascript rules | ✅ (with extension) | ❌ |
| Use your default SQL features | ✅ | ✅ |
| Run SQL rules using Dapper | ✅ | ✅ |
| Run SQL rules using Entity Framework | ✅ | ❌ (They implement their own ISqlRuleExecutor) |
| Run rules against a Web API/NoSQL | ✅ | ❌ (They implement their own ISqlRuleExecutor) |


> [!NOTE]
> ### Source Code 
> - **Version in master branch may be ahead of nuget releases.**
> ### AI Development Support
> This codebase includes structured documentation designed for AI consumption. If you are an AI agent, please prioritize scanning these documents:
> - **[Architecture Overview](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/ARCHITECTURE_OVERVIEW.md)**: Comprehensive architectural guide with Mermaid diagrams covering system components, sequence orchestration, async tracking, and sandboxing.
> - **[Schema Upgrade Guide](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/SCHEMA_UPGRADE.md)**: Idempotent database migration scripts (SQL Server, Postgres, MySQL) for upgrading database schemas across versions.
> - **[AI Implementation Guide](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/ai_implementation_guide.md)**: Technical reference for integration, sandboxing constraints, and data-passing patterns.
> - **[Integration Example](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/Example.md)**: Full C# example showing DI setup and usage in `Program.cs`.
> - **[Example Rules Engine Editor and Runner](https://github.com/etl-madness/BusinessRulesEngineExample)**: A Radzen Blazor Server based application for editing and running rules in a web interface.
> - **[ExampleSimple.md](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/ExampleSimple.md)**: A simplified, single-file example demonstrating basic usage of the rules engine in a console application.
> - **[ExampleSimpleXML.md](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/ExampleSimpleXML.md)**: A single-file console example demonstrating how to load business rules from an XML file (`rules.xml`) using an `XmlRuleStore`.
> - **[BUSINESS_USE_CASES.md](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/BUSINESS_USE_CASES.md)**: Real-world business use cases including Zero-Day exploit remediation, fraud detection, and dynamic pricing.
> - **[BUSINESS_RULES.md](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/BUSINESS_RULES.md)**: Example Business Rules documentation
> - **[DEVELOPERS_GUIDE.md](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/DEVELOPERS_GUIDE.md)**: Developers Guide.
> - **[Security Blacklist Modification](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/docs/forbidden_keywords_modification.md)**: Instructions for managing the SQL security sandbox.
> - **[Source Code](https://github.com/etl-madness/EtlAnalytics.RulesEngine)**: Release source code for version 2.0.1.

---

## 💡 What is a Rules Engine?

Imagine you are building a pizza delivery app. Usually, you might hard-code a rule like:
`if (orderTotal > 50) { applyDiscount = true; }`

But what if you want to change that limit to $60 tomorrow? Or offer a special discount only for a specific city? Instead of changing your C# code and restarting your server, you can store these instructions as **Rules** in a database. This library is the "engine" that reads those instructions and makes them happen.

---

## 📦 Flexible Storage Options

This library is built with flexibility in mind. You are not locked into any specific way of storing your rules. By implementing the `IBusinessRuleStore` interface, you can source your rules from anywhere:

*   **Relational Databases**: Store rules in SQL Server, PostgreSQL, or MySQL for dynamic, real-time updates.
*   **Static Files**: Use XML or JSON files to keep rules alongside your source code for version control.
*   **Centralized APIs**: Fetch rule definitions from a remote web service to share logic across multiple microservices.
*   **In-Memory/Hardcoded**: For testing or fixed logic, you can even store rules in a simple C# list.

---

The core engine is designed to be database-agnostic and is decoupled from specific SQL execution libraries. This means the core package has **zero dependencies** on Dapper or specific database drivers.

### 🏗️ Modular Architecture

By splitting the library into two packages, we provide maximum flexibility for different hosting environments:

| Package | Purpose | Use When... |
| :--- | :--- | :--- |
| **EtlAnalytics.RulesEngine** | The "Brain". Handles logic, C# scripting, and rule orchestration. | Always. This is the core library. |
| **EtlAnalytics.RulesEngine.Dapper** | The "Hands (SQL)". Provides SQL execution using the Dapper library. | You want to run T-SQL rules using our default provider. |
| **EtlAnalytics.RulesEngine.Javascript** | The "Hands (JS)". Provides Javascript execution using the Jint engine. | You want to run Javascript rules. |

#### **When to use each?**
*   **Core only**: Use this if you only need C# Script rules, or if you want to implement your own execution logic.
*   **Core + Dapper**: Use this for the full experience with T-SQL, Postgres, and MySQL.
*   **Core + Javascript**: Use this to enable browser-like scripting within your rules.

---

### 1. Registering the SQL Executor

To execute SQL rules, you must register an implementation of `ISqlRuleExecutor`. We provide a Dapper-based implementation in the `EtlAnalytics.RulesEngine.Dapper` package.

```csharp
// Core Engine Setup
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<BusinessRuleEngine<PizzaAppContext>>();

// 1. Register the SQL Executor (Optional)
builder.Services.AddScoped<ISqlRuleExecutor, DapperSqlRuleExecutor>();
builder.Services.AddScoped<IRuleDbProvider, SqlServerRuleDbProvider>();

// 2. Register the Javascript Executor (Optional)
builder.Services.AddJavascriptRules();
```

### 2. Custom Connection Providers

To support different databases, you implement `IRuleDbProvider`.

#### **SQL Server (Microsoft.Data.SqlClient)**
```csharp
public class SqlServerRuleDbProvider : IRuleDbProvider
{
    public string ProviderType => "SqlServer";
    public IDbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);
}
```

---

## 🔗 Multi-Database Rulesets (Cross-Database Logic)

You can now configure individual rules within a bundle to run against different databases. This is perfect for auditing across multiple systems or consolidating data from disparate sources.

### 1. Define your Connections
First, store your connection strings in the `DbConnections` table. 

> [!NOTE]
> Connection strings are stored **encrypted** using **AES-256** in the database.

| Id | Name | ConnectionString (Encrypted) | ProviderType |
| :--- | :--- | :--- | :--- |
| 1 | ProductionDB | `uP6+...[Base64 Encrypted Blob]...` | `SqlServer` |
| 2 | AuditDB | `vA2x...[Base64 Encrypted Blob]...` | `SqlServer` |

### 2. Link Rules to Connections
When creating a rule, specify the `ConnectionId`:

```sql
-- Rule: "Audit Order" (ConnectionId: 2)
INSERT INTO dbo.OrderAudit (OrderId, Status)
SELECT OrderId, 'Processed' FROM OPENJSON(@PreviousResultJson) WITH (OrderId INT '$')
```

The engine will automatically switch to the **AuditDB** connection for this specific step!

---

## 🔒 Security & Encryption

Database connection strings are sensitive data. The library includes built-in support for **AES-256 encryption with a PBKDF2-derived key** to protect these strings at rest in your database.

### 1. How it Works
The `BusinessRuleEngine` automatically manages security for you:
*   **Decryption**: Connection strings are decrypted on-the-fly when retrieved from the `IBusinessRuleStore`.
*   **C# Sandboxing**: Scripts are executed in a restricted environment with limited assembly access and mandatory timeouts.
*   **SQL Safety**: Queries are validated for dangerous keywords and forced to respect execution timeouts.

### 2. Key Configuration
You must provide an encryption secret for the AES algorithm. The `AesEncryptionService` uses **PBKDF2** (100,000 iterations) to derive a 256-bit key. It resolves the secret in this order:
1.  **`DB_ENCRYPTION_KEY`** (Environment Variable) - *Recommended for production*.
2.  **`Security:EncryptionKey`** (AppSettings.json).

### 3. Setup in Program.cs
```csharp
// Register the encryption service as a singleton
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
```

### 4. C# Script Sandboxing
To protect against arbitrary code execution, the C# scripting engine is hardened:
*   **Assembly Whitelisting**: Only core assemblies (System, Linq, Collections) and your Context assembly are accessible. `System.IO`, `System.Net`, and `System.Diagnostics` are blocked.
*   **Execution Timeouts**: All C# scripts are capped at a **10-second timeout**.
*   **Cooperative Cancellation**: Scripts can access `CancellationToken` via the context to handle early termination.

### 5. SQL Sandboxing & Safety
To prevent accidental or malicious data modification via T-SQL rules:
*   **Keyword Filtering**: The engine blocks queries containing dangerous keywords (e.g., `DROP`, `TRUNCATE`, `DELETE`, `UPDATE`, `GRANT`, `EXEC`).
*   **Command Timeouts**: All SQL queries are capped at a **30-second timeout**.
*   **Infrastructure (Recommended)**: 
    > [!IMPORTANT]
    > Always use a **Least Privilege** service account. The engine user should only have `SELECT` permissions and should **never** be a member of `db_owner`.

---

## 📊 Cross-Database SQL Syntax

When writing **T-SQL** rules that use **Piping** (receiving data from a previous rule), the syntax for reading the `@PreviousResultJson` parameter varies by database.

| Database | Parameter Prefix | JSON Extraction Example |
| :--- | :--- | :--- |
| **SQL Server** | `@` | `CROSS APPLY OPENJSON(@PreviousResultJson) WITH (Val INT '$.Status')` |
| **PostgreSQL** | `:` | `SELECT * FROM table WHERE data ->> 'Status' = :PreviousResultJson` |
| **MySQL** | `?` | `SELECT * FROM table WHERE JSON_EXTRACT(?PreviousResultJson, '$.Status') = 1` |

### Database-Specific Rule Examples

#### **SQL Server Example**
```sql
SELECT TOP 1 * FROM Discounts 
CROSS APPLY OPENJSON(@PreviousResultJson) WITH (CustomerType NVARCHAR(50)) p
WHERE p.CustomerType = 'VIP'
```

#### **PostgreSQL Example**
```sql
SELECT * FROM "Discounts" 
WHERE "CustomerType" = CAST(:PreviousResultJson->>'CustomerType' AS TEXT)
LIMIT 1;
```

#### **MySQL Example**
```sql
SELECT * FROM Discounts 
WHERE CustomerType = JSON_UNQUOTE(JSON_EXTRACT(?PreviousResultJson, '$.CustomerType'))
LIMIT 1;
```

---

## 🛠️ Key Concepts to Learn

Before you start coding, here are the main parts of this engine:

5. **Executor**: The component that actually runs the code (TSQL, C#, Javascript).
6.  **Sequence Group**: A collection of rules within a bundle that share the same `SequenceOrder` and are executed in parallel.

---

## ⚡ Parallel Execution

You can now execute multiple rules within a bundle concurrently by assigning them the same **`SequenceOrder`**.

### How it Works
1.  **Orchestration**: The engine groups rules by `SequenceOrder`.
2.  **Concurrency**: If a group has more than one rule, they are executed in parallel using `Task.WhenAll`.
3.  **Synchronization**: The engine waits for all rules in the current sequence group to complete before moving to the next sequence.
4.  **Result Aggregation**: The results from a parallel group are aggregated into a `List<object?>`.

### Accessing Parallel Results
When a rule follows a parallel group, its `PreviousResult` will contain the list of results from all rules in that group.

```csharp
// In a downstream C# rule following a parallel sequence:
var parallelResults = (List<object?>)PreviousResult;

// Positional Indexing:
// Index [0] = 1st parallel rule configured in this sequence
// Index [1] = 2nd parallel rule configured in this sequence
var rule1Data = parallelResults[0];
var rule2Data = parallelResults[1];

Log($"Collected results: Rule 1 = {rule1Data}, Rule 2 = {rule2Data}");
```

> [!TIP]
> **Lookup by Rule Name or ID via Tracker**: Callers monitoring state via `IBundleExecutionTracker` can also target parallel rule outcomes directly by `RuleName` or `RuleId`:
> ```csharp
> var state = await tracker.GetExecutionAsync(executionId);
> var seq2 = state.Sequences.First(s => s.SequenceOrder == 2);
> var rule102Data = seq2.Rules.First(r => r.RuleId == 102).Result;
> var threatData = seq2.Rules.First(r => r.RuleName == "Filter Threat IPs").Result;
> ```

---

## ⏱️ Asynchronous Execution & Granular Status Tracking

For long-running bundles, callers can trigger execution asynchronously and monitor step-by-step progress (`Pending` $\rightarrow$ `Starting` $\rightarrow$ `Completed` / `Failed`) across all sequence groups and parallel rules.

### Features
* **Thread-Safe State Store**: `IBundleExecutionTracker` and `InMemoryBundleExecutionTracker` provide real-time state snapshots.
* **Pre-populated Execution Tree**: Automatically pre-populates all sequences and parallel rules in `Pending` state before execution starts.
* **Real-time Progress Events**: Observe lifecycle updates via the `OnStatusChanged` event.
* **API Non-Blocking Integration**: Callers receive an `executionId` immediately and poll state without keeping HTTP connections open.

### Quick Setup

```csharp
// 1. Register tracking in DI
builder.Services.AddBusinessRulesEngineTracking();

// 2. Initialize tracking entry and run bundle asynchronously
var tracker = serviceProvider.GetRequiredService<IBundleExecutionTracker>();
var executionState = await tracker.CreateExecutionAsync(bundle);

_ = Task.Run(async () =>
{
    await engine.ExecuteBundleAsync(bundle, context, appendLog: null, tracker: tracker, executionId: executionState.ExecutionId);
});

// 3. Query current progress anytime
var statusSnapshot = await tracker.GetExecutionAsync(executionState.ExecutionId);
```

For full details and Web API examples, see the [Asynchronous Execution Tracking Guide](docs/EXECUTION_TRACKING.md). To persist execution logs in SQL Server, PostgreSQL, or MySQL across server restarts, see the [Persistent Database Execution Tracking Guide](docs/PERSISTENT_EXECUTION_TRACKING.md).

---

## 🚀 Quick Start Guide (The 5-Step Setup)

### 1. Install the Library
Add the project reference to your application:
```bash
dotnet add package EtlAnalytics.RulesEngine
# Optional: Add the Dapper executor if you need SQL support
dotnet add package EtlAnalytics.RulesEngine.Dapper
```

### 2. Prepare your "Data Box" (Context)
The engine needs to know what data your rules should work with. Create a class that inherits from `RuleExecutionContext`.

```csharp
// This is your custom 'Context'. Everything inside here is visible to your rules.
public class PizzaAppContext : RuleExecutionContext
{
    public double OrderTotal { get; set; }
    public string CustomerCity { get; set; }
    // Inherited: CancellationToken, PreviousResult, StepResults, etc.
}
```

### 3. Setup Your Database (The Store)
The engine needs to find your rules in a database. Here is the recommended SQL schema to store your Rules and Bundles.

#### **SQL Server Schema**
```sql


CREATE TABLE dbo.DbConnections (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    ConnectionString NVARCHAR(MAX) NOT NULL, -- Stored as AES-256 encrypted Base64
    ProviderType NVARCHAR(100) NOT NULL DEFAULT 'SqlServer',
    Categories NVARCHAR(MAX) NULL, -- JSON array of category strings
    Tags NVARCHAR(MAX) NULL,       -- JSON array of tag strings
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.BusinessRules (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    RuleType NVARCHAR(50) NOT NULL, -- 'TSQL', 'CSharp', or 'Javascript'
    Code NVARCHAR(MAX) NOT NULL,
    ConnectionId INT NULL, -- Optional: Link to a specific database
    Categories NVARCHAR(MAX) NULL, -- JSON array of category strings
    Tags NVARCHAR(MAX) NULL,       -- JSON array of tag strings
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_BusinessRules_Connection FOREIGN KEY (ConnectionId) REFERENCES dbo.DbConnections(Id)
);

CREATE TABLE dbo.BusinessRuleBundles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Categories NVARCHAR(MAX) NULL, -- JSON array of category strings
    Tags NVARCHAR(MAX) NULL,       -- JSON array of tag strings
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE dbo.BusinessRuleBundleItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BundleId INT NOT NULL,
    RuleId INT NOT NULL,
    SequenceOrder INT NOT NULL, -- Items with the same SequenceOrder run in parallel
    CONSTRAINT FK_BundleItems_Bundle FOREIGN KEY (BundleId) REFERENCES dbo.BusinessRuleBundles(Id) ON DELETE CASCADE
);

```sql
-- Bundle-level execution run table
CREATE TABLE dbo.BundleExecutionLogs (
    ExecutionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    BundleId INT NOT NULL,
    BundleName NVARCHAR(255) NOT NULL,
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
```

#### **PostgreSQL Schema**
```sql
DROP TABLE IF EXISTS BusinessRuleBundleItems;
DROP TABLE IF EXISTS BusinessRuleBundles;
DROP TABLE IF EXISTS BusinessRules;
DROP TABLE IF EXISTS DbConnections;

CREATE TABLE DbConnections (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    ConnectionString TEXT NOT NULL,
    ProviderType VARCHAR(100) NOT NULL DEFAULT 'SqlServer',
    Categories TEXT NULL,
    Tags TEXT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE BusinessRules (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    RuleType VARCHAR(50) NOT NULL,
    Code TEXT NOT NULL,
    ConnectionId INT REFERENCES DbConnections(Id),
    Categories TEXT NULL,
    Tags TEXT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE BusinessRuleBundles (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    Categories TEXT NULL,
    Tags TEXT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE BusinessRuleBundleItems (
    Id SERIAL PRIMARY KEY,
    BundleId INT NOT NULL REFERENCES BusinessRuleBundles(Id) ON DELETE CASCADE,
    RuleId INT NOT NULL REFERENCES BusinessRules(Id),
    SequenceOrder INT NOT NULL -- Items with the same SequenceOrder run in parallel
);
```

#### **MySQL Schema**
```sql
DROP TABLE IF EXISTS BusinessRuleBundleItems;
DROP TABLE IF EXISTS BusinessRuleBundles;
DROP TABLE IF EXISTS BusinessRules;
DROP TABLE IF EXISTS DbConnections;

CREATE TABLE DbConnections (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    ConnectionString TEXT NOT NULL,
    ProviderType VARCHAR(100) NOT NULL DEFAULT 'SqlServer',
    Categories TEXT NULL,
    Tags TEXT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE BusinessRules (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    RuleType VARCHAR(50) NOT NULL, -- 'TSQL', 'CSharp', or 'Javascript'
    Code TEXT NOT NULL,
    ConnectionId INT,
    Categories TEXT NULL,
    Tags TEXT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    FOREIGN KEY (ConnectionId) REFERENCES DbConnections(Id)
);

CREATE TABLE BusinessRuleBundles (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    Categories TEXT NULL,
    Tags TEXT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE BusinessRuleBundleItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    BundleId INT NOT NULL,
    RuleId INT NOT NULL,
    SequenceOrder INT NOT NULL, -- Items with the same SequenceOrder run in parallel
    FOREIGN KEY (BundleId) REFERENCES BusinessRuleBundles(Id) ON DELETE CASCADE,
    FOREIGN KEY (RuleId) REFERENCES BusinessRules(Id)
);
```

#### Implementing IBusinessRuleStore with Dapper
You can make your store database-agnostic by using the same `IRuleDbProvider` you created earlier. This allows the same Store code to work for SQL Server, PostgreSQL, or MySQL.

```csharp
public class AppRuleStore : IBusinessRuleStore
{
    private readonly string _connectionString;
    private readonly IRuleDbProvider _dbProvider;

    public AppRuleStore(IConfiguration config, IRuleDbProvider dbProvider)
    {
        _connectionString = config.GetConnectionString("DefaultConnection");
        _dbProvider = dbProvider;
    }

    public async Task<BusinessRule?> GetBusinessRuleByIdAsync(int id)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        // Note: Use the parameter prefix correct for your DB (@ for SQL Server, : for Postgres)
        return await db.QueryFirstOrDefaultAsync<BusinessRule>(
            "SELECT * FROM BusinessRules WHERE Id = @Id", new { Id = id });
    }

    public async Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        var bundle = await db.QueryFirstOrDefaultAsync<BusinessRuleBundle>(
            "SELECT * FROM BusinessRuleBundles WHERE Name = @Name", new { Name = name });

        if (bundle != null)
        {
            var items = await db.QueryAsync<BusinessRuleBundleItem>(
                "SELECT * FROM BusinessRuleBundleItems WHERE BundleId = @Id ORDER BY SequenceOrder", 
                new { Id = bundle.Id });
            bundle.Items = items.ToList();
        }
        return bundle;
    }
}
```

#### **Option B: Storing Rules in XML (File-based)**
If you prefer to keep your rules in a file (for version control or simple projects), you can implement a Store that reads from XML.

```csharp
public class XmlRuleStore : IBusinessRuleStore
{
    private readonly List<BusinessRule> _rules;
    private readonly List<BusinessRuleBundle> _bundles;

    public XmlRuleStore(string filePath)
    {
        var doc = XDocument.Load(filePath);
        // Load rules and bundles using LINQ to XML or XmlSerializer
        _rules = doc.Descendants("Rule").Select(r => new BusinessRule { ... }).ToList();
        _bundles = doc.Descendants("Bundle").Select(b => new BusinessRuleBundle { ... }).ToList();
    }

    public Task<BusinessRule?> GetBusinessRuleByIdAsync(int id) => 
        Task.FromResult(_rules.FirstOrDefault(r => r.Id == id));

    public Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name) => 
        Task.FromResult(_bundles.FirstOrDefault(b => b.Name == name));

    public Task<DbConnectionDefinition?> GetDbConnectionByIdAsync(int id) => 
        Task.FromResult((DbConnectionDefinition?)null); // Implement if using XML connections

    public Task<IEnumerable<DbConnectionDefinition>> GetAllDbConnectionsAsync() => 
        Task.FromResult(Enumerable.Empty<DbConnectionDefinition>());
}
```

#### **Option C: Sourcing Rules from an API (JSON or XML)**
You can also centralize your rules behind a web service. This is useful if you want multiple applications to share the same rule definitions.

##### **JSON Data Format**
The engine expects your API to return data in a structure like this:
```json
{
  "rules": [
    {
      "id": 101,
      "name": "CheckInventory",
      "ruleType": "CSharp",
      "code": "return Items.All(i => i.InStock);",
      "connectionId": null,
      "isActive": true
    }
  ],
  "bundles": [
    {
      "name": "OrderValidation",
      "items": [
        { "ruleId": 101, "sequenceOrder": 1 }
      ]
    }
  ]
}
```

##### **Implementing an API Store**
```csharp
public class ApiRuleStore : IBusinessRuleStore
{
    private readonly HttpClient _http;

    public ApiRuleStore(HttpClient http) => _http = http;

    public async Task<BusinessRule?> GetBusinessRuleByIdAsync(int id)
    {
        // Fetch from a JSON API
        return await _http.GetFromJsonAsync<BusinessRule>($"https://api.rules.com/rules/{id}");
    }

    public async Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name)
    {
        // Or fetch from an XML API
        var xmlString = await _http.GetStringAsync($"https://api.rules.com/bundles?name={name}");
        var doc = XDocument.Parse(xmlString);
        return ParseBundleFromXml(doc);
    }

    public async Task<DbConnectionDefinition?> GetDbConnectionByIdAsync(int id) =>
        await _http.GetFromJsonAsync<DbConnectionDefinition>($"https://api.rules.com/connections/{id}");

    public async Task<IEnumerable<DbConnectionDefinition>> GetAllDbConnectionsAsync() =>
        await _http.GetFromJsonAsync<IEnumerable<DbConnectionDefinition>>("https://api.rules.com/connections") ?? new List<DbConnectionDefinition>();
}
```

---

### 4. Register the Engine
In your `Program.cs` (or where you setup your services), tell your app how to use the engine:

```csharp
builder.Services.AddScoped<IBusinessRuleStore, AppRuleStore>();
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

// Register all supported database providers
builder.Services.AddScoped<IRuleDbProvider, SqlServerRuleDbProvider>();
builder.Services.AddScoped<IRuleDbProvider, PostgresRuleDbProvider>();

builder.Services.AddScoped<BusinessRuleEngine<PizzaAppContext>>();
```

### 5. Run a Rule!
Now you can inject the engine into your classes and use it:

```csharp
public class CheckoutService
{
    private readonly BusinessRuleEngine<PizzaAppContext> _engine;

    public CheckoutService(BusinessRuleEngine<PizzaAppContext> engine) => _engine = engine;

    public async Task ProcessCheckout(double total, string city)
    {
        var myData = new PizzaAppContext { OrderTotal = total, CustomerCity = city };
        
        // Let's assume you have a rule in your DB named "CalculateDiscount"
        var rule = await _store.GetRuleByName("CalculateDiscount");
        
        // Execute the rule!
        var result = await _engine.ExecuteRuleAsync(rule, myData);
        
        Console.WriteLine($"Rule Result: {result}");
    }
}
```

---

## 📝 Writing Your First Rules

### The Javascript Rule (New! ✨)
If you prefer Javascript, you can write rules like this:
```javascript
// Properties from your PizzaAppContext are available via the 'context' object
if (context.OrderTotal > 100) {
    log("High value order in JS");
    return context.OrderTotal * 0.1; // 10% discount
}
return 0;
```

### The C# Script Rule
In your database, you might save a rule with this code:
```csharp
// You can use properties from your PizzaAppContext directly!
if (OrderTotal > 100) {
    Log("Big spender detected!"); // You can log messages to a watch window
    return 20.0; // Give them $20 off
}
return 0.0;
```

### The T-SQL Rule
If your rule needs to check the database, write a SQL script:
```sql
-- The engine automatically provides @PreviousResultJson
-- This allows you to use data from a previous rule in a bundle!
SELECT * FROM HolidayCoupons 
WHERE MinSpend <= @OrderTotal 
  AND City = @CustomerCity
```

---

## 🔄 Advanced Tip: Rule Piping (Connecting Rules)

One of the coolest features is **Piping**. When you run a **Bundle** (a sequence of rules), the result of Rule #1 is automatically handed to Rule #2 as `PreviousResult`.

**Example Bundle:**
1.  **Rule #1 (C#)**: Checks if user is a "VIP". Returns `true`.
2.  **Rule #2 (SQL)**: Receives `true`. Runs a special query that only VIPs can see.

---

## 🌳 Branching & Conditional Logic

You can create complex workflows by triggering different **Rule Bundles** based on logic within a rule. This is done using the `RunBundle` function available in C# rules.

### Example: The "Smart Discount" Workflow

Imagine you have two separate bundles:
- `HighValueBundle`: Contains 5 complex rules for big spenders.
- `StandardBundle`: Contains 2 simple rules for everyone else.

You can create a "Router" rule in C# to decide which one to run:

#### **Router Rule (C#)**
```csharp
// The 'RunBundle' function is built-in to the context!
if (OrderTotal > 500) {
    Log("Switching to High Value ruleset...");
    return await RunBundle("HighValueBundle");
} else {
    Log("Using standard ruleset.");
    return await RunBundle("StandardBundle");
}
```

### Conditional Execution in T-SQL

In T-SQL, you can use the result of a previous rule to filter your current query.

#### **Rule #2 (SQL)**
```sql
-- Assuming Rule #1 returned a boolean (true/false)
-- We can use OPENJSON to check that boolean before returning data
SELECT 
    CASE 
        WHEN p.Result = 1 THEN 'Eligible for Extra Points'
        ELSE 'Standard Points'
    END as Status
FROM OPENJSON(@PreviousResultJson) WITH (Result BIT '$') p
```

---

---

## 🚀 Publishing the Packages

### 1. Generating the Packages
To create the `.nupkg` files, run:
```bash
dotnet pack -c Release -o ./dist
```

### 2. Local Testing (Recommended)
Before publishing to a public feed, you can test the packages locally.

**Create a local NuGet source:**
```bash
dotnet nuget add source C:\MyLocalNuGetFeed -n LocalFeed
```

**Push to the local source:**
```bash
dotnet nuget push ./dist/*.nupkg -s LocalFeed
```

### 3. Publishing to NuGet.org
To publish to the public NuGet gallery:

```bash
dotnet nuget push ./dist/EtlAnalytics.RulesEngine.2.0.0.nupkg -s https://api.nuget.org/v3/index.json -k YOUR_API_KEY
dotnet nuget push ./dist/EtlAnalytics.RulesEngine.Dapper.2.0.0.nupkg -s https://api.nuget.org/v3/index.json -k YOUR_API_KEY
```

### 4. Publishing via IDE

#### **Visual Studio**
1. Right-click the project in **Solution Explorer**.
2. Select **Pack**. The files will be generated in `bin/Release`.
3. You can use the **NuGet Package Explorer** or the command line to push.

#### **VS Code**
1. Open the **Command Palette** (`Ctrl+Shift+P`).
2. Type `Tasks: Run Task` and select `dotnet pack`.
3. Use the integrated terminal to run the `dotnet nuget push` commands listed above.

---

## ❓ Need Help?
- **Logs**: Always pass a logging action to `ExecuteRuleAsync` to see what's happening inside: `log => Console.WriteLine(log)`.
- **Errors**: If a C# rule has a typo, the engine will return a clear compilation error in the logs.

Happy coding! 🍕

---

## 📄 License

This project is licensed under the **MIT License**. See the [LICENSE](./LICENSE) file for details.
