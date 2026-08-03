using System;

namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents a historical version of a business rule.
/// </summary>
public class BusinessRuleHistory
{
    /// <summary>Gets or sets the history record identifier.</summary>
    public int Id
    {
        get; set;
    }
    /// <summary>Gets or sets the identifier of the related rule.</summary>
    public int RuleId
    {
        get; set;
    }
    /// <summary>Gets or sets the code or script as it was at the time of this record.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Gets or sets the version number for this historical record.</summary>
    public int Version
    {
        get; set;
    }
    /// <summary>Gets or sets the timestamp when the change occurred.</summary>
    public DateTime ChangedAt { get; set; } = DateTime.Now;
    /// <summary>Gets or sets the user or system that performed the change.</summary>
    public string ChangedBy { get; set; } = "System";
    /// <summary>Gets or sets the stable actor identifier that performed the change.</summary>
    public string? ChangedById
    {
        get; set;
    }
    /// <summary>Gets or sets the actor display name that performed the change.</summary>
    public string? ChangedByName
    {
        get; set;
    }
}
