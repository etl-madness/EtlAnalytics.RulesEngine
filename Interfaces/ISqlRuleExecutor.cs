using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EtlAnalytics.RulesEngine.Interfaces;

/// <summary>
/// Defines the contract for an executor that handles SQL execution for business rules.
/// This abstraction allows the core engine to remain independent of specific database libraries like Dapper.
/// </summary>
public interface ISqlRuleExecutor
{
    /// <summary>
    /// Executes a SQL command asynchronously and returns the results as a collection of dynamic objects.
    /// </summary>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">The parameters to pass to the query.</param>
    /// <param name="connectionString">The connection string to use.</param>
    /// <param name="providerType">The type of database provider (e.g., "SqlServer", "Postgres").</param>
    /// <param name="timeoutSeconds">The execution timeout in seconds.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A collection of dynamic objects representing the result rows.</returns>
    Task<IEnumerable<dynamic>> ExecuteAsync(
        string sql, 
        Dictionary<string, object>? parameters, 
        string connectionString, 
        string providerType,
        int timeoutSeconds,
        CancellationToken ct);
}
