# Developer's Guide - EtlAnalytics.RulesEngine

This guide provides technical instructions for developers integrating or extending the `EtlAnalytics.RulesEngine` library.

## 📦 Package Comparison

Before extending the engine, understand the architectural split between the Core library and the Dapper extension.

| Feature | EtlAnalytics.RulesEngine (Core) | Core + RulesEngine.Dapper | Core + RulesEngine.Javascript |
| :--- | :--- | :--- | :--- |
| **Logic Engine** | Orchestrates script & SQL execution, supporting parallel sequence groups. | Inherited from Core. | Inherited from Core. |
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

---

## 5. Bundle Orchestration & Parallelism

The `BusinessRuleEngine` uses a grouping strategy to handle rule execution within a bundle.

### 5.1 Parallel Grouping
Rules are grouped by their `SequenceOrder`. The engine iterates through these groups in ascending order.
- **Single Item Groups**: Executed sequentially using `await ExecuteRuleAsync`.
- **Multi-Item Groups**: Executed concurrently using `Task.WhenAll`.

### 5.2 Internal Result Aggregation
When executing a parallel group, the engine collects all results into a `List<object?>`. 
- This list is then assigned to `baseContext.PreviousResult` for the next group.
- The list is also stored in `baseContext.StepResults[sequenceOrder]`.

### 5.3 Exception Handling
The engine follows a "fail-fast" approach. If any rule in a parallel group throws an exception, the `Task.WhenAll` will propagate it, and the engine will catch it, log a `[FATAL]` error, and stop the bundle execution immediately.

### 5.4 Targeting Specific Rules in a Parallel Group
When rules execute in parallel under the same `SequenceOrder`, their results are stored in `List<object?>`.

1. **Positional Indexing (`PreviousResult[i]`)**: Index `[0]` corresponds to the 1st rule item configured in `bundle.Items` for that sequence, and index `[1]` corresponds to the 2nd rule item:
   ```csharp
   var parallelResults = (List<object?>)PreviousResult;
   var rule1Result = parallelResults[0]; // First parallel rule
   var rule2Result = parallelResults[1]; // Second parallel rule
   ```

2. **Lookup by Rule ID or Rule Name via `IBundleExecutionTracker`**:
   ```csharp
   var state = await tracker.GetExecutionAsync(executionId);
   var parallelSeq = state?.Sequences.FirstOrDefault(s => s.SequenceOrder == 2);

   // Target by Rule ID
   var ruleA = parallelSeq?.Rules.FirstOrDefault(r => r.RuleId == 101)?.Result;

   // Target by Rule Name
   var ruleB = parallelSeq?.Rules.FirstOrDefault(r => r.RuleName == "Fetch Inventory")?.Result;
   ```

---

## 6. Asynchronous Execution & Real-Time Status Tracking

The engine includes thread-safe state tracking for monitoring long-running bundles asynchronously without blocking client threads.

### 6.1 Service Registration
Register the execution tracker in Dependency Injection using the extension method:

```csharp
builder.Services.AddBusinessRulesEngineTracking();
```

This registers `IBundleExecutionTracker` with the default `InMemoryBundleExecutionTracker` implementation as a Singleton.

### 6.2 Pre-populating and Triggering Async Execution
To trigger execution asynchronously:

```csharp
var tracker = serviceProvider.GetRequiredService<IBundleExecutionTracker>();

// 1. Pre-populate all sequences and parallel rules in 'Pending' status
var state = await tracker.CreateExecutionAsync(bundle);
Guid executionId = state.ExecutionId;

// 2. Execute bundle asynchronously in a background task
_ = Task.Run(async () =>
{
    var context = new MyAppContext();
    await engine.ExecuteBundleAsync(
        bundle,
        context,
        appendLog: null,
        tracker: tracker,
        executionId: executionId);
});
```

### 6.3 Querying Progress & Status Lifecycle
Call `tracker.GetExecutionAsync(executionId)` at any time to retrieve the current `BundleExecutionState`. Each sequence and rule item moves through well-defined lifecycle states: `Pending` $\rightarrow$ `Starting` $\rightarrow$ `Completed` / `Failed` / `Skipped`.

For complete details, API controller code examples, and event subscription details, refer to [EXECUTION_TRACKING.md](EXECUTION_TRACKING.md).

---

## 7. Categorization & Tagging

`BusinessRule`, `BusinessRuleBundle`, and `DbConnectionDefinition` support multi-category and multi-tag classification via `Categories` (`List<string>`) and `Tags` (`List<string>`).

### 7.1 Model Usage
```csharp
var rule = new BusinessRule
{
    Name = "PCI Compliance Check",
    Categories = new List<string> { "Finance", "Security" },
    Tags = new List<string> { "PCI-DSS", "HighPriority", "Automated" }
};
```

### 7.2 Database Persistence
When persisting rules, bundles, or connections in relational databases (SQL Server, Postgres, MySQL), `Categories` and `Tags` are stored as JSON array strings (e.g. `["Finance","Security"]`).

### 7.3 Store Search & Filtering
`IBusinessRuleStore` includes search extensions for filtering rules and bundles by category or tag:
- `GetRulesByCategoryAsync(category)`
- `GetRulesByTagAsync(tag)`
- `GetBundlesByCategoryAsync(category)`
- `GetBundlesByTagAsync(tag)`


