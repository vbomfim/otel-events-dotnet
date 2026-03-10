using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    /// (channel capacity, backpressure policy, handler timeout).
    /// </param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="OtelEventsSubscriptionOptions.ChannelCapacity"/> is less than or equal to zero
    /// or when <see cref="OtelEventsSubscriptionOptions.HandlerTimeout"/> is not positive.
    /// </exception>
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
        options.Validate();

        // Register options as IOptions<T> for the dispatcher to consume
        services.Configure<OtelEventsSubscriptionOptions>(opts =>
        {
            opts.ChannelCapacity = options.ChannelCapacity;
            opts.FullMode = options.FullMode;
            opts.HandlerTimeout = options.HandlerTimeout;
        });

        var builder = new OtelEventsSubscriptionBuilder(services);
        configureSubscriptions?.Invoke(builder);

        // Create the bounded channel with configured capacity and backpressure policy.
        // The itemDropped callback fires whenever the channel drops an item in Drop* modes,
        // ensuring the channel_full counter is accurate regardless of FullMode.
        var channel = Channel.CreateBounded<DispatchItem>(
            new BoundedChannelOptions(options.ChannelCapacity)
            {
                FullMode = options.FullMode,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            },
            itemDropped: _ => SubscriptionMetrics.ChannelFull.Add(1));

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
