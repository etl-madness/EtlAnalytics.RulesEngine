# Developer's Guide - EtlAnalytics.RulesEngine

This guide provides technical instructions for developers integrating or extending the `EtlAnalytics.RulesEngine` library.

## 📦 Package Comparison

Before extending the engine, understand the architectural split between the Core library and the Dapper extension.

| Feature | EtlAnalytics.RulesEngine (Core) | Core + RulesEngine.Dapper | Core + RulesEngine.Javascript |
| :--- | :--- | :--- | :--- |
| **Logic Engine** | Orchestrates script & SQL execution. | Inherited from Core. | Inherited from Core. |
| **Execution** | Pluggable via `IRuleExecutor`. | TSQL implemented via Dapper. | Javascript implemented via Jint. |
| **Database Support** | Agnostic. | SQL Server, PostgreSQL, MySQL. | N/A (JS only). |
| **Security** | Global keyword blacklist logic. | Enforces Core security rules. | Enforces JS timeout limits. |

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
    "SqlTimeoutSeconds": 60,
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

## 3. C# Script Security Sandbox (References & Imports)

The C# scripting engine uses Roslyn `ScriptOptions` to control assembly references (`WithReferences`) and namespace imports (`WithImports`).

### 3.1 Default References & Imports
By default, script execution is restricted to core system assemblies and model namespaces:
- **Default References**: `System.Runtime`, `System.Linq`, `System.Collections`, and `EtlAnalytics.RulesEngine`.
- **Default Imports**: `System`, `System.Collections.Generic`, `System.Linq`, `System.Text`, `System.Threading.Tasks`, and `EtlAnalytics.RulesEngine.Models`.

### 3.2 Customizing References, Imports, and Timeouts
You can customize the allowed assembly references, imports, and script execution timeout via the `IConfiguration` provider (e.g., in `appsettings.json`).

**appsettings.json:**
```json
{
  "RulesEngine": {
    "SqlTimeoutSeconds": 60,
    "ScriptTimeoutSeconds": 15,
    "WithReferences": [
      "System.Runtime",
      "System.Linq",
      "System.Text.Json"
    ],
    "WithImports": [
      "System",
      "System.Collections.Generic",
      "System.Linq",
      "System.Text.Json"
    ]
  }
}
```

> [!NOTE]
> The engine accepts `SqlTimeoutSeconds` (or `SqlTimeout` / `CommandTimeout`) for SQL execution timeouts, and `ScriptTimeoutSeconds` (or `ScriptTimeout`) for C# script timeouts.

---

## 4. Extending the Engine

### 4.1 Custom Contexts
Always inherit from `RuleExecutionContext` to provide your rules with application-specific data and services.

```csharp
public class MyAppContext : RuleExecutionContext {
    public int CurrentUserId { get; set; }
    public IMyService Service { get; set; }
}
```

### 4.2 Custom Rule Executors (Extending the Engine)
You can extend the engine to support any language or protocol by implementing the `IRuleExecutor` interface.

```csharp
public class PythonRuleExecutor : IRuleExecutor
{
    public string RuleType => "Python";

    public async Task<object?> ExecuteAsync(BusinessRule rule, RuleExecutionContext context, Type contextType, Action<string>? appendLog)
    {
        // Your logic to execute Python code here
        return result;
    }
}
```

Register it in DI:
```csharp
services.AddSingleton<IRuleExecutor, PythonRuleExecutor>();
```

### 4.3 Custom SQL Executors
If you need to use something other than Dapper (e.g., Entity Framework) for the TSQL type, implement the `ISqlRuleExecutor` interface. This is consumed by the built-in `TsqlRuleExecutor`.

```csharp
public class EfSqlRuleExecutor : ISqlRuleExecutor {
    public async Task<IEnumerable<dynamic>> ExecuteAsync(...) {
        // Your EF implementation here
    }
}
```
