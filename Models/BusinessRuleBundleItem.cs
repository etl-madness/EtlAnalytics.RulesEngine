namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents a mapping between a rule and a bundle, including the execution order.
/// </summary>
public class BusinessRuleBundleItem
{
    /// <summary>Gets or sets the identifier for this mapping.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the identifier of the parent bundle.</summary>
    public int BundleId { get; set; }
    /// <summary>Gets or sets the identifier of the rule to execute.</summary>
    public int RuleId { get; set; }
    /// <summary>Gets or sets the order in which this rule should be executed within the bundle.</summary>
    public int SequenceOrder { get; set; }
    
    // UI Helpers
    /// <summary>Gets or sets the name of the rule (helper property).</summary>
    public string? RuleName { get; set; }
    /// <summary>Gets or sets the type of the rule (helper property).</summary>
    public string? RuleType { get; set; }
}
