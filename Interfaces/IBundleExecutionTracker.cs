using System;
using System.Threading.Tasks;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.RulesEngine.Interfaces;

/// <summary>
/// Contract for tracking, persisting, and observing real-time progress of business rule bundle executions.
/// </summary>
public interface IBundleExecutionTracker
{
    /// <summary>
    /// Pre-populates and registers a new bundle execution state with all sequences and rules initialized in the <see cref="ExecutionStatus.Pending"/> state.
    /// </summary>
    /// <param name="bundle">The business rule bundle to be executed.</param>
    /// <param name="executionId">Optional custom execution identifier. If null, a new GUID will be generated.</param>
    /// <returns>The newly created <see cref="BundleExecutionState"/>.</returns>
    Task<BundleExecutionState> CreateExecutionAsync(BusinessRuleBundle bundle, Guid? executionId = null);

    /// <summary>
    /// Pre-populates and registers a new bundle execution state including optional actor metadata from the consuming application.
    /// </summary>
    /// <param name="bundle">The business rule bundle to be executed.</param>
    /// <param name="executionId">Optional custom execution identifier. If null, a new GUID will be generated.</param>
    /// <param name="actorContext">Optional normalized actor context supplied by the application.</param>
    /// <returns>The newly created <see cref="BundleExecutionState"/>.</returns>
    Task<BundleExecutionState> CreateExecutionAsync(BusinessRuleBundle bundle, Guid? executionId, ExecutionActorContext? actorContext)
        => CreateExecutionAsync(bundle, executionId);

    /// <summary>
    /// Updates the overall status of the bundle execution run.
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <param name="status">The new status.</param>
    /// <param name="message">Optional message or log line to record.</param>
    Task UpdateBundleStatusAsync(Guid executionId, ExecutionStatus status, string? message = null);

    /// <summary>
    /// Updates the status of a specific sequence group within a bundle execution run.
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <param name="sequenceOrder">The sequence order group number.</param>
    /// <param name="status">The new status.</param>
    /// <param name="message">Optional status message or log detail.</param>
    Task UpdateSequenceStatusAsync(Guid executionId, int sequenceOrder, ExecutionStatus status, string? message = null);

    /// <summary>
    /// Updates the status of an individual rule within a bundle sequence (including parallel rules).
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <param name="ruleId">The rule identifier.</param>
    /// <param name="sequenceOrder">The sequence order group number.</param>
    /// <param name="status">The new status.</param>
    /// <param name="result">Optional execution result produced by the rule.</param>
    /// <param name="error">Optional exception if rule execution failed.</param>
    Task UpdateRuleStatusAsync(Guid executionId, int ruleId, int sequenceOrder, ExecutionStatus status, object? result = null, Exception? error = null);

    /// <summary>
    /// Marks the bundle execution run as completed or failed with a final result or exception.
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <param name="status">Final execution status (e.g. Completed or Failed).</param>
    /// <param name="finalResult">Final return result of the bundle.</param>
    /// <param name="error">Optional error exception if the bundle run failed.</param>
    Task CompleteExecutionAsync(Guid executionId, ExecutionStatus status, object? finalResult = null, Exception? error = null);

    /// <summary>
    /// Retrieves the current execution state snapshot for a given execution ID.
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <returns>The <see cref="BundleExecutionState"/> snapshot, or null if not found.</returns>
    Task<BundleExecutionState?> GetExecutionAsync(Guid executionId);

    /// <summary>
    /// Appends a log line to the bundle execution state.
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <param name="logMessage">The message line to log.</param>
    Task AppendLogAsync(Guid executionId, string logMessage);

    /// <summary>
    /// Event triggered whenever a bundle, sequence, or rule changes execution status.
    /// </summary>
    event EventHandler<BundleProgressEventArgs>? OnStatusChanged;
}
