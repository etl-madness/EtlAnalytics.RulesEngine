using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.RulesEngine.Executors;

/// <summary>
/// Executes business rules containing C# script code using Roslyn scripting.
/// </summary>
public class CSharpRuleExecutor : IRuleExecutor
{
    private readonly string[] _scriptReferences;
    private readonly string[] _scriptImports;
    private readonly int _scriptTimeoutSeconds;

    private static readonly System.Reflection.Assembly[] DefaultScriptReferences =
    {
        typeof(object).Assembly, // mscorlib / System.Runtime
        typeof(System.Linq.Enumerable).Assembly,
        typeof(System.Collections.Generic.List<>).Assembly,
        typeof(System.Xml.Linq.XElement).Assembly,
        typeof(System.Xml.XmlDocument).Assembly,
        typeof(RuleExecutionContext).Assembly
    };

    private static readonly string[] DefaultScriptImports =
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

    /// <inheritdoc />
    public string RuleType => RuleConstants.CSharp;

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpRuleExecutor"/> class.
    /// </summary>
    /// <param name="scriptReferences">Assemblies to reference in scripts.</param>
    /// <param name="scriptImports">Namespaces to import in scripts.</param>
    /// <param name="scriptTimeoutSeconds">Maximum allowed script execution time in seconds.</param>
    public CSharpRuleExecutor(
        string[] scriptReferences,
        string[] scriptImports,
        int scriptTimeoutSeconds)
    {
        _scriptReferences = scriptReferences;
        _scriptImports = scriptImports;
        _scriptTimeoutSeconds = scriptTimeoutSeconds;
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(BusinessRule rule, RuleExecutionContext context, Type contextType, Action<string>? appendLog)
    {
        appendLog?.Invoke("[CS] Compiling and executing C# script...");

        // Define a restricted set of allowed assemblies and namespaces
        var options = ScriptOptions.Default;

        var references = _scriptReferences
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToArray();
        options = options.WithImports(_scriptImports);
        options = references.Length > 0
            ? options.WithReferences(references)
            : options.WithReferences(DefaultScriptReferences);
        var imports = _scriptImports
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToArray();
        options = options.WithImports(imports.Length > 0 ? imports : DefaultScriptImports);


        // Add reference to the assembly containing contextType if it's different and not already added
        if (contextType.Assembly != typeof(RuleExecutionContext).Assembly)
        {
            options = options.AddReferences(contextType.Assembly);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_scriptTimeoutSeconds));
        if (context != null)
        {
            context.CancellationToken = cts.Token;
        }

        try
        {
            // Evaluate script with timeout and restricted options
            var result = await CSharpScript.EvaluateAsync(rule.Code, options, context, contextType, cts.Token);

            if (cts.Token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cts.Token);
            }

            appendLog?.Invoke("[CS] Execution completed successfully.");
            return result;
        }
        catch (OperationCanceledException)
        {
            appendLog?.Invoke($"[ERR] Script execution timed out after {_scriptTimeoutSeconds} seconds.");
            throw new TimeoutException($"The C# script exceeded the maximum execution time of {_scriptTimeoutSeconds} seconds.");
        }
        catch (CompilationErrorException ex)
        {
            appendLog?.Invoke($"[ERR] Compilation Error: {string.Join(Environment.NewLine, ex.Diagnostics)}");
            throw;
        }
    }
}
