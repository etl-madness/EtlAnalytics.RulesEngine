# Simple Console Integration: EtlAnalytics.RulesEngine

This example provides a single-file `Program.cs` approach for a console application that can execute either a single Rule or a Rule Bundle based on command-line arguments.

## BusinessRulesEngineExample Project
You can clone or download the full example project from the link below.

[BusinessRulesEngineExample on GitHub](https://github.com/etl-madness/BusinessRulesEngineExample)

## Prerequisites
- `EtlAnalytics.RulesEngine` NuGet
- `EtlAnalytics.RulesEngine.Dapper` NuGet
- `Microsoft.Data.SqlClient` NuGet

## Single File Implementation

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Dapper;
using EtlAnalytics.RulesEngine.Services;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Models;
using EtlAnalytics.RulesEngine.Providers;
using System.Text.Json;

namespace BusinessRulesEngineCLIExample;

// 1. Define your Context
public class MyContext : RuleExecutionContext
{
    /*
    public string UserRole { get; set; } = "Guest";
    public double TransactionAmount
    {
        get; set;
    }
    */
}

// 2. Simple Rule Store using Dapper directly
public class SimpleRuleStore : IBusinessRuleStore
{
    private readonly string _connectionString;
    private readonly IRuleDbProvider _dbProvider;

    public SimpleRuleStore(string connectionString, IEnumerable<IRuleDbProvider> dbProviders)
    {
        _connectionString = connectionString;
        _dbProvider = dbProviders.First(p => p.ProviderType == "SqlServer");
    }

    public async Task<BusinessRule?> GetBusinessRuleByIdAsync(int id)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        return await db.QueryFirstOrDefaultAsync<BusinessRule>(
            "SELECT * FROM BusinessRules WHERE Id = @Id", new
            {
                Id = id
            });
    }

    public async Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        var bundle = await db.QueryFirstOrDefaultAsync<BusinessRuleBundle>(
            "SELECT * FROM BusinessRuleBundles WHERE Name = @Name", new
            {
                Name = name
            });

        if (bundle != null)
        {
            var items = await db.QueryAsync<BusinessRuleBundleItem>(
                "SELECT * FROM BusinessRuleBundleItems WHERE BundleId = @Id ORDER BY SequenceOrder",
                new
                {
                    Id = bundle.Id
                });
            bundle.Items = items.ToList();
        }
        return bundle;
    }

    public async Task<DbConnectionDefinition?> GetDbConnectionByIdAsync(int id)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        return await db.QueryFirstOrDefaultAsync<DbConnectionDefinition>(
            "SELECT * FROM DbConnections WHERE Id = @Id", new
            {
                Id = id
            });
    }

    public async Task<IEnumerable<DbConnectionDefinition>> GetAllDbConnectionsAsync()
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        return await db.QueryAsync<DbConnectionDefinition>("SELECT * FROM DbConnections");
    }
}

// 3. Main Program Logic
public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- <RuleId>      (e.g. dotnet run -- 101)");
            Console.WriteLine("  dotnet run -- <BundleName>  (e.g. dotnet run -- ValidationBundle)");
            return;
        }

        var input = args[0];

        // Setup DI
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        services.AddScoped<ISqlRuleExecutor, DapperSqlRuleExecutor>();
        services.AddScoped<IRuleDbProvider, SqlServerRuleDbProvider>();

        // Register Executors (Pass empty list to use defaults, or add extensions)
        services.AddScoped<IEnumerable<IRuleExecutor>>(sp => Enumerable.Empty<IRuleExecutor>());

        services.AddScoped<BusinessRuleEngine<MyContext>>();

        services.AddScoped<IBusinessRuleStore>(sp =>
            new SimpleRuleStore(
                config.GetConnectionString("DefaultConnection")!,
                sp.GetServices<IRuleDbProvider>()
            ));

        var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<BusinessRuleEngine<MyContext>>();
        var ruleStore = provider.GetRequiredService<IBusinessRuleStore>();

        // Create a sample context
        var context = new MyContext { };

        object? result = null;

        try
        {
            if (int.TryParse(input, out int ruleId))
            {
                Console.WriteLine($"Executing Rule ID: {ruleId}...");
                var rule = await ruleStore.GetBusinessRuleByIdAsync(ruleId)
                    ?? throw new InvalidOperationException($"Rule with ID '{ruleId}' was not found.");
                result = await engine.ExecuteRuleAsync(rule, context, log => Console.WriteLine($"[LOG]: {log}"));
            }
            else
            {
                Console.WriteLine($"Executing Bundle: {input}...");
                var bundle = await ruleStore.GetBusinessRuleBundleByNameAsync(input)
                    ?? throw new InvalidOperationException($"Bundle '{input}' was not found.");
                result = await engine.ExecuteBundleAsync(bundle, context, log => Console.WriteLine($"[LOG]: {log}"));
            }

            Console.WriteLine("\n--- EXECUTION SUCCESS ---");
            Console.WriteLine($"Final Result: {JsonSerializer.Serialize(result)}");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n--- EXECUTION FAILED ---");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```
