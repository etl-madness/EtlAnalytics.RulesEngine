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
    private readonly IRuleAuthorizationService? _authorizationService;
    private readonly bool _authorizationFailClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleEngine{TContext}"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="ruleStore">The store to retrieve rules and bundles from.</param>
    /// <param name="sqlExecutor">The executor used for running SQL rules.</param>
    /// <param name="encryptionService">The service used for decrypting sensitive data.</param>
    /// <param name="executors">The collection of rule executors.</param>
    /// <param name="authorizationService">Optional host-provided authorization policy evaluator.</param>
    public BusinessRuleEngine(
        IConfiguration configuration,
        IBusinessRuleStore ruleStore,
        ISqlRuleExecutor sqlExecutor,
        IEncryptionService encryptionService,
        IEnumerable<IRuleExecutor> executors,
        IRuleAuthorizationService? authorizationService = null)
    {
        _ruleStore = ruleStore;
        _authorizationService = authorizationService;
        _authorizationFailClosed = LoadAuthorizationFailClosed(configuration);

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

    private bool LoadAuthorizationFailClosed(IConfiguration configuration)
    {
        var failClosedStr = configuration["RulesEngine:Authorization:FailClosed"]
            ?? configuration["RulesEngine:Authorization:RequirePolicyService"]
            ?? configuration["RulesEngine:RequireAuthorizationService"];

        return bool.TryParse(failClosedStr, out var parsed) && parsed;
    }

    /// <summary>
    /// Executes a single business rule asynchronously.
    /// </summary>
    public async Task<object?> ExecuteRuleAsync(
        BusinessRule rule,
        TContext? globals = null,
        Action<string>? appendLog = null)
    {
        return await ExecuteRuleAsync(rule, globals, appendLog, authorizeAsync: null);
    }

    /// <summary>
    /// Executes a single business rule asynchronously with optional application-side authorization callback.
    /// </summary>
    public async Task<object?> ExecuteRuleAsync(
        BusinessRule rule,
        TContext? globals,
        Action<string>? appendLog,
        Func<AuthorizationRequest, Task<bool>>? authorizeAsync)
    {
        await EnsureAuthorizedAsync(authorizeAsync, globals?.ActorContext, new AuthorizationRequest
        {
            ResourceType = "Rule",
            ResourceId = rule.Id.ToString(),
            Action = "Execute",
            Reason = $"ExecuteRuleAsync:{rule.Name}"
        });

        if (rule.ConnectionId.HasValue)
        {
            await EnsureAuthorizedAsync(authorizeAsync, globals?.ActorContext, new AuthorizationRequest
            {
                ResourceType = "Connection",
                ResourceId = rule.ConnectionId.Value.ToString(),
                Action = "Use",
                Reason = $"RuleConnection:{rule.Name}"
            });
        }

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
                return await ExecuteBundleAsync(
                    bundle,
                    globals,
                    appendLog,
                    tracker: null,
                    executionId: null,
                    actorContext: globals.ActorContext,
                    authorizeAsync: authorizeAsync);
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
    public Task<object?> ExecuteBundleAsync(
        BusinessRuleBundle bundle,
        TContext baseContext,
        Action<string>? appendLog = null)
    {
        return ExecuteBundleAsync(bundle, baseContext, appendLog, tracker: null, executionId: null, actorContext: baseContext.ActorContext, authorizeAsync: null);
    }

    /// <summary>
    /// Executes a business rule bundle asynchronously with optional real-time sequence and rule level status tracking.
    /// </summary>
    /// <param name="bundle">The business rule bundle to execute.</param>
    /// <param name="baseContext">The execution context.</param>
    /// <param name="appendLog">Optional delegate for receiving plain text log messages.</param>
    /// <param name="tracker">Optional execution tracker instance to update status changes in real time.</param>
    /// <param name="executionId">Optional execution identifier. If null and tracker is provided, a new execution entry will be created.</param>
    /// <param name="actorContext">Optional normalized actor context for execution-level auditing.</param>
    /// <param name="authorizeAsync">Optional application-side authorization callback used for resource checks.</param>
    /// <returns>The result of the final executed step in the bundle.</returns>
    public async Task<object?> ExecuteBundleAsync(
        BusinessRuleBundle bundle,
        TContext baseContext,
        Action<string>? appendLog,
        IBundleExecutionTracker? tracker,
        Guid? executionId = null,
        ExecutionActorContext? actorContext = null,
        Func<AuthorizationRequest, Task<bool>>? authorizeAsync = null)
    {
        actorContext ??= baseContext.ActorContext;
        baseContext.ActorContext = actorContext;

        await EnsureAuthorizedAsync(authorizeAsync, actorContext, new AuthorizationRequest
        {
            ResourceType = "Bundle",
            ResourceId = bundle.Id.ToString(),
            Action = "Execute",
            Reason = $"ExecuteBundleAsync:{bundle.Name}"
        });

        Guid execId = executionId ?? Guid.NewGuid();

        if (tracker != null)
        {
            var existing = await tracker.GetExecutionAsync(execId);
            if (existing == null)
            {
                await tracker.CreateExecutionAsync(bundle, execId, actorContext);
            }
            await tracker.UpdateBundleStatusAsync(execId, ExecutionStatus.Starting, $"Starting execution of bundle '{bundle.Name}'");
        }

        Action<string> log = msg =>
        {
            appendLog?.Invoke(msg);
            if (tracker != null)
            {
                _ = tracker.AppendLogAsync(execId, msg);
            }
        };

        log($"[BUNDLE] --- Starting Bundle: {bundle.Name} ---");
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

            if (tracker != null)
            {
                await tracker.UpdateSequenceStatusAsync(execId, sequenceOrder, ExecutionStatus.Starting, $"Starting sequence step #{sequenceOrder}");
            }

            try
            {
                if (items.Count == 1)
                {
                    var item = items[0];
                    if (tracker != null)
                    {
                        await tracker.UpdateRuleStatusAsync(execId, item.RuleId, sequenceOrder, ExecutionStatus.Starting);
                    }

                    var rule = await _ruleStore.GetBusinessRuleByIdAsync(item.RuleId);
                    if (rule == null)
                    {
                        log($"[ERR] Rule ID {item.RuleId} not found. Skipping.");
                        if (tracker != null)
                        {
                            await tracker.UpdateRuleStatusAsync(execId, item.RuleId, sequenceOrder, ExecutionStatus.Skipped, error: new KeyNotFoundException($"Rule ID {item.RuleId} not found"));
                        }
                        continue;
                    }

                    log($"[BUNDLE] Step {sequenceOrder}: {rule.Name}");

                    try
                    {
                        lastResult = await ExecuteRuleAsync(rule, baseContext, log, authorizeAsync);
                        if (tracker != null)
                        {
                            await tracker.UpdateRuleStatusAsync(execId, item.RuleId, sequenceOrder, ExecutionStatus.Completed, result: lastResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (tracker != null)
                        {
                            await tracker.UpdateRuleStatusAsync(execId, item.RuleId, sequenceOrder, ExecutionStatus.Failed, error: ex);
                        }
                        throw;
                    }

                    if (tracker != null)
                    {
                        await tracker.UpdateSequenceStatusAsync(execId, sequenceOrder, ExecutionStatus.Completed, $"Sequence #{sequenceOrder} completed successfully");
                    }
                }
                else
                {
                    log($"[BUNDLE] Step {sequenceOrder}: Executing {items.Count} rules in parallel.");

                    if (tracker != null)
                    {
                        await tracker.UpdateSequenceStatusAsync(execId, sequenceOrder, ExecutionStatus.Starting, $"Executing {items.Count} rules in parallel");
                    }

                    var tasks = items.Select(async item =>
                    {
                        if (tracker != null)
                        {
                            await tracker.UpdateRuleStatusAsync(execId, item.RuleId, sequenceOrder, ExecutionStatus.Starting);
                        }

                        var rule = await _ruleStore.GetBusinessRuleByIdAsync(item.RuleId);
                        if (rule == null)
                        {
                            log($"[ERR] Rule ID {item.RuleId} not found.");
                            if (tracker != null)
                            {
                                await tracker.UpdateRuleStatusAsync(execId, item.RuleId, sequenceOrder, ExecutionStatus.Skipped, error: new KeyNotFoundException($"Rule ID {item.RuleId} not found"));
                            }
                            return null;
                        }

                        try
                        {
                            var result = await ExecuteRuleAsync(rule, baseContext, log, authorizeAsync);
                            if (tracker != null)
                            {
                                await tracker.UpdateRuleStatusAsync(execId, item.RuleId, sequenceOrder, ExecutionStatus.Completed, result: result);
                            }
                            return result;
                        }
                        catch (Exception ex)
                        {
                            if (tracker != null)
                            {
                                await tracker.UpdateRuleStatusAsync(execId, item.RuleId, sequenceOrder, ExecutionStatus.Failed, error: ex);
                            }
                            throw;
                        }
                    });

                    var results = await Task.WhenAll(tasks);
                    lastResult = results.ToList();

                    if (tracker != null)
                    {
                        await tracker.UpdateSequenceStatusAsync(execId, sequenceOrder, ExecutionStatus.Completed, $"Sequence #{sequenceOrder} parallel execution completed successfully");
                    }
                }

                // Store in history
                baseContext.StepResults[sequenceOrder] = lastResult;
            }
            catch (Exception ex)
            {
                log($"[BUNDLE] [FATAL] Step group {sequenceOrder} failed: {ex.Message}. Aborting bundle.");
                if (tracker != null)
                {
                    await tracker.UpdateSequenceStatusAsync(execId, sequenceOrder, ExecutionStatus.Failed, ex.Message);
                    await tracker.CompleteExecutionAsync(execId, ExecutionStatus.Failed, null, ex);
                }
                break;
            }
        }

        log($"[BUNDLE] --- Bundle Finished: {bundle.Name} ---");
        if (tracker != null)
        {
            var currentState = await tracker.GetExecutionAsync(execId);
            if (currentState != null && currentState.Status != ExecutionStatus.Failed)
            {
                await tracker.CompleteExecutionAsync(execId, ExecutionStatus.Completed, lastResult);
            }
        }

        return lastResult;
    }

    private async Task EnsureAuthorizedAsync(
        Func<AuthorizationRequest, Task<bool>>? authorizeAsync,
        ExecutionActorContext? actorContext,
        AuthorizationRequest request)
    {
        bool allowed;
        if (authorizeAsync != null)
        {
            allowed = await authorizeAsync(request);
        }
        else if (_authorizationService != null)
        {
            allowed = await _authorizationService.AuthorizeAsync(request, actorContext);
        }
        else
        {
            if (_authorizationFailClosed)
            {
                throw new InvalidOperationException(
                    "Authorization fail-closed mode is enabled, but no authorization callback or IRuleAuthorizationService was provided.");
            }

            return;
        }

        if (!allowed)
        {
            throw new UnauthorizedAccessException($"Access denied for action '{request.Action}' on resource '{request.ResourceType}:{request.ResourceId}'.");
        }
    }
}
