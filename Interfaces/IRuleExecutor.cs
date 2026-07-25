using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.RulesEngine.Interfaces;

/// <summary>
/// Defines the contract for an execution engine that can process a specific type of business rule.
/// </summary>
public interface IRuleExecutor
{
    /// <summary>
    /// Gets the type of rule this executor handles (e.g., "TSQL", "CSharp", "Javascript").
    /// </summary>
    string RuleType { get; }

    /// <summary>
    /// Executes the business rule asynchronously.
    /// </summary>
    /// <param name="rule">The rule definition to execute.</param>
    /// <param name="context">The execution context containing variables and services for the rule.</param>
    /// <param name="contextType">The specific type of the execution context (for generic support).</param>
    /// <param name="appendLog">Optional callback for logging execution details.</param>
    /// <returns>The result of the rule execution.</returns>
    Task<object?> ExecuteAsync(BusinessRule rule, RuleExecutionContext context, Type contextType, Action<string>? appendLog);
}
