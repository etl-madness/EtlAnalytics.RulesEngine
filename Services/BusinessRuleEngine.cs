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
    private readonly IEncryptionService _encryptionService;
    private readonly string _connectionString;
    private readonly IBusinessRuleStore _ruleStore;
    private readonly ISqlRuleExecutor _sqlExecutor;
    private const int DefaultScriptTimeoutSeconds = 10;
    private const int DefaultSqlTimeoutSeconds = 30;

    private static readonly string[] DefaultForbiddenSqlKeywords =
    {
        "DROP", "TRUNCATE", "DELETE", "UPDATE", "INSERT",
        "GRANT", "REVOKE", "ALTER", "CREATE",
        "xp_cmdshell", "sys.", "information_schema"
    };

    private readonly string[] _forbiddenKeywords;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleEngine{TContext}"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="ruleStore">The store to retrieve rules and bundles from.</param>
    /// <param name="sqlExecutor">The executor used for running SQL rules.</param>
    /// <param name="encryptionService">The service used for decrypting sensitive data.</param>
    public BusinessRuleEngine(
        IConfiguration configuration,
        IBusinessRuleStore ruleStore,
        ISqlRuleExecutor sqlExecutor,
        IEncryptionService encryptionService)
    {
        _ruleStore = ruleStore;
        _sqlExecutor = sqlExecutor;
        _encryptionService = encryptionService;

        // 1. Load Forbidden Keywords
        var configKeywords = configuration.GetSection("RulesEngine:ForbiddenSqlKeywords")
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => x != null)
            .Cast<string>()
            .ToArray();
            
        _forbiddenKeywords = configKeywords.Length > 0 ? configKeywords : DefaultForbiddenSqlKeywords;

        // 2. Load Connection String
        var rawConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Assume default connection string might be encrypted as well
        try
        {
            _connectionString = _encryptionService.Decrypt(rawConnectionString);
        }
        catch
        {
            // If decryption fails, assume it's plain text (fallback for legacy or dev)
            _connectionString = rawConnectionString;
        }
    }

    /// <summary>
    /// Executes a single business rule asynchronously.
    /// </summary>
    /// <param name="rule">The rule definition to execute.</param>
    /// <param name="globals">The execution context containing variables and services for the rule.</param>
    /// <param name="appendLog">Optional callback for logging execution details.</param>
    /// <returns>The result of the rule execution.</returns>
    /// <exception cref="NotSupportedException">Thrown when the rule type is not supported.</exception>
    /// <exception cref="SecurityException">Thrown when a security violation is detected (e.g., forbidden SQL keywords).</exception>
    /// <exception cref="TimeoutException">Thrown when a C# script execution times out.</exception>
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
            if (rule.RuleType == RuleType.TSQL)
            {
                return await ExecuteTsqlAsync(rule, globals, appendLog);
            }
            else if (rule.RuleType == RuleType.CSharp)
            {
                return await ExecuteCSharpAsync(rule.Code, globals, appendLog);
            }
            else
            {
                throw new NotSupportedException($"Rule type {rule.RuleType} is not supported.");
            }
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
    /// <param name="bundle">The bundle definition containing rules to execute.</param>
    /// <param name="baseContext">The base execution context.</param>
    /// <param name="appendLog">Optional callback for logging execution details.</param>
    /// <returns>The result of the last rule executed in the bundle.</returns>
    public async Task<object?> ExecuteBundleAsync(
        BusinessRuleBundle bundle,
        TContext baseContext,
        Action<string>? appendLog = null)
    {
        appendLog?.Invoke($"[BUNDLE] --- Starting Bundle: {bundle.Name} ---");
        object? lastResult = null;

        foreach (var item in bundle.Items.OrderBy(i => i.SequenceOrder))
        {
            var rule = await _ruleStore.GetBusinessRuleByIdAsync(item.RuleId);
            if (rule == null)
            {
                appendLog?.Invoke($"[ERR] Rule ID {item.RuleId} not found. Skipping.");
                continue;
            }

            appendLog?.Invoke($"[BUNDLE] Step {item.SequenceOrder}: {rule.Name}");

            // Pipe results
            baseContext.PreviousResult = lastResult;

            try
            {
                lastResult = await ExecuteRuleAsync(rule, baseContext, appendLog);
                // Store in history
                baseContext.StepResults[item.SequenceOrder] = lastResult;
            }
            catch (Exception ex)
            {
                appendLog?.Invoke($"[BUNDLE] [FATAL] Step failed: {ex.Message}. Aborting bundle.");
                break;
            }
        }

        appendLog?.Invoke($"[BUNDLE] --- Bundle Finished: {bundle.Name} ---");
        return lastResult;
    }

    private async Task<object?> ExecuteTsqlAsync(BusinessRule rule, TContext? context, Action<string>? appendLog)
    {
        string connectionString = _connectionString;
        string providerType = "SqlServer"; // Default fallback

        if (rule.ConnectionId.HasValue)
        {
            var dbConn = await _ruleStore.GetDbConnectionByIdAsync(rule.ConnectionId.Value);
            if (dbConn != null)
            {
                appendLog?.Invoke($"[SQL] Using specific connection: {dbConn.Name} ({dbConn.ProviderType})");

                string decryptedConn;
                try
                {
                    decryptedConn = _encryptionService.Decrypt(dbConn.ConnectionString);
                }
                catch (Exception ex)
                {
                    appendLog?.Invoke($"[WARN] Failed to decrypt connection string for {dbConn.Name}. Error: {ex.Message}. Attempting to use as-is.");
                    decryptedConn = dbConn.ConnectionString;
                }

                connectionString = decryptedConn;
                providerType = dbConn.ProviderType;
            }
            else
            {
                appendLog?.Invoke($"[WARN] Connection ID {rule.ConnectionId} not found. Falling back to default connection.");
            }
        }

        appendLog?.Invoke("[SQL] Executing T-SQL script...");

        // Basic Query Validation
        var upperCode = rule.Code.ToUpperInvariant();
        foreach (var keyword in _forbiddenKeywords)
        {
            if (upperCode.Contains(keyword.ToUpperInvariant()))
            {
                appendLog?.Invoke($"[SECURITY ALERT] T-SQL rule '{rule.Name}' contains forbidden keyword: {keyword}. Execution blocked.");
                throw new SecurityException($"Forbidden SQL keyword detected: {keyword}");
            }
        }

        var parameters = new Dictionary<string, object>();
        var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        string previousJson = context != null ? JsonSerializer.Serialize(context.PreviousResult, jsonOptions) : "[]";
        string stepResultsJson = context != null ? JsonSerializer.Serialize(context.StepResults, jsonOptions) : "{}";

        parameters.Add("PreviousResultJson", previousJson);
        parameters.Add("StepResultsJson", stepResultsJson);

        var results = await _sqlExecutor.ExecuteAsync(
            rule.Code,
            parameters,
            connectionString,
            providerType,
            DefaultSqlTimeoutSeconds,
            context?.CancellationToken ?? CancellationToken.None);

        var resultList = results.ToList();

        appendLog?.Invoke($"[SQL] Execution completed. {resultList.Count} rows returned.");
        return resultList;
    }

    private async Task<object?> ExecuteCSharpAsync(string code, TContext? globals, Action<string>? appendLog)
    {
        appendLog?.Invoke("[CS] Compiling and executing C# script...");

        // Define a restricted set of allowed assemblies and namespaces
        var options = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly, // mscorlib / System.Runtime
                typeof(System.Linq.Enumerable).Assembly,
                typeof(System.Collections.Generic.List<>).Assembly,
                typeof(RuleExecutionContext).Assembly
            )
            .WithImports(
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "System.Threading.Tasks",
                "EtlAnalytics.RulesEngine.Models"
            );

        // Add reference to the assembly containing TContext if it's different and not already added
        if (typeof(TContext).Assembly != typeof(RuleExecutionContext).Assembly)
        {
            options = options.AddReferences(typeof(TContext).Assembly);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultScriptTimeoutSeconds));
        if (globals != null)
        {
            globals.CancellationToken = cts.Token;
        }

        try
        {
            // Evaluate script with timeout and restricted options
            var result = await CSharpScript.EvaluateAsync(code, options, globals, typeof(TContext), cts.Token);

            if (cts.Token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cts.Token);
            }

            appendLog?.Invoke("[CS] Execution completed successfully.");
            return result;
        }
        catch (OperationCanceledException)
        {
            appendLog?.Invoke($"[ERR] Script execution timed out after {DefaultScriptTimeoutSeconds} seconds.");
            throw new TimeoutException($"The C# script exceeded the maximum execution time of {DefaultScriptTimeoutSeconds} seconds.");
        }
        catch (CompilationErrorException ex)
        {
            appendLog?.Invoke($"[ERR] Compilation Error: {string.Join(Environment.NewLine, ex.Diagnostics)}");
            throw;
        }
    }
}
