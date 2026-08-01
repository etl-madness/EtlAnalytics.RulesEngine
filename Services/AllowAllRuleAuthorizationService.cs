using System.Threading;
using System.Threading.Tasks;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.RulesEngine.Services;

/// <summary>
/// Default authorization service that allows all operations.
/// Hosts should replace this with a policy-backed implementation.
/// </summary>
public sealed class AllowAllRuleAuthorizationService : IRuleAuthorizationService
{
    /// <inheritdoc />
    public Task<bool> AuthorizeAsync(AuthorizationRequest request, ExecutionActorContext? actorContext = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
