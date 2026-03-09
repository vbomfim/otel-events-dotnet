using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OtelEvents.Azure.Storage.Events;

namespace OtelEvents.Azure.Storage;

/// <summary>
/// Extension methods for registering the OtelEvents.Azure.Storage integration pack
/// in the dependency injection container.
/// </summary>
public static class OtelEventsAzureStorageExtensions
{
    /// <summary>
    /// Adds OtelEvents.Azure.Storage services with default options.
    /// Registers the pipeline policy for injection into Azure SDK client configurations.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddOtelEventsAzureStorage(this IServiceCollection services)
    {
        return services.AddOtelEventsAzureStorage(_ => { });
    }

    /// <summary>
    /// Adds OtelEvents.Azure.Storage services with the specified options.
    /// Registers the pipeline policy for injection into Azure SDK client configurations.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Action to configure <see cref="OtelEventsAzureStorageOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddOtelEventsAzureStorage(
        this IServiceCollection services,
        Action<OtelEventsAzureStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new OtelEventsAzureStorageOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OtelEventsStorageEventSource>>();
            return new OtelEventsStoragePipelinePolicy(logger, options);
        });

        return services;
    }
}
