using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace OtelEvents.HealthChecks;

/// <summary>
/// Extension methods for registering <see cref="OtelEventsHealthCheckPublisher"/>
/// in the dependency injection container.
/// </summary>
public static class OtelEventsHealthCheckExtensions
{
    /// <summary>
    /// Adds the OtelEvents health check publisher that emits structured events
    /// for health check execution results and state changes.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional action to configure <see cref="OtelEventsHealthCheckOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddOtelEventsHealthChecks(options =>
    /// {
    ///     options.EmitExecutedEvents = true;
    ///     options.EmitStateChangedEvents = true;
    ///     options.EmitReportCompletedEvents = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOtelEventsHealthChecks(
        this IServiceCollection services,
        Action<OtelEventsHealthCheckOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OtelEventsHealthCheckOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IHealthCheckPublisher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OtelEventsHealthCheckPublisher>>();
            return new OtelEventsHealthCheckPublisher(logger, options);
        });

        return services;
    }
}
