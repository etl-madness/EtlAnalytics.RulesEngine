using System;

namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents a single business rule definition.
/// </summary>
public class BusinessRule
{
    /// <summary>Gets or sets the rule identifier.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the name of the rule.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets a description of the rule's purpose.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Gets or sets the type of the rule (e.g., TSQL, CSharp).</summary>
    public string RuleType { get; set; } = string.Empty;
    /// <summary>Gets or sets the actual code or script to be executed.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Gets or sets the version number of the rule.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Gets or sets an optional connection identifier to run the rule against a specific database.</summary>
    public int? ConnectionId { get; set; }
    /// <summary>Gets or sets the categories associated with this rule.</summary>
    public List<string> Categories { get; set; } = new();
    /// <summary>Gets or sets the tags associated with this rule.</summary>
    public List<string> Tags { get; set; } = new();
    /// <summary>Gets or sets a value indicating whether the rule is active.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>Gets or sets the last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
