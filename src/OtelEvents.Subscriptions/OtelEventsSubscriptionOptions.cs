using System.Threading.Channels;

namespace OtelEvents.Subscriptions;

/// <summary>
/// Configuration options for <see cref="OtelEventsSubscriptionProcessor"/>
/// and the background dispatch loop.
/// </summary>
public sealed class OtelEventsSubscriptionOptions
{
    /// <summary>
    /// Gets or sets the bounded channel capacity for event dispatch.
    /// When the channel is full, the <see cref="FullMode"/> policy determines behavior.
    /// Must be greater than zero.
    /// Default: <c>1024</c>.
    /// </summary>
    public int ChannelCapacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the backpressure policy when the channel is full.
    /// Default: <see cref="BoundedChannelFullMode.DropWrite"/> — events are silently dropped
    /// and the <c>otel_events.subscription.channel_full</c> counter is incremented.
    /// </summary>
    /// <remarks>
    /// <see cref="BoundedChannelFullMode.DropOldest"/> causes <c>TryWrite</c> to always
    /// return <c>true</c>, which prevents the <c>channel_full</c> counter from firing.
    /// Use <see cref="BoundedChannelFullMode.DropWrite"/> (the default) for accurate metering.
    /// </remarks>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropWrite;

    /// <summary>
    /// Gets or sets the maximum time allowed for a single handler invocation.
    /// If a handler does not complete within this timeout, its invocation is cancelled
    /// and the <c>otel_events.subscription.handler_timeouts</c> counter is incremented.
    /// Default: <c>30 seconds</c>.
    /// </summary>
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the options and throws if any values are invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="ChannelCapacity"/> is less than or equal to zero
    /// or when <see cref="HandlerTimeout"/> is not positive.
    /// </exception>
    internal void Validate()
    {
        if (ChannelCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ChannelCapacity),
                ChannelCapacity,
                "Channel capacity must be greater than zero.");
        }

        if (HandlerTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HandlerTimeout),
                HandlerTimeout,
                "Handler timeout must be a positive duration.");
        }
    }
}
