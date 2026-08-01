namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents a provider-agnostic authorization request emitted by the rules engine enforcement hooks.
/// </summary>
public class AuthorizationRequest
{
    /// <summary>Gets or sets the resource type being accessed (for example Bundle, Rule, Connection).</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Gets or sets the resource identifier being accessed.</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the action being requested (for example Read, Execute, Update).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets a short reason used for audit diagnostics.</summary>
    public string? Reason
    {
        get; set;
    }
}
