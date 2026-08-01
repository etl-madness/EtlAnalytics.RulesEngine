namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents a database connection definition.
/// </summary>
public class DbConnectionDefinition
{
    /// <summary>Gets or sets the connection identifier.</summary>
    public int Id
    {
        get; set;
    }
    /// <summary>Gets or sets the name of the connection.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the connection string (stored as AES-256 encrypted Base64).</summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Gets or sets the database provider type (e.g., "SqlServer", "Postgres").</summary>
    public string ProviderType { get; set; } = "SqlServer";
    /// <summary>Gets or sets the categories associated with this connection.</summary>
    public List<string> Categories { get; set; } = new();
    /// <summary>Gets or sets the tags associated with this connection.</summary>
    public List<string> Tags { get; set; } = new();
    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Gets or sets the identifier of the actor that created this connection.</summary>
    public string? CreatedBy
    {
        get; set;
    }
    /// <summary>Gets or sets the display name of the actor that created this connection.</summary>
    public string? CreatedByName
    {
        get; set;
    }
    /// <summary>Gets or sets the timestamp of the last modification.</summary>
    public DateTime? ModifiedAtUtc
    {
        get; set;
    }
    /// <summary>Gets or sets the identifier of the actor that last modified this connection.</summary>
    public string? ModifiedBy
    {
        get; set;
    }
    /// <summary>Gets or sets the display name of the actor that last modified this connection.</summary>
    public string? ModifiedByName
    {
        get; set;
    }
    /// <summary>Gets or sets the current owner identifier for this connection.</summary>
    public string? OwnerUserId
    {
        get; set;
    }
    /// <summary>Gets or sets whether owner-derived privileges have been revoked.</summary>
    public bool OwnershipRevoked
    {
        get; set;
    }
}
