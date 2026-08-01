using System.Threading;
using System.Threading.Tasks;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.RulesEngine.Interfaces;

/// <summary>
/// Contract for host-provided rule authorization checks.
/// </summary>
public interface IRuleAuthorizationService
{
    /// <summary>
    /// Evaluates whether the requested action is authorized for the provided actor context.
    /// </summary>
    /// <param name="request">The normalized authorization request.</param>
    /// <param name="actorContext">Optional actor context supplied by the host.</param>
    /// <param name="cancellationToken">Cancellation signal for long-running policy checks.</param>
    /// <returns>True when access is allowed; otherwise false.</returns>
    Task<bool> AuthorizeAsync(AuthorizationRequest request, ExecutionActorContext? actorContext = null, CancellationToken cancellationToken = default);
}
