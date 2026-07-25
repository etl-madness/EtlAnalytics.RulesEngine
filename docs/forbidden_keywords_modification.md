# Guide: Modifying Forbidden SQL Keywords

This document explains how to manage and modify the list of prohibited SQL keywords in the `EtlAnalytics.RulesEngine`.

## 1. Locating the Keywords
The prohibited keywords are defined in a `static readonly` array within the `BusinessRuleEngine` class.

**File Path**: [BusinessRuleEngine.cs](file:///C:/Users/U00001/source/repos/etl-madness/EtlAnalytics.RulesEngine/Services/BusinessRuleEngine.cs)
**Line Range**: [L29-L34](file:///C:/Users/U00001/source/repos/etl-madness/EtlAnalytics.RulesEngine/Services/BusinessRuleEngine.cs#L29-L34)

```csharp
private static readonly string[] ForbiddenSqlKeywords = 
{ 
    "DROP", "TRUNCATE", "DELETE", "UPDATE", "INSERT", 
    "GRANT", "REVOKE", "ALTER", "CREATE", 
    "xp_cmdshell", "sys.", "information_schema"
};
```

## 2. Adding or Removing Keywords
To modify the list, you must edit the source code and rebuild the library.

### To block a new keyword (e.g., `MERGE`):
Simply add the uppercase string to the array:
```diff
     private static readonly string[] ForbiddenSqlKeywords = 
     { 
         "DROP", "TRUNCATE", "DELETE", "UPDATE", "INSERT", 
-        "GRANT", "REVOKE", "ALTER", "CREATE", "EXEC", "EXECUTE",
+        "GRANT", "REVOKE", "ALTER", "CREATE", "EXEC", "EXECUTE", "MERGE",
         "xp_cmdshell", "sys.", "information_schema"
     };
```

### To allow a blocked keyword (e.g., `INSERT`):
Remove the string from the array. 
> [!CAUTION]
> Removing keywords like `DELETE` or `DROP` significantly increases the risk of data loss or malicious rule execution. Only do this if you have implemented external sandboxing at the database user/permission level.

## 3. Recommended Refactor: Making Keywords Configurable
For NuGet package users, hardcoded lists are restrictive. We recommend refactoring the engine to read from `IConfiguration`.

### Step 1: Update the Constructor
Modify [BusinessRuleEngine.cs](file:///C:/Users/U00001/source/repos/etl-madness/EtlAnalytics.RulesEngine/Services/BusinessRuleEngine.cs) to initialize the keywords from configuration:

```csharp
private readonly string[] _forbiddenKeywords;

public BusinessRuleEngine(IConfiguration configuration, ...)
{
    var configKeywords = configuration.GetSection("Security:ForbiddenSqlKeywords").Get<string[]>();
    _forbiddenKeywords = configKeywords ?? new[] { "DROP", "TRUNCATE", ... }; // Default fallback
}
```

### Step 2: Update `appsettings.json`
Users can then manage the list without changing code:
```json
{
  "Security": {
    "ForbiddenSqlKeywords": ["DROP", "TRUNCATE", "DELETE", "MY_CUSTOM_BLOCKED_WORD"]
  }
}
```

## 4. How the Check Works
The engine performs a case-insensitive check using `ToUpperInvariant()` on the rule code. If any blocked keyword is found as a substring, a `SecurityException` is thrown, and the execution is aborted before the database connection is even opened.
