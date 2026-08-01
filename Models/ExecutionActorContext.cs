using System;
using System.Collections.Generic;

namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents the normalized actor identity and execution metadata passed from the consuming application.
/// </summary>
public class ExecutionActorContext
{
    /// <summary>Gets or sets the stable actor identifier (user id or service principal id).</summary>
    public string? ActorId
    {
        get; set;
    }

    /// <summary>Gets or sets the display name for the actor.</summary>
    public string? ActorName
    {
        get; set;
    }

    /// <summary>Gets or sets the authentication method used by the actor (for example JWT, AD, or local).</summary>
    public string? AuthMethod
    {
        get; set;
    }

    /// <summary>Gets or sets the actor type (for example User, ServicePrincipal, or System).</summary>
    public string? ActorType
    {
        get; set;
    }

    /// <summary>Gets or sets the authorization decision correlation id supplied by the application.</summary>
    public Guid? DecisionCorrelationId
    {
        get; set;
    }

    /// <summary>Gets or sets optional metadata captured by the host application.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
