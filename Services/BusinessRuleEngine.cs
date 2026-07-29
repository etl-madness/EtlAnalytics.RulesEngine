using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using EtlAnalytics.RulesEngine.Models;
using EtlAnalytics.RulesEngine.Interfaces;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Security;

namespace EtlAnalytics.RulesEngine.Services;

/// <summary>
/// A lightweight, database-agnostic business rules engine supporting T-SQL and C# scripting.
/// </summary>
/// <typeparam name="TContext">The type of the execution context, which must inherit from <see cref="RuleExecutionContext"/>.</typeparam>
public class BusinessRuleEngine<TContext> where TContext : RuleExecutionContext
{
    private readonly IBusinessRuleStore _ruleStore;
    private readonly IEnumerable<IRuleExecutor> _executors;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleEngine{TContext}"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="ruleStore">The store to retrieve rules and bundles from.</param>
    /// <param name="sqlExecutor">The executor used for running SQL rules.</param>
    /// <param name="encryptionService">The service used for decrypting sensitive data.</param>
    /// <param name="executors">The collection of rule executors.</param>
    public BusinessRuleEngine(
        IConfiguration configuration,
        IBusinessRuleStore ruleStore,
        ISqlRuleExecutor sqlExecutor,
        IEncryptionService encryptionService,
        IEnumerable<IRuleExecutor> executors)
    {
        _ruleStore = ruleStore;
        
        // Combine provided executors with default ones if they are not already there
        var executorList = executors.ToList();

        // 1. Initialize Default TSQL Executor if not present
        if (!executorList.Any(e => e.RuleType == RuleConstants.TSQL))
        {
            var forbiddenKeywords = LoadForbiddenKeywords(configuration);
            var connectionString = LoadConnectionString(configuration, encryptionService);
            var sqlTimeout = LoadSqlTimeout(configuration);
            executorList.Add(new Executors.TsqlRuleExecutor(configuration, ruleStore, sqlExecutor, encryptionService, connectionString, forbiddenKeywords, sqlTimeout));
        }

        // 2. Initialize Default C# Executor if not present
        if (!executorList.Any(e => e.RuleType == RuleConstants.CSharp))
        {
            var scriptReferences = LoadScriptReferences(configuration);
            var scriptImports = LoadScriptImports(configuration);
            var scriptTimeout = LoadScriptTimeout(configuration);
            executorList.Add(new Executors.CSharpRuleExecutor(scriptReferences, scriptImports, scriptTimeout));
        }

        _executors = executorList;
    }

    private string[] LoadForbiddenKeywords(IConfiguration configuration)
    {
        var configKeywords = configuration.GetSection("RulesEngine:ForbiddenSqlKeywords")
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => x != null)
            .Cast<string>()
            .ToArray();

        return configKeywords.Length > 0 ? configKeywords : new[]
        {
            "DROP", "TRUNCATE", "DELETE", "UPDATE", "INSERT",
            "GRANT", "REVOKE", "ALTER", "CREATE",
            "xp_cmdshell", "sys.", "information_schema"
        };
    }

    private string LoadConnectionString(IConfiguration configuration, IEncryptionService encryptionService)
    {
        var rawConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        try
        {
            return encryptionService.Decrypt(rawConnectionString);
        }
        catch
        {
            return rawConnectionString;
        }
    }

    private int LoadSqlTimeout(IConfiguration configuration)
    {
        var sqlTimeoutStr = configuration["RulesEngine:SqlTimeoutSeconds"]
            ?? configuration["RulesEngine:SqlTimeout"]
            ?? configuration["RulesEngine:CommandTimeout"];
        return int.TryParse(sqlTimeoutStr, out var parsedSqlTimeout) && parsedSqlTimeout > 0
            ? parsedSqlTimeout
            : 30;
    }

    private int LoadScriptTimeout(IConfiguration configuration)
    {
        var scriptTimeoutStr = configuration["RulesEngine:ScriptTimeoutSeconds"]
            ?? configuration["RulesEngine:ScriptTimeout"];
        return int.TryParse(scriptTimeoutStr, out var parsedScriptTimeout) && parsedScriptTimeout > 0
            ? parsedScriptTimeout
            : 10;
    }

    private string[] LoadScriptReferences(IConfiguration configuration)
    {
        return configuration.GetSection("RulesEngine:WithReferences")
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => x != null)
            .Cast<string>()
            .ToArray();
    }

    private string[] LoadScriptImports(IConfiguration configuration)
    {
        var configImports = configuration.GetSection("RulesEngine:WithImports")
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => x != null)
            .Cast<string>()
            .ToArray();

        return configImports.Length > 0 ? configImports : new[]
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Threading.Tasks",
            "System.Xml",
            "System.Xml.Linq",
            "EtlAnalytics.RulesEngine.Models"
        };
    }

    /// <summary>
    /// Executes a single business rule asynchronously.
    /// </summary>
    public async Task<object?> ExecuteRuleAsync(
        BusinessRule rule,
        TContext? globals = null,
        Action<string>? appendLog = null)
    {
        if (globals != null)
        {
            globals.RunBundle = async (name) =>
            {
                var bundle = await _ruleStore.GetBusinessRuleBundleByNameAsync(name);
                if (bundle == null)
                {
                    appendLog?.Invoke($"[WARN] RunBundle: Bundle '{name}' not found.");
                    return null;
                }
                appendLog?.Invoke($"[INFO] Triggering nested bundle: {name}");
                return await ExecuteBundleAsync(bundle, globals, appendLog);
            };
        }

        appendLog?.Invoke($"[INFO] Starting execution of rule: {rule.Name} ({rule.RuleType})");

        try
        {
            var executor = _executors.FirstOrDefault(e => e.RuleType.Equals(rule.RuleType, StringComparison.OrdinalIgnoreCase));
            if (executor == null)
            {
                throw new NotSupportedException($"Rule type '{rule.RuleType}' is not supported. Ensure the appropriate executor is registered.");
            }

            return await executor.ExecuteAsync(rule, globals!, typeof(TContext), appendLog);
        }
        catch (Exception ex)
        {
            appendLog?.Invoke($"[ERR] Execution failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Executes a business rule bundle asynchronously.
    /// </summary>
    public async Task<object?> ExecuteBundleAsync(
        BusinessRuleBundle bundle,
        TContext baseContext,
        Action<string>? appendLog = null)
    {
        appendLog?.Invoke($"[BUNDLE] --- Starting Bundle: {bundle.Name} ---");
        object? lastResult = null;

        var groups = bundle.Items
            .GroupBy(i => i.SequenceOrder)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            int sequenceOrder = group.Key;
            var items = group.ToList();

            // Pipe results from the PREVIOUS group into the current one
            baseContext.PreviousResult = lastResult;

            try
            {
                if (items.Count == 1)
                {
                    var item = items[0];
                    var rule = await _ruleStore.GetBusinessRuleByIdAsync(item.RuleId);
                    if (rule == null)
                    {
                        appendLog?.Invoke($"[ERR] Rule ID {item.RuleId} not found. Skipping.");
                        continue;
                    }

                    appendLog?.Invoke($"[BUNDLE] Step {sequenceOrder}: {rule.Name}");
                    lastResult = await ExecuteRuleAsync(rule, baseContext, appendLog);
                }
                else
                {
                    appendLog?.Invoke($"[BUNDLE] Step {sequenceOrder}: Executing {items.Count} rules in parallel.");

                    var tasks = items.Select(async item =>
                    {
                        var rule = await _ruleStore.GetBusinessRuleByIdAsync(item.RuleId);
                        if (rule == null)
                        {
                            appendLog?.Invoke($"[ERR] Rule ID {item.RuleId} not found.");
                            return null;
                        }
                        return await ExecuteRuleAsync(rule, baseContext, appendLog);
                    });

                    var results = await Task.WhenAll(tasks);
                    lastResult = results.ToList();
                }

                // Store in history
                baseContext.StepResults[sequenceOrder] = lastResult;
            }
            catch (Exception ex)
            {
                appendLog?.Invoke($"[BUNDLE] [FATAL] Step group {sequenceOrder} failed: {ex.Message}. Aborting bundle.");
                break;
            }
        }

        appendLog?.Invoke($"[BUNDLE] --- Bundle Finished: {bundle.Name} ---");
        return lastResult;
    }
}
