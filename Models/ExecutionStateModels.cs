using System;
using System.Collections.Generic;

namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Holds status and execution details for an individual rule within a bundle sequence.
/// </summary>
public class RuleExecutionState
{
    /// <summary>Gets or sets the rule identifier.</summary>
    public int RuleId { get; set; }

    /// <summary>Gets or sets the rule name.</summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>Gets or sets the rule execution type (e.g. TSQL, CSharp).</summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>Gets or sets the sequence order group this rule belongs to.</summary>
    public int SequenceOrder { get; set; }

    /// <summary>Gets or sets the current execution status.</summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>Gets or sets the timestamp when execution started.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Gets or sets the timestamp when execution completed or failed.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Gets or sets the result produced by the rule, if any.</summary>
    public object? Result { get; set; }

    /// <summary>Gets or sets the error message if execution failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Holds status and execution details for a group of rules sharing the same sequence order.
/// Supports both single rule steps and parallel rule steps.
/// </summary>
public class SequenceExecutionState
{
    /// <summary>Gets or sets the sequence order number.</summary>
    public int SequenceOrder { get; set; }

    /// <summary>Gets or sets the overall sequence execution status.</summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>Gets or sets the timestamp when sequence execution started.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Gets or sets the timestamp when sequence execution completed or failed.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Gets or sets the status message for the sequence.</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets the list of rule execution states in this sequence (includes parallel rules).</summary>
    public List<RuleExecutionState> Rules { get; set; } = new();
}

/// <summary>
/// Holds overall status, timing, and sequence-level breakdown for a business rule bundle execution run.
/// </summary>
public class BundleExecutionState
{
    /// <summary>Gets or sets the unique execution identifier.</summary>
    public Guid ExecutionId { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the parent bundle identifier.</summary>
    public int BundleId { get; set; }

    /// <summary>Gets or sets the bundle name.</summary>
    public string BundleName { get; set; } = string.Empty;

    /// <summary>Gets or sets the overall bundle execution status.</summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>Gets or sets the timestamp when bundle execution started.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Gets or sets the timestamp when bundle execution completed or failed.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Gets or sets the final return result of the bundle execution.</summary>
    public object? FinalResult { get; set; }

    /// <summary>Gets or sets error details if the bundle execution failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the sequence execution states ordered by sequence number.</summary>
    public List<SequenceExecutionState> Sequences { get; set; } = new();

    /// <summary>Gets or sets execution log entries appended during execution.</summary>
    public List<string> Logs { get; set; } = new();
}

/// <summary>
/// Event arguments emitted whenever a bundle, sequence, or rule changes state.
/// </summary>
public class BundleProgressEventArgs : EventArgs
{
    /// <summary>Gets the unique bundle execution identifier.</summary>
    public Guid ExecutionId { get; }

    /// <summary>Gets the name of the bundle being executed.</summary>
    public string BundleName { get; }

    /// <summary>Gets the sequence order associated with the progress event, if applicable.</summary>
    public int? SequenceOrder { get; }

    /// <summary>Gets the rule identifier associated with the progress event, if applicable.</summary>
    public int? RuleId { get; }

    /// <summary>Gets the rule name associated with the progress event, if applicable.</summary>
    public string? RuleName { get; }

    /// <summary>Gets the status being reported.</summary>
    public ExecutionStatus Status { get; }

    /// <summary>Gets descriptive text or log snippet associated with the update.</summary>
    public string Message { get; }

    /// <summary>Gets the full current state snapshot at the time of the event.</summary>
    public BundleExecutionState CurrentState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BundleProgressEventArgs"/> class.
    /// </summary>
    public BundleProgressEventArgs(
        Guid executionId,
        string bundleName,
        ExecutionStatus status,
        string message,
        BundleExecutionState currentState,
        int? sequenceOrder = null,
        int? ruleId = null,
        string? ruleName = null)
    {
        ExecutionId = executionId;
        BundleName = bundleName;
        Status = status;
        Message = message;
        CurrentState = currentState;
        SequenceOrder = sequenceOrder;
        RuleId = ruleId;
        RuleName = ruleName;
    }
}
