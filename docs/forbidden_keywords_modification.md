# Guide: Modifying Forbidden SQL Keywords & C# Script Sandboxing

This document explains how to manage and modify the list of prohibited SQL keywords as well as C# script references and imports in `EtlAnalytics.RulesEngine`.

Important: keyword sandboxing is not a replacement for application-level authorization. CRUD and execution permissions should be enforced by the consuming application's RBAC/group/ACL policy layer.

See `RBAC.md` for recommended policy flow.

## 1. Default Security Configuration
The engine defines default forbidden keywords for SQL rules, and default assembly references and imports for C# rules within its internal logic.

**File Path**: [BusinessRuleEngine.cs](https://github.com/etl-madness/EtlAnalytics.RulesEngine/blob/master/Services/BusinessRuleEngine.cs)

## 2. Configuring Keywords via `appsettings.json`
`BusinessRuleEngine` reads custom security keywords from your application's `IConfiguration` under the `RulesEngine:ForbiddenSqlKeywords` section. Configuring this section will override the default keyword blacklist without requiring code changes or recompilation.

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

> [!CAUTION]
> Removing default keywords like `DELETE` or `DROP` significantly increases the risk of data loss or malicious rule execution. Only do this if you have implemented external sandboxing at the database user/permission level.

## 3. Configuring C# Script References & Imports
Similarly, C# scripting references (`WithReferences`) and namespace imports (`WithImports`) can be customized via `IConfiguration`:

```json
{
  "RulesEngine": {
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
> Configured lists will override the default references (`System.Runtime`, `System.Linq`, `System.Collections`, `EtlAnalytics.RulesEngine`) and default imports (`System`, `System.Collections.Generic`, `System.Linq`, `System.Text`, `System.Threading.Tasks`, `EtlAnalytics.RulesEngine.Models`).

## 4. How the Security Check Works
For T-SQL rules, the engine performs a case-insensitive check using `ToUpperInvariant()` on the rule code. If any forbidden keyword is detected as a substring, a `SecurityException` is thrown and execution is aborted before the database query is dispatched.
