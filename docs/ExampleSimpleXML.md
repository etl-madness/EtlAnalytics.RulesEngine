# Simple Console Integration (XML Rule Store): EtlAnalytics.RulesEngine

This example demonstrates how to implement an **XML-backed Rule Store** (`XmlRuleStore`) for `EtlAnalytics.RulesEngine`. Instead of storing rules and bundles in a SQL database, rules are loaded dynamically from an XML configuration file (`rules.xml`).

## Prerequisites
- `EtlAnalytics.RulesEngine` NuGet
- `EtlAnalytics.RulesEngine.Dapper` NuGet *(if executing T-SQL rules)*
- Built-in .NET `System.Xml.Linq` library

---

## 1. Sample XML File (`rules.xml`)

Place this `rules.xml` file in your application directory:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RulesEngineData>
  <Rules>
    <BusinessRule>
      <Id>101</Id>
      <Name>CheckOrderValue</Name>
      <RuleType>CSharp</RuleType>
      <Code><![CDATA[
        var total = PreviousResult != null ? (double)PreviousResult : 150.0;
        return total > 100.0 ? "HighValueOrder" : "StandardOrder";
      ]]></Code>
      <Categories>
        <Category>Validation</Category>
        <Category>Finance</Category>
      </Categories>
      <Tags>
        <Tag>OrderValue</Tag>
        <Tag>PCI-DSS</Tag>
      </Tags>
    </BusinessRule>
    <BusinessRule>
      <Id>102</Id>
      <Name>FetchHighRiskCustomers</Name>
      <RuleType>TSQL</RuleType>
      <Code>SELECT CustomerId, TotalAmount FROM Orders WHERE TotalAmount > 500</Code>
      <ConnectionId>1</ConnectionId>
      <Categories>
        <Category>Security</Category>
      </Categories>
      <Tags>
        <Tag>HighRisk</Tag>
      </Tags>
    </BusinessRule>
  </Rules>

  <RuleBundles>
    <BusinessRuleBundle>
      <Id>1</Id>
      <Name>ValidationBundle</Name>
      <Categories>
        <Category>Validation</Category>
      </Categories>
      <Tags>
        <Tag>Nightly</Tag>
      </Tags>
      <Items>
        <BusinessRuleBundleItem>
          <BundleId>1</BundleId>
          <RuleId>101</RuleId>
          <SequenceOrder>1</SequenceOrder>
        </BusinessRuleBundleItem>
        <BusinessRuleBundleItem>
          <BundleId>1</BundleId>
          <RuleId>102</RuleId>
          <SequenceOrder>2</SequenceOrder>
        </BusinessRuleBundleItem>
      </Items>
    </BusinessRuleBundle>
  </RuleBundles>

  <!-- Rules with the same SequenceOrder will be executed in parallel -->

  <DbConnections>
    <DbConnectionDefinition>
      <Id>1</Id>
      <Name>DefaultConnection</Name>
      <ProviderType>SqlServer</ProviderType>
      <ConnectionString>Server=.;Database=RulesDb;Trusted_Connection=True;</ConnectionString>
      <Categories>
        <Category>Production</Category>
      </Categories>
      <Tags>
        <Tag>Encrypted</Tag>
      </Tags>
    </DbConnectionDefinition>
  </DbConnections>
</RulesEngineData>
```

---

## 2. Single File Implementation (`Program.cs`)

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using EtlAnalytics.RulesEngine.Services;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Models;
using EtlAnalytics.RulesEngine.Providers;

namespace BusinessRulesEngineXmlExample;

// 1. Define your Custom Execution Context
public class MyContext : RuleExecutionContext
{
}

// 2. XML-Backed Rule Store implementing IBusinessRuleStore
public class XmlRuleStore : IBusinessRuleStore
{
    private readonly string _xmlFilePath;

    public XmlRuleStore(string xmlFilePath)
    {
        _xmlFilePath = xmlFilePath;
    }

    private XDocument LoadXml()
    {
        if (!File.Exists(_xmlFilePath))
        {
            throw new FileNotFoundException($"Rules XML file not found at: {_xmlFilePath}");
        }
        return XDocument.Load(_xmlFilePath);
    }

    private static List<string> ParseXmlList(XElement? parentElement, string elementName, string childName)
    {
        if (parentElement == null) return new List<string>();
        return parentElement.Element(elementName)?
            .Elements(childName)
            .Select(e => e.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList() ?? new List<string>();
    }

    public Task<BusinessRule?> GetBusinessRuleByIdAsync(int id)
    {
        var doc = LoadXml();
        var element = doc.Descendants("BusinessRule")
            .FirstOrDefault(e => (int?)e.Element("Id") == id);

        if (element == null) return Task.FromResult<BusinessRule?>(null);

        int? connectionId = (int?)element.Element("ConnectionId");

        var rule = new BusinessRule
        {
            Id = (int)element.Element("Id")!,
            Name = (string)element.Element("Name")!,
            RuleType = (string)element.Element("RuleType")!,
            Code = (string)element.Element("Code")!,
            ConnectionId = connectionId,
            Categories = ParseXmlList(element, "Categories", "Category"),
            Tags = ParseXmlList(element, "Tags", "Tag")
        };

        return Task.FromResult<BusinessRule?>(rule);
    }

    public Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name)
    {
        var doc = LoadXml();
        var bundleElement = doc.Descendants("BusinessRuleBundle")
            .FirstOrDefault(e => (string?)e.Element("Name") == name);

        if (bundleElement == null) return Task.FromResult<BusinessRuleBundle?>(null);

        var bundle = new BusinessRuleBundle
        {
            Id = (int)bundleElement.Element("Id")!,
            Name = (string)bundleElement.Element("Name")!,
            Categories = ParseXmlList(bundleElement, "Categories", "Category"),
            Tags = ParseXmlList(bundleElement, "Tags", "Tag"),
            Items = new List<BusinessRuleBundleItem>()
        };

        var items = bundleElement.Descendants("BusinessRuleBundleItem")
            .Select(i => new BusinessRuleBundleItem
            {
                BundleId = (int)i.Element("BundleId")!,
                RuleId = (int)i.Element("RuleId")!,
                SequenceOrder = (int)i.Element("SequenceOrder")!
            })
            .OrderBy(i => i.SequenceOrder)
            .ToList();

        bundle.Items = items;
        return Task.FromResult<BusinessRuleBundle?>(bundle);
    }

    public Task<IEnumerable<BusinessRule>> GetRulesByCategoryAsync(string category)
    {
        var doc = LoadXml();
        var rules = doc.Descendants("BusinessRule")
            .Select(e => new BusinessRule
            {
                Id = (int)e.Element("Id")!,
                Name = (string)e.Element("Name")!,
                RuleType = (string)e.Element("RuleType")!,
                Code = (string)e.Element("Code")!,
                Categories = ParseXmlList(e, "Categories", "Category"),
                Tags = ParseXmlList(e, "Tags", "Tag")
            })
            .Where(r => r.Categories.Contains(category, StringComparer.OrdinalIgnoreCase));

        return Task.FromResult<IEnumerable<BusinessRule>>(rules);
    }

    public Task<IEnumerable<BusinessRule>> GetRulesByTagAsync(string tag)
    {
        var doc = LoadXml();
        var rules = doc.Descendants("BusinessRule")
            .Select(e => new BusinessRule
            {
                Id = (int)e.Element("Id")!,
                Name = (string)e.Element("Name")!,
                RuleType = (string)e.Element("RuleType")!,
                Code = (string)e.Element("Code")!,
                Categories = ParseXmlList(e, "Categories", "Category"),
                Tags = ParseXmlList(e, "Tags", "Tag")
            })
            .Where(r => r.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

        return Task.FromResult<IEnumerable<BusinessRule>>(rules);
    }

    public Task<IEnumerable<BusinessRuleBundle>> GetBundlesByCategoryAsync(string category)
    {
        var doc = LoadXml();
        var bundles = doc.Descendants("BusinessRuleBundle")
            .Select(b => new BusinessRuleBundle
            {
                Id = (int)b.Element("Id")!,
                Name = (string)b.Element("Name")!,
                Categories = ParseXmlList(b, "Categories", "Category"),
                Tags = ParseXmlList(b, "Tags", "Tag")
            })
            .Where(b => b.Categories.Contains(category, StringComparer.OrdinalIgnoreCase));

        return Task.FromResult<IEnumerable<BusinessRuleBundle>>(bundles);
    }

    public Task<IEnumerable<BusinessRuleBundle>> GetBundlesByTagAsync(string tag)
    {
        var doc = LoadXml();
        var bundles = doc.Descendants("BusinessRuleBundle")
            .Select(b => new BusinessRuleBundle
            {
                Id = (int)b.Element("Id")!,
                Name = (string)b.Element("Name")!,
                Categories = ParseXmlList(b, "Categories", "Category"),
                Tags = ParseXmlList(b, "Tags", "Tag")
            })
            .Where(b => b.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

        return Task.FromResult<IEnumerable<BusinessRuleBundle>>(bundles);
    }

    public Task<DbConnectionDefinition?> GetDbConnectionByIdAsync(int id)
    {
        var doc = LoadXml();
        var element = doc.Descendants("DbConnectionDefinition")
            .FirstOrDefault(e => (int?)e.Element("Id") == id);

        if (element == null) return Task.FromResult<DbConnectionDefinition?>(null);

        var connection = new DbConnectionDefinition
        {
            Id = (int)element.Element("Id")!,
            Name = (string)element.Element("Name")!,
            ProviderType = (string)element.Element("ProviderType")!,
            ConnectionString = (string)element.Element("ConnectionString")!,
            Categories = ParseXmlList(element, "Categories", "Category"),
            Tags = ParseXmlList(element, "Tags", "Tag")
        };

        return Task.FromResult<DbConnectionDefinition?>(connection);
    }

    public Task<IEnumerable<DbConnectionDefinition>> GetAllDbConnectionsAsync()
    {
        var doc = LoadXml();
        var connections = doc.Descendants("DbConnectionDefinition")
            .Select(e => new DbConnectionDefinition
            {
                Id = (int)e.Element("Id")!,
                Name = (string)e.Element("Name")!,
                ProviderType = (string)e.Element("ProviderType")!,
                ConnectionString = (string)e.Element("ConnectionString")!,
                Categories = ParseXmlList(e, "Categories", "Category"),
                Tags = ParseXmlList(e, "Tags", "Tag")
            })
            .ToList();

        return Task.FromResult<IEnumerable<DbConnectionDefinition>>(connections);
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
        string xmlFilePath = Path.Combine(AppContext.BaseDirectory, "rules.xml");

        // Setup Dependency Injection
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        services.AddBusinessRulesEngineTracking();
        services.AddScoped<ISqlRuleExecutor, DapperSqlRuleExecutor>();
        services.AddScoped<IRuleDbProvider, SqlServerRuleDbProvider>();
        
        // Register Executors (Pass empty list to use defaults, or add extensions)
        services.AddScoped<IEnumerable<IRuleExecutor>>(sp => Enumerable.Empty<IRuleExecutor>());

        services.AddScoped<BusinessRuleEngine<MyContext>>();

        // Register XML Rule Store
        services.AddScoped<IBusinessRuleStore>(sp => new XmlRuleStore(xmlFilePath));

        var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<BusinessRuleEngine<MyContext>>();
        var ruleStore = provider.GetRequiredService<IBusinessRuleStore>();

        var context = new MyContext();
        object? result = null;

        try
        {
            if (int.TryParse(input, out int ruleId))
            {
                Console.WriteLine($"[XML STORE] Loading Rule ID: {ruleId}...");
                var rule = await ruleStore.GetBusinessRuleByIdAsync(ruleId)
                    ?? throw new InvalidOperationException($"Rule with ID '{ruleId}' not found in {xmlFilePath}.");
                result = await engine.ExecuteRuleAsync(rule, context, log => Console.WriteLine($"[LOG]: {log}"));
            }
            else
            {
                Console.WriteLine($"[XML STORE] Loading Bundle: {input}...");
                var bundle = await ruleStore.GetBusinessRuleBundleByNameAsync(input)
                    ?? throw new InvalidOperationException($"Bundle '{input}' not found in {xmlFilePath}.");
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
