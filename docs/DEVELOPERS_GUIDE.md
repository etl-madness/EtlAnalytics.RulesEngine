# Developer's Guide - EtlAnalytics.RulesEngine

This guide provides technical instructions for developers integrating or extending the `EtlAnalytics.RulesEngine` library.

## 📦 Package Comparison

Before extending the engine, understand the architectural split between the Core library and the Dapper extension.

| Feature | EtlAnalytics.RulesEngine (Core) | Core + RulesEngine.Dapper |
| :--- | :--- | :--- |
| **Logic Engine** | Orchestrates C# & SQL execution. | Inherited from Core. |
| **SQL Execution** | Abstraction only (`ISqlRuleExecutor`). | Implemented via Dapper. |
| **Database Support** | Agnostic. | SQL Server, PostgreSQL, MySQL. |
| **Data Piping** | Logic provided in `BusinessRuleEngine`. | Handled automatically by `DapperSqlRuleExecutor`. |
| **Security** | Global keyword blacklist logic. | Enforces Core security rules during execution. |

The **Core package** is the "Brain" and contains all the logic for bundle orchestration and C# sandboxing. The **Dapper package** is the "Hands", providing the concrete implementation for database communication.

## 1. Connection String Management

The engine resolves its primary connection string (used for the rule store and default SQL execution) using a hierarchical approach.

### 1.1 Overriding via Environment Variables
The highest priority is given to the `DB_CONNECTION_STRING` environment variable. This is the recommended method for production environments and CI/CD pipelines.

**Windows (PowerShell):**
```powershell
$env:DB_CONNECTION_STRING = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"
```

**Linux/macOS:**
```bash
export DB_CONNECTION_STRING="Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"
```

### 1.2 Overriding via App Settings
If the environment variable is not set, the engine looks for `ConnectionStrings:DefaultConnection` in your `appsettings.json`.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RulesDb;Trusted_Connection=True;"
  }
}
```

---

## 2. SQL Security Sandbox (Forbidden Keywords)

The engine includes a security layer that scans T-SQL rules for potentially dangerous keywords before execution.

### 2.1 Default Forbidden Keywords
By default, the following keywords are blocked:
`DROP`, `TRUNCATE`, `DELETE`, `UPDATE`, `INSERT`, `GRANT`, `REVOKE`, `ALTER`, `CREATE`, `xp_cmdshell`, `sys.`, `information_schema`.

### 2.2 Overriding or Adding Keywords
You can customize the forbidden keywords list via the `IConfiguration` provider (e.g., in `appsettings.json`). This will **replace** the default list entirely.

**appsettings.json:**
```json
{
  "RulesEngine": {
    "ForbiddenSqlKeywords": [
      "DROP",
      "TRUNCATE",
      "DELETE",
      "UPDATE",
      "INSERT",
      "SHUTDOWN",
      "DBCC"
    ]
  }
}
```

### 2.3 Implementation Details
The keywords are checked case-insensitively. If any forbidden keyword is detected in a T-SQL rule, a `SecurityException` is thrown, and the execution is logged as a `[SECURITY ALERT]`.

---

## 3. Extending the Engine

### 3.1 Custom Contexts
Always inherit from `RuleExecutionContext` to provide your rules with application-specific data and services.

```csharp
public class MyAppContext : RuleExecutionContext {
    public int CurrentUserId { get; set; }
    public IMyService Service { get; set; }
}
```

### 3.2 Custom SQL Executors
If you need to use something other than Dapper (e.g., Entity Framework), implement the `ISqlRuleExecutor` interface and register it in your Dependency Injection container.

```csharp
public class EfSqlRuleExecutor : ISqlRuleExecutor {
    public async Task<IEnumerable<dynamic>> ExecuteAsync(...) {
        // Your EF implementation here
    }
}
```
