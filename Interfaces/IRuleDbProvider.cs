using System.Data;

namespace EtlAnalytics.RulesEngine.Interfaces;

/// <summary>
/// Defines the contract for a provider that creates database connections for rules.
/// </summary>
public interface IRuleDbProvider
{
    /// <summary>Gets the unique name of the provider (e.g., "SqlServer", "Postgres").</summary>
    string ProviderType { get; }
    /// <summary>Creates a new database connection using the specified connection string.</summary>
    /// <param name="connectionString">The connection string to use.</param>
    /// <returns>A new <see cref="IDbConnection"/> instance.</returns>
    IDbConnection CreateConnection(string connectionString);
}
