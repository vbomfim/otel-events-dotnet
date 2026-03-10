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
    /// Default: <c>1024</c>.
    /// </summary>
    public int ChannelCapacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the backpressure policy when the channel is full.
    /// Default: <see cref="BoundedChannelFullMode.DropOldest"/>.
    /// </summary>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;
}
