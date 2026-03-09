using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Exporter.Json;

/// <summary>
/// OTEL <see cref="BaseProcessor{T}"/> that rate-limits log records by event category.
/// Events exceeding the configured rate limit are dropped (not forwarded to the
/// inner processor). Supports per-event-name rate limits with exact and wildcard matching.
/// </summary>
/// <remarks>
/// This processor wraps an inner <see cref="BaseProcessor{T}"/> and conditionally
/// forwards records. It must be configured to wrap the downstream processor
/// (typically a batch export processor) to prevent rate-limited events from
/// reaching exporters.
/// </remarks>
public sealed class AllRateLimitProcessor : BaseProcessor<LogRecord>
{
    private static readonly Meter SelfMeter = new("all.processor.rate_limit");

    private readonly AllRateLimitOptions _options;
    private readonly BaseProcessor<LogRecord> _innerProcessor;
    private readonly TimeProvider _timeProvider;
    private readonly Counter<long> _eventsDropped;
    private readonly Counter<long> _eventsPassed;
    private readonly ConcurrentDictionary<string, SlidingWindowCounter> _counters = new();
    private readonly Dictionary<string, int> _exactLimits;
    private readonly List<KeyValuePair<string, int>> _wildcardLimits;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="AllRateLimitProcessor"/>
    /// that wraps the specified inner processor.
    /// </summary>
    /// <param name="options">Rate limit configuration options.</param>
    /// <param name="innerProcessor">
    /// The downstream processor to forward passing records to.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="innerProcessor"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="AllRateLimitOptions.DefaultMaxEventsPerWindow"/> or
    /// <see cref="AllRateLimitOptions.Window"/> have invalid values.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="AllRateLimitOptions.EventLimits"/> contains
    /// empty keys, a bare wildcard <c>"*"</c>, or negative limit values.
    /// </exception>
    public AllRateLimitProcessor(
        AllRateLimitOptions options,
        BaseProcessor<LogRecord> innerProcessor)
        : this(options, innerProcessor, TimeProvider.System)
    {
    }

    /// <summary>
    /// Internal constructor accepting a <see cref="TimeProvider"/> for deterministic testing.
    /// </summary>
    internal AllRateLimitProcessor(
        AllRateLimitOptions options,
        BaseProcessor<LogRecord> innerProcessor,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(innerProcessor);
        ArgumentNullException.ThrowIfNull(timeProvider);

        ValidateOptions(options);

        _options = options;
        _innerProcessor = innerProcessor;
        _timeProvider = timeProvider;

        _eventsDropped = SelfMeter.CreateCounter<long>(
            "all.processor.rate_limit.events_dropped",
            description: "Total events dropped by rate limiter");

        _eventsPassed = SelfMeter.CreateCounter<long>(
            "all.processor.rate_limit.events_passed",
            description: "Total events passed by rate limiter");

        // Pre-partition limits into exact and wildcard for fast lookup
        _exactLimits = [];
        _wildcardLimits = [];

        foreach (var kvp in _options.EventLimits)
        {
            if (kvp.Key.EndsWith('*'))
            {
                _wildcardLimits.Add(
                    new KeyValuePair<string, int>(
                        kvp.Key[..^1], // strip trailing '*'
                        kvp.Value));
            }
            else
            {
                _exactLimits[kvp.Key] = kvp.Value;
            }
        }

        // Sort wildcards by prefix length descending — longest (most-specific) first
        _wildcardLimits.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
    }

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        var limit = GetRateLimit(data);

        if (limit == 0)
        {
            // No rate limit — pass through unconditionally
            _eventsPassed.Add(1);
            _innerProcessor.OnEnd(data);
            return;
        }

        var eventName = data.EventId.Name ?? string.Empty;
        var counter = _counters.GetOrAdd(
            eventName,
            _ => new SlidingWindowCounter(_options.Window, _timeProvider));

        if (counter.TryIncrement(limit))
        {
            _eventsPassed.Add(1);
            _innerProcessor.OnEnd(data);
        }
        else
        {
            _eventsDropped.Add(1);
        }
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
        => _innerProcessor.ForceFlush(timeoutMilliseconds);

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
        => _innerProcessor.Shutdown(timeoutMilliseconds);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _innerProcessor.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Validates rate limit options at construction time.
    /// </summary>
    private static void ValidateOptions(AllRateLimitOptions options)
    {
        if (options.DefaultMaxEventsPerWindow < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.DefaultMaxEventsPerWindow,
                "DefaultMaxEventsPerWindow must be non-negative (0 = unlimited).");
        }

        if (options.Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Window,
                "Window must be a positive duration.");
        }

        foreach (var kvp in options.EventLimits)
        {
            if (string.IsNullOrEmpty(kvp.Key))
            {
                throw new ArgumentException(
                    "EventLimits keys must not be empty.",
                    nameof(options));
            }

            if (kvp.Key == "*")
            {
                throw new ArgumentException(
                    "Bare wildcard \"*\" is not allowed in EventLimits. "
                    + "Use DefaultMaxEventsPerWindow to set a global rate limit, "
                    + "or use a qualified prefix wildcard like \"db.query.*\".",
                    nameof(options));
            }

            if (kvp.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    kvp.Value,
                    $"EventLimits value for \"{kvp.Key}\" must be non-negative (0 = unlimited).");
            }
        }
    }

    /// <summary>
    /// Resolves the effective rate limit for a given record,
    /// considering per-event-name overrides.
    /// Exact match takes precedence over wildcard match.
    /// Wildcards are evaluated longest-prefix-first for deterministic,
    /// most-specific matching.
    /// Falls back to global <see cref="AllRateLimitOptions.DefaultMaxEventsPerWindow"/>.
    /// </summary>
    internal int GetRateLimit(LogRecord record)
    {
        var eventName = record.EventId.Name;

        if (string.IsNullOrEmpty(eventName))
        {
            return _options.DefaultMaxEventsPerWindow;
        }

        // Exact match first (O(1) dictionary lookup)
        if (_exactLimits.TryGetValue(eventName, out var exactLimit))
        {
            return exactLimit;
        }

        // Wildcard match (prefix-based, sorted longest-first for specificity)
        foreach (var wildcard in _wildcardLimits)
        {
            if (eventName.StartsWith(wildcard.Key, StringComparison.Ordinal))
            {
                return wildcard.Value;
            }
        }

        return _options.DefaultMaxEventsPerWindow;
    }
}
