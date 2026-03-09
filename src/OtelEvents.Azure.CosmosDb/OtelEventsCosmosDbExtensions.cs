using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OtelEvents.Azure.CosmosDb.Events;

namespace OtelEvents.Azure.CosmosDb;

/// <summary>
/// Extension methods for registering <see cref="OtelEventsCosmosDbObserver"/>
/// in the dependency injection container.
/// </summary>
public static class OtelEventsCosmosDbExtensions
{
    /// <summary>
    /// Adds the OtelEvents CosmosDB observer that emits structured events
    /// for Azure CosmosDB operations via DiagnosticListener.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional action to configure <see cref="OtelEventsCosmosDbOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddOtelEventsCosmosDb(options =>
    /// {
    ///     options.CaptureQueryText = false;
    ///     options.EnableCausalScope = true;
    ///     options.RuThreshold = 10;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOtelEventsCosmosDb(
        this IServiceCollection services,
        Action<OtelEventsCosmosDbOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OtelEventsCosmosDbOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OtelEventsCosmosDbEventSource>>();
            var observer = new OtelEventsCosmosDbObserver(logger, options);
            observer.Subscribe();
            return observer;
        });

        return services;
    }
}
