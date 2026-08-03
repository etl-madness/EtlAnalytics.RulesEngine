using Microsoft.Extensions.DependencyInjection;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Services;

namespace EtlAnalytics.RulesEngine;

/// <summary>
/// Extension methods for registering rules engine services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IBundleExecutionTracker"/> and default <see cref="InMemoryBundleExecutionTracker"/> singleton service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddBusinessRulesEngineTracking(this IServiceCollection services)
    {
        services.AddSingleton<IBundleExecutionTracker, InMemoryBundleExecutionTracker>();
        return services;
    }

    /// <summary>
    /// Registers a permissive default <see cref="IRuleAuthorizationService"/> implementation.
    /// Replace this in production with a policy-backed authorization service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddBusinessRulesEngineAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IRuleAuthorizationService, AllowAllRuleAuthorizationService>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IRuleAuthorizationService"/> implementation.
    /// </summary>
    /// <typeparam name="TAuthorizationService">The custom authorization service type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The desired service lifetime.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddBusinessRulesEngineAuthorization<TAuthorizationService>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TAuthorizationService : class, IRuleAuthorizationService
    {
        services.Add(new ServiceDescriptor(typeof(IRuleAuthorizationService), typeof(TAuthorizationService), lifetime));
        return services;
    }
}
