using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtelEvents.HttpClient.Events;

namespace OtelEvents.HttpClient;

/// <summary>
/// Extension methods for registering the OtelEvents.HttpClient outbound tracking handler
/// with <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class OtelEventsHttpClientExtensions
{
    /// <summary>
    /// Adds the OtelEvents outbound tracking <see cref="DelegatingHandler"/> to the HTTP client pipeline.
    /// Emits structured events for every outbound HTTP call: started, completed, and failed.
    /// </summary>
    /// <param name="builder">The HTTP client builder to configure.</param>
    /// <param name="configure">Optional action to configure <see cref="OtelEventsOutboundTrackingOptions"/>.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <remarks>
    /// Options are keyed to the HttpClient name, so multiple named clients can have
    /// independent configurations without overwriting each other.
    /// </remarks>
    public static IHttpClientBuilder AddOtelEventsOutboundTracking(
        this IHttpClientBuilder builder,
        Action<OtelEventsOutboundTrackingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure<OtelEventsOutboundTrackingOptions>(builder.Name, configure);
        }

        builder.AddHttpMessageHandler(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OtelEventsHttpClientEventSource>>();
            var options = sp.GetRequiredService<IOptionsMonitor<OtelEventsOutboundTrackingOptions>>();

            return new OtelEventsOutboundTrackingHandler(logger, options, builder.Name);
        });

        return builder;
    }
}
