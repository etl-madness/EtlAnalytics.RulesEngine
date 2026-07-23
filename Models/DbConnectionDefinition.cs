namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Represents a database connection definition.
/// </summary>
public class DbConnectionDefinition
{
    /// <summary>Gets or sets the connection identifier.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the name of the connection.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the connection string (stored as AES-256 encrypted Base64).</summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Gets or sets the database provider type (e.g., "SqlServer", "Postgres").</summary>
    public string ProviderType { get; set; } = "SqlServer";
    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
