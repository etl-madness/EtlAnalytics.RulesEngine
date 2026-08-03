namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents the execution state of a business rule, sequence, or bundle.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// The step has been queued or scheduled but has not started executing yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The step is currently starting or actively executing.
    /// </summary>
    Starting = 1,

    /// <summary>
    /// The step completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The step encountered an error or unhandled exception during execution.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The step was skipped (e.g. due to a prior failure in the bundle execution).
    /// </summary>
    Skipped = 4
}
