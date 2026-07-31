# Complete Integration Example: EtlAnalytics.RulesEngine + Dapper

This guide provides a complete, end-to-end example of how to build a dynamic discount engine using both the core **RulesEngine** and the **Dapper** extension package.

## 1. Required NuGet Packages
Ensure your project file (or `dotnet add package` commands) includes:
- `EtlAnalytics.RulesEngine`
- `EtlAnalytics.RulesEngine.Dapper`
- `Microsoft.Data.SqlClient` (or your preferred DB driver)

---

## 2. The Implementation

### 2.1 The Data Context
The "Context" is the object that rules "see." Properties added here are directly accessible in C# scripts and passed as JSON to SQL rules.

```csharp
using EtlAnalytics.RulesEngine.Models;

public class PizzaAppContext : RuleExecutionContext
{
    public string CustomerName { get; set; } = string.Empty;
    public double OrderTotal { get; set; }
    public string City { get; set; } = string.Empty;
}
```

### 2.2 The Data Service (Example Pattern)
Many developers already have a service for database access. This pattern is recommended to keep your Store logic clean and decoupled from Dapper.

```csharp
// IDataService.cs
public interface IDataService
{
    Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null);
    Task<IEnumerable<T>> QueryListAsync<T>(string sql, object? parameters = null);
}

// DapperDataService.cs
using Dapper;
using EtlAnalytics.RulesEngine.Interfaces;

public class DapperDataService : IDataService
{
    private readonly string _connString;
    private readonly IRuleDbProvider _dbProvider;

    public DapperDataService(string connString, IEnumerable<IRuleDbProvider> providers)
    {
        _connString = connString;
        _dbProvider = providers.First(p => p.ProviderType == "SqlServer");
    }

    public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null)
    {
        using var db = _dbProvider.CreateConnection(_connString);
        return await db.QueryFirstOrDefaultAsync<T>(sql, parameters);
    }

    public async Task<IEnumerable<T>> QueryListAsync<T>(string sql, object? parameters = null)
    {
        using var db = _dbProvider.CreateConnection(_connString);
        return await db.QueryAsync<T>(sql, parameters);
    }
}
```

### 2.3 The Rule Store (Using Data Service)
Notice that this file **no longer needs** `using Dapper;`. It is strictly focused on rule orchestration.

```csharp
// SqlRuleStore.cs
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Models;

public class SqlRuleStore : IBusinessRuleStore
{
    private readonly IDataService _dataService;

    public SqlRuleStore(IDataService dataService)
    {
        _dataService = dataService;
    }

    public Task<BusinessRule?> GetBusinessRuleByIdAsync(int id) =>
        _dataService.QuerySingleAsync<BusinessRule>(
            "SELECT * FROM BusinessRules WHERE Id = @Id", new { Id = id });

    public async Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name)
    {
        var bundle = await _dataService.QuerySingleAsync<BusinessRuleBundle>(
            "SELECT * FROM BusinessRuleBundles WHERE Name = @Name", new { Name = name });

        if (bundle != null)
        {
            var items = await _dataService.QueryListAsync<BusinessRuleBundleItem>(
                "SELECT * FROM BusinessRuleBundleItems WHERE BundleId = @Id ORDER BY SequenceOrder",
                new { Id = bundle.Id });
            // Note: Rules with the same SequenceOrder will be executed in parallel by the engine
            bundle.Items = items.ToList();
        }
        return bundle;
    }

    public async Task<IEnumerable<BusinessRule>> GetRulesByCategoryAsync(string category)
    {
        var rules = await _dataService.QueryListAsync<BusinessRule>("SELECT * FROM BusinessRules WHERE IsActive = 1");
        return rules.Where(r => r.Categories.Contains(category, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<BusinessRule>> GetRulesByTagAsync(string tag)
    {
        var rules = await _dataService.QueryListAsync<BusinessRule>("SELECT * FROM BusinessRules WHERE IsActive = 1");
        return rules.Where(r => r.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<BusinessRuleBundle>> GetBundlesByCategoryAsync(string category)
    {
        var bundles = await _dataService.QueryListAsync<BusinessRuleBundle>("SELECT * FROM BusinessRuleBundles WHERE IsActive = 1");
        return bundles.Where(b => b.Categories.Contains(category, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<BusinessRuleBundle>> GetBundlesByTagAsync(string tag)
    {
        var bundles = await _dataService.QueryListAsync<BusinessRuleBundle>("SELECT * FROM BusinessRuleBundles WHERE IsActive = 1");
        return bundles.Where(b => b.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    public Task<DbConnectionDefinition?> GetDbConnectionByIdAsync(int id) =>
        _dataService.QuerySingleAsync<DbConnectionDefinition>(
            "SELECT * FROM DbConnections WHERE Id = @Id", new { Id = id });

    public Task<IEnumerable<DbConnectionDefinition>> GetAllDbConnectionsAsync() =>
        _dataService.QueryListAsync<DbConnectionDefinition>("SELECT * FROM DbConnections");
}
```

### 2.4 Dependency Injection & Execution
Wire the Data Service, Execution Tracker, and Rule Store into your container.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using EtlAnalytics.RulesEngine.Services;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Providers;

var services = new ServiceCollection();

// 1. Setup Configuration
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> {
        { "ConnectionStrings:DefaultConnection", "Server=.;Database=RulesDb;Trusted_Connection=True;" },
        { "Security:EncryptionKey", "SuperSecretKey123!" }
    })
    .Build();

services.AddSingleton<IConfiguration>(config);

// 2. Register Rules Engine (Core) & Execution Tracking
services.AddSingleton<IEncryptionService, AesEncryptionService>();
services.AddBusinessRulesEngineTracking();

// 3. Register Dapper Executor & DB Providers (Dapper Package)
services.AddScoped<ISqlRuleExecutor, DapperSqlRuleExecutor>();
services.AddScoped<IRuleDbProvider, SqlServerRuleDbProvider>();

// 4. Register Executors (Default TSQL and CSharp are auto-added by engine if not registered)
// But we can add extensions like Javascript here:
// services.AddJavascriptRules(); 

services.AddScoped<BusinessRuleEngine<PizzaAppContext>>();

// 4. Register the Data Service and Rule Store
services.AddScoped<IDataService>(sp => 
    new DapperDataService(
        config.GetConnectionString("DefaultConnection")!, 
        sp.GetServices<IRuleDbProvider>()
    ));

services.AddScoped<IBusinessRuleStore, SqlRuleStore>();

var provider = services.BuildServiceProvider();

// --- EXECUTION ---

var engine = provider.GetRequiredService<BusinessRuleEngine<PizzaAppContext>>();

var context = new PizzaAppContext { 
    CustomerName = "Alice", 
    OrderTotal = 150.0, 
    City = "New York" 
};

Console.WriteLine("Running Discount Workflow...");

var result = await engine.ExecuteBundleAsync("DiscountBundle", context, log => {
    Console.WriteLine($"[RULE ENGINE]: {log}");
});

Console.WriteLine($"Final Discount Applied: {result}");
```

---

## 3. Sample Bundle Definition

Here is how you would define a 2-step bundle in your database to use with the example above.

### Step 1: "Is VIP?" (C# Rule)
Checks the context directly.
```csharp
if (OrderTotal > 100) {
    Log("High value order detected.");
    return true; 
}
return false;
```

### Step 2: "Get Discount" (T-SQL Rule)
Uses the result from Step 1 (`@PreviousResultJson`) to find a matching coupon.
```sql
-- @PreviousResultJson contains '[true]' or '[false]'
SELECT TOP 1 DiscountPercent 
FROM dbo.Coupons 
CROSS APPLY OPENJSON(@PreviousResultJson) WITH (IsVip BIT '$') p
WHERE IsActive = 1 
  AND IsVipOnly = p.IsVip
  AND City = @City;
```
