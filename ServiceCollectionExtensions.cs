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
}
