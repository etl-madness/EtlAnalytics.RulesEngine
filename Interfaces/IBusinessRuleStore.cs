using System.Threading.Tasks;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.RulesEngine.Interfaces;

/// <summary>
/// Defines the contract for a store that manages business rules and bundles.
/// </summary>
public interface IBusinessRuleStore
{
    /// <summary>Retrieves a business rule by its identifier.</summary>
    /// <param name="id">The rule identifier.</param>
    /// <returns>The business rule, or null if not found.</returns>
    Task<BusinessRule?> GetBusinessRuleByIdAsync(int id);
    /// <summary>Retrieves a business rule bundle by its name.</summary>
    /// <param name="name">The bundle name.</param>
    /// <returns>The bundle definition, or null if not found.</returns>
    Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name);
    /// <summary>Retrieves a database connection definition by its identifier.</summary>
    /// <param name="id">The connection identifier.</param>
    /// <returns>The connection definition, or null if not found.</returns>
    Task<DbConnectionDefinition?> GetDbConnectionByIdAsync(int id);
    /// <summary>Retrieves all available database connection definitions.</summary>
    /// <returns>A collection of connection definitions.</returns>
    Task<IEnumerable<DbConnectionDefinition>> GetAllDbConnectionsAsync();
}
