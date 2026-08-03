using System;
using System.Collections.Generic;

namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents a bundle (collection) of business rules to be executed in sequence.
/// </summary>
public class BusinessRuleBundle
{
    /// <summary>Gets or sets the bundle identifier.</summary>
    public int Id
    {
        get; set;
    }
    /// <summary>Gets or sets the name of the bundle.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets a description of the bundle.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Gets or sets the categories associated with this bundle.</summary>
    public List<string> Categories { get; set; } = new();
    /// <summary>Gets or sets the tags associated with this bundle.</summary>
    public List<string> Tags { get; set; } = new();
    /// <summary>Gets or sets a value indicating whether the bundle is active.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>Gets or sets the identifier of the actor that created this bundle.</summary>
    public string? CreatedBy
    {
        get; set;
    }
    /// <summary>Gets or sets the display name of the actor that created this bundle.</summary>
    public string? CreatedByName
    {
        get; set;
    }
    /// <summary>Gets or sets the last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    /// <summary>Gets or sets the identifier of the actor that last modified this bundle.</summary>
    public string? ModifiedBy
    {
        get; set;
    }
    /// <summary>Gets or sets the display name of the actor that last modified this bundle.</summary>
    public string? ModifiedByName
    {
        get; set;
    }
    /// <summary>Gets or sets the current owner identifier for this bundle.</summary>
    public string? OwnerUserId
    {
        get; set;
    }
    /// <summary>Gets or sets whether owner-derived privileges have been revoked.</summary>
    public bool OwnershipRevoked
    {
        get; set;
    }

    /// <summary>Gets or sets the list of items (rules) contained within the bundle.</summary>
    public List<BusinessRuleBundleItem> Items { get; set; } = new();
}
