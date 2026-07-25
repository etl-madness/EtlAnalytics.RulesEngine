using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Provides a base class for the execution context of a rule, containing state and shared services.
/// </summary>
public class RuleExecutionContext
{
    /// <summary>Gets or sets the time the execution started.</summary>
    public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;
    /// <summary>Gets or sets the result of the immediately preceding rule in a bundle.</summary>
    public object? PreviousResult { get; set; }
    /// <summary>Gets or sets a dictionary of results from all previous steps in the current bundle, keyed by sequence order.</summary>
    public Dictionary<int, object?> StepResults { get; set; } = new();
    /// <summary>Gets or sets an optional logging action for rule scripts to use.</summary>
    public Action<string>? Log { get; set; }
    /// <summary>Gets or sets a function allowing rules to trigger other bundles by name.</summary>
    public Func<string, Task<object?>>? RunBundle { get; set; }
    /// <summary>Gets or sets the cancellation token for the current execution.</summary>
    public System.Threading.CancellationToken CancellationToken { get; set; }
}
