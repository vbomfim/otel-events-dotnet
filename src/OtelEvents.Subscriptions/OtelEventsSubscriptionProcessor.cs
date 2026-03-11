using System.Threading.Channels;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Subscriptions;

/// <summary>
/// OTEL <see cref="BaseProcessor{T}"/> that snapshots matching log records
/// and dispatches them to a background <see cref="Channel{T}"/> for handler execution.
/// <para>
/// This processor never blocks the OTEL pipeline. Events are matched against
/// registered subscriptions (exact match first, then wildcards sorted by longest
/// prefix), snapshotted into <see cref="OtelEventContext"/>, and written to a
/// bounded channel. A separate <see cref="OtelEventsSubscriptionDispatcher"/>
/// reads from the channel and invokes handlers.
/// </para>
/// </summary>
public sealed class OtelEventsSubscriptionProcessor : BaseProcessor<LogRecord>
{
    private readonly Channel<DispatchItem> _channel;
    private readonly Dictionary<string, List<SubscriptionRegistration>> _exactSubscriptions;
    private readonly List<WildcardEntry> _wildcardSubscriptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="OtelEventsSubscriptionProcessor"/>.
    /// </summary>
    /// <param name="channel">The bounded channel to write dispatch items to.</param>
    /// <param name="registrations">The subscription registrations from the builder.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="channel"/> or <paramref name="registrations"/> is null.
    /// </exception>
    internal OtelEventsSubscriptionProcessor(
        Channel<DispatchItem> channel,
        IReadOnlyList<SubscriptionRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(registrations);

        _channel = channel;

        // Pre-partition registrations into exact and wildcard for fast lookup
        _exactSubscriptions = [];
        _wildcardSubscriptions = [];

        foreach (var reg in registrations)
        {
            if (reg.IsWildcard)
            {
                _wildcardSubscriptions.Add(new WildcardEntry(reg.WildcardPrefix!, reg));
            }
            else
            {
                if (!_exactSubscriptions.TryGetValue(reg.EventPattern, out var list))
                {
                    list = [];
                    _exactSubscriptions[reg.EventPattern] = list;
                }

                list.Add(reg);
            }
        }

        // Sort wildcards by prefix length descending — longest (most-specific) first
        _wildcardSubscriptions.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
    }

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        var eventName = data.EventId.Name;

        if (string.IsNullOrEmpty(eventName))
        {
            return;
        }

        var matchingRegistrations = GetMatchingRegistrations(eventName);

        if (matchingRegistrations.Count == 0)
        {
            return;
        }

        // Snapshot the LogRecord before it gets recycled by the OTEL pipeline
        var context = OtelEventContext.FromLogRecord(data);
        var item = new DispatchItem(context, matchingRegistrations);

        // TryWrite is non-blocking — never blocks the OTEL pipeline.
        // When the channel is full (DropWrite mode), TryWrite returns false.
        // We increment the channel_full counter to track dropped events.
        if (!_channel.Writer.TryWrite(item))
        {
            SubscriptionMetrics.ChannelFull.Add(1);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Finds all registrations matching the given event name.
    /// Exact matches are checked first, then wildcards (longest-prefix-first).
    /// Multiple registrations can match the same event.
    /// </summary>
    private List<SubscriptionRegistration> GetMatchingRegistrations(string eventName)
    {
        var matches = new List<SubscriptionRegistration>();

        // Exact match (O(1) dictionary lookup)
        if (_exactSubscriptions.TryGetValue(eventName, out var exactList))
        {
            matches.AddRange(exactList);
        }

        // Wildcard match (prefix-based, sorted longest-first for specificity)
        foreach (var entry in _wildcardSubscriptions)
        {
            if (eventName.StartsWith(entry.Prefix, StringComparison.Ordinal))
            {
                matches.Add(entry.Registration);
            }
        }

        return matches;
    }

    /// <summary>
    /// Pre-sorted wildcard entry for fast prefix matching.
    /// </summary>
    private readonly record struct WildcardEntry(string Prefix, SubscriptionRegistration Registration);
}

/// <summary>
/// Item placed on the dispatch channel containing the event context
/// and the list of matching subscription registrations.
/// </summary>
internal sealed class DispatchItem
{
    public OtelEventContext Context { get; }
    public IReadOnlyList<SubscriptionRegistration> Registrations { get; }

    public DispatchItem(OtelEventContext context, IReadOnlyList<SubscriptionRegistration> registrations)
    {
        Context = context;
        Registrations = registrations;
    }
}
