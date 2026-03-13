using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    public static IHttpClientBuilder AddOtelEventsOutboundTracking(
        this IHttpClientBuilder builder,
        Action<OtelEventsOutboundTrackingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.AddHttpMessageHandler(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OtelEventsOutboundTrackingHandler>>();
            var options = sp.GetRequiredService<IOptionsMonitor<OtelEventsOutboundTrackingOptions>>();

            return new OtelEventsOutboundTrackingHandler(logger, options, builder.Name);
        });

        return builder;
    }
}
