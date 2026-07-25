# AI Implementation Guide: EtlAnalytics.RulesEngine

This document serves as a structured technical reference for AI agents to integrate, extend, and develop rules using the `EtlAnalytics.RulesEngine` NuGet package.

## 1. Core Architecture

The library is a database-agnostic business rules engine that supports a pluggable execution architecture. By default, it supports **TSQL** and **C# Scripting**, but it can be extended to support any language (e.g., **Javascript**) via external packages. It follows a standard Dependency Injection (DI) pattern and is decoupled from specific database execution libraries (like Dapper). It targets both **.NET 8** and **.NET 10**.

### Primary Service
- **`BusinessRuleEngine<TContext>`**: The orchestrator. `TContext` must inherit from <see cref="RuleExecutionContext"/>.

### Required Interfaces
Implementing these is mandatory for integration:
- **`IBusinessRuleStore`**: Handles retrieval of `BusinessRule` and `BusinessRuleBundle` objects.
- **`IRuleExecutor`**: The core interface for extending the engine. Implement this to add support for new rule types (e.g., Python, Javascript).
- **`ISqlRuleExecutor`**: Abstracts the actual SQL execution (used by the default TSQL executor).
- **`IRuleDbProvider`**: Provides `IDbConnection` instances (used by implementations of `ISqlRuleExecutor`).
- **`IEncryptionService`**: Handles AES-256 encryption/decryption of connection strings.

## 2. Model Definitions

### `BusinessRule`
- `RuleType`: A string constant. Built-in types are available in `RuleConstants`: `TSQL`, `CSharp`. Extensions add others like `Javascript`.
- `Code`: The raw SQL query or C# script.
- `ConnectionId`: Links to a `DbConnectionDefinition`. The engine resolves the correct provider and connection string before calling the `ISqlRuleExecutor`.

### `RuleExecutionContext` (Base Class)
Agents should inherit from this to pass custom data to rules.
- `PreviousResult`: Result from the last rule in a bundle.
- `StepResults`: Dictionary of all previous results in a bundle (`sequenceOrder` -> `result`).
- `CancellationToken`: Used to signal timeouts (10s for C#, 30s for SQL).

## 3. Data Passing in Bundles (Sequence Execution)

The engine supports sequential execution of rules within a `BusinessRuleBundle`. Data is passed between steps automatically via the context.

### C# to C# Data Passing
Rules can read `globals.PreviousResult` or look up specific results in `globals.StepResults`.

### SQL to C# Data Passing
SQL results are returned as `IEnumerable<dynamic>`. C# rules can then process this data.

### C# to SQL Data Passing
The engine passes `PreviousResultJson` and `StepResultsJson` as parameters into every SQL execution.

## 4. Multi-Database Support

The engine passes the `ProviderType` (e.g., "SqlServer", "Postgres") to the `ISqlRuleExecutor`. Implementations of the executor use this to resolve the correct `IRuleDbProvider`.

## 5. Security & Sandboxing (CRITICAL)

AI agents generating rules must adhere to these constraints to avoid execution errors.

### C# Scripting Restrictions
- **Default Timeout**: **10 seconds**.
- **Override Keys**: `RulesEngine:ScriptTimeoutSeconds` (preferred), `RulesEngine:ScriptTimeout` (fallback).
- **Namespace Whitelist**:
  - `System`, `System.Linq`, `System.Collections.Generic`
  - `System.Text`, `System.Threading.Tasks`
- **Forbidden**: `System.IO`, `System.Net`, `System.Diagnostics`, `System.Reflection`.

### T-SQL Sandboxing
- **Default Timeout**: **30 seconds**.
- **Override Keys**: `RulesEngine:SqlTimeoutSeconds` (preferred), `RulesEngine:SqlTimeout` (fallback), `RulesEngine:CommandTimeout` (fallback).
- **Forbidden SQL Keywords (Default)**: `DROP`, `TRUNCATE`, `DELETE`, `UPDATE`, `INSERT`, `GRANT`, `REVOKE`, `CREATE`, `ALTER`, `xp_cmdshell`, `sys.`, `information_schema`.
- **Override Keywords**: Set `RulesEngine:ForbiddenSqlKeywords` to replace the default list.
- **Reference**: For full details on forbidden keywords and C# script sandbox customization, see [forbidden_keywords_modification.md](forbidden_keywords_modification.md).

### Security and Timeout Override Example
The engine reads values from `IConfiguration` at startup. If the configured timeout value is missing, invalid, or `<= 0`, the engine falls back to defaults (10s C#, 30s SQL).

```json
{
  "RulesEngine": {
    "SqlTimeoutSeconds": 60,
    "ScriptTimeoutSeconds": 15,
    "ForbiddenSqlKeywords": [
      "DROP",
      "TRUNCATE",
      "DELETE",
      "UPDATE",
      "INSERT",
      "ALTER",
      "CREATE",
      "xp_cmdshell",
      "sys.",
      "information_schema"
    ]
  }
}
```

## 6. Integration Guide

### Dependency Injection Setup
To use the engine, you must register a SQL executor (e.g., one using Dapper).

```csharp
services.AddSingleton<IEncryptionService, AesEncryptionService>();
services.AddScoped<IBusinessRuleStore, MySqlRuleStore>();
services.AddScoped<IRuleDbProvider, SqlServerRuleDbProvider>();

// Register the SQL Executor (via Core or Dapper extension)
services.AddScoped<ISqlRuleExecutor, DapperSqlRuleExecutor>();

// Register any other executors (extensions)
services.AddJavascriptRules(); 

services.AddScoped<BusinessRuleEngine<MyCustomContext>>();
```

### Execution Flow
1. Retrieve Rule/Bundle from Store.
2. Initialize `TContext`.
3. Call `ExecuteRuleAsync` or `ExecuteBundleAsync`.
4. Engine decrypts connection strings and prepares parameters.
5. Engine calls `ISqlRuleExecutor.ExecuteAsync` for SQL rules.

## 7. C# Rule Development Patterns
(Unchanged from previous versions)

## 8. T-SQL Rule Development Patterns
SQL rules are executed via the registered `ISqlRuleExecutor`. By default, this supports Dapper-style parameter binding (`@PreviousResultJson`).

## 9. Configuration Keys
The `AesEncryptionService` expects:
- Environment Variable: `DB_ENCRYPTION_KEY`
- OR Configuration: `Security:EncryptionKey`

The `BusinessRuleEngine` also supports:
- `RulesEngine:SqlTimeoutSeconds` (or `RulesEngine:SqlTimeout`, `RulesEngine:CommandTimeout`)
- `RulesEngine:ScriptTimeoutSeconds` (or `RulesEngine:ScriptTimeout`)
- `RulesEngine:ForbiddenSqlKeywords`
- `RulesEngine:WithReferences`, `RulesEngine:WithImports`
