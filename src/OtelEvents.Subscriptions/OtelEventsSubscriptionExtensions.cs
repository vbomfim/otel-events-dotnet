using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Subscriptions;

/// <summary>
/// Extension methods for registering <see cref="OtelEventsSubscriptionProcessor"/>
/// and the background dispatch loop in the dependency injection container.
/// </summary>
public static class OtelEventsSubscriptionExtensions
{
    /// <summary>
    /// Adds the OtelEvents subscription system: a processor that dispatches matching events
    /// to registered handlers via a background channel.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureSubscriptions">
    /// Optional action to register event subscriptions (lambda and DI-resolved handlers).
    /// </param>
    /// <param name="configureOptions">
    /// Optional action to configure <see cref="OtelEventsSubscriptionOptions"/>
    /// (channel capacity, backpressure policy).
    /// </param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddOtelEventsSubscriptions(
    ///     subs =>
    ///     {
    ///         subs.On("cosmosdb.throttled", async (ctx, ct) =>
    ///         {
    ///             var retryMs = ctx.GetAttribute&lt;long&gt;("retryAfterMs");
    ///             await Task.Delay(TimeSpan.FromMilliseconds(retryMs), ct);
    ///         });
    ///         subs.AddHandler&lt;OrderPlacedHandler&gt;("order.placed");
    ///     },
    ///     opts =>
    ///     {
    ///         opts.ChannelCapacity = 2048;
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddOtelEventsSubscriptions(
        this IServiceCollection services,
        Action<OtelEventsSubscriptionBuilder>? configureSubscriptions = null,
        Action<OtelEventsSubscriptionOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OtelEventsSubscriptionOptions();
        configureOptions?.Invoke(options);

        var builder = new OtelEventsSubscriptionBuilder(services);
        configureSubscriptions?.Invoke(builder);

        // Create the bounded channel with configured capacity and backpressure policy
        var channel = Channel.CreateBounded<DispatchItem>(
            new BoundedChannelOptions(options.ChannelCapacity)
            {
                FullMode = options.FullMode,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });

        // Register the channel as a singleton for processor and dispatcher to share
        services.AddSingleton(channel);

        // Register the processor as a singleton for pipeline registration
        services.AddSingleton(sp =>
            new OtelEventsSubscriptionProcessor(channel, builder.Registrations));

        // Register the background dispatch loop
        services.AddLogging();
        services.AddHostedService<OtelEventsSubscriptionDispatcher>();

        return services;
    }
}
