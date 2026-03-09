using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Exporter.Json;

/// <summary>
/// OTEL <see cref="BaseProcessor{T}"/> that probabilistically samples log records.
/// Supports head sampling (random probability at arrival) and tail sampling
/// (error-aware: always sample errors, sample non-errors at configured rate).
/// Per-event-name sampling rate overrides with exact and wildcard matching.
/// </summary>
/// <remarks>
/// This processor wraps an inner <see cref="BaseProcessor{T}"/> and conditionally
/// forwards records based on a random probability check. It must be configured
/// to wrap the downstream processor (typically a batch export processor) to
/// prevent dropped events from reaching exporters.
/// </remarks>
public sealed class AllSamplingProcessor : BaseProcessor<LogRecord>
{
    private static readonly Meter SelfMeter = new("all.processor.sampling");

    private readonly AllSamplingOptions _options;
    private readonly BaseProcessor<LogRecord> _innerProcessor;
    private readonly Counter<long> _eventsSampled;
    private readonly Counter<long> _eventsDropped;
    private readonly Dictionary<string, double> _exactRates;
    private readonly List<KeyValuePair<string, double>> _wildcardRates;

    [ThreadStatic]
    private static Random? t_random;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="AllSamplingProcessor"/>
    /// that wraps the specified inner processor.
    /// </summary>
    /// <param name="options">Sampling configuration options.</param>
    /// <param name="innerProcessor">
    /// The downstream processor to forward sampled records to.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="innerProcessor"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="AllSamplingOptions.DefaultSamplingRate"/> is outside [0.0, 1.0],
    /// <see cref="AllSamplingOptions.Strategy"/> is not a defined enum value, or
    /// <see cref="AllSamplingOptions.ErrorMinLevel"/> is not a defined enum value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="AllSamplingOptions.EventRates"/> contains empty keys,
    /// a bare wildcard <c>"*"</c>, or rate values outside [0.0, 1.0].
    /// </exception>
    public AllSamplingProcessor(
        AllSamplingOptions options,
        BaseProcessor<LogRecord> innerProcessor)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(innerProcessor);

        ValidateOptions(options);

        _options = options;
        _innerProcessor = innerProcessor;

        _eventsSampled = SelfMeter.CreateCounter<long>(
            "all.processor.sampling.events_sampled",
            description: "Total events sampled (forwarded) by sampling processor");

        _eventsDropped = SelfMeter.CreateCounter<long>(
            "all.processor.sampling.events_dropped",
            description: "Total events dropped by sampling processor");

        // Pre-partition rates into exact and wildcard for fast lookup
        _exactRates = [];
        _wildcardRates = [];

        foreach (var kvp in _options.EventRates)
        {
            if (kvp.Key.EndsWith('*'))
            {
                _wildcardRates.Add(
                    new KeyValuePair<string, double>(
                        kvp.Key[..^1], // strip trailing '*'
                        kvp.Value));
            }
            else
            {
                _exactRates[kvp.Key] = kvp.Value;
            }
        }

        // Sort wildcards by prefix length descending — longest (most-specific) first
        _wildcardRates.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
    }

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        if (ShouldSample(data))
        {
            _eventsSampled.Add(1);
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
    /// Determines whether a log record should be sampled based on the
    /// configured strategy, sampling rate, and per-event overrides.
    /// </summary>
    internal bool ShouldSample(LogRecord record)
    {
        // Tail sampling: always sample errors when configured
        if (_options.Strategy == AllSamplingStrategy.Tail
            && _options.AlwaysSampleErrors
            && record.LogLevel >= _options.ErrorMinLevel)
        {
            return true;
        }

        var rate = GetSamplingRate(record);

        // Fast path: rate 1.0 means always sample
        if (rate >= 1.0)
        {
            return true;
        }

        // Fast path: rate 0.0 means never sample
        if (rate <= 0.0)
        {
            return false;
        }

        return NextDouble() < rate;
    }

    /// <summary>
    /// Resolves the effective sampling rate for a given record,
    /// considering per-event-name overrides.
    /// Exact match takes precedence over wildcard match.
    /// Wildcards are evaluated longest-prefix-first for deterministic,
    /// most-specific matching.
    /// Falls back to global <see cref="AllSamplingOptions.DefaultSamplingRate"/>.
    /// </summary>
    internal double GetSamplingRate(LogRecord record)
    {
        var eventName = record.EventId.Name;

        if (string.IsNullOrEmpty(eventName))
        {
            return _options.DefaultSamplingRate;
        }

        // Exact match first (O(1) dictionary lookup)
        if (_exactRates.TryGetValue(eventName, out var exactRate))
        {
            return exactRate;
        }

        // Wildcard match (prefix-based, sorted longest-first for specificity)
        foreach (var wildcard in _wildcardRates)
        {
            if (eventName.StartsWith(wildcard.Key, StringComparison.Ordinal))
            {
                return wildcard.Value;
            }
        }

        return _options.DefaultSamplingRate;
    }

    /// <summary>
    /// Validates sampling options at construction time.
    /// </summary>
    private static void ValidateOptions(AllSamplingOptions options)
    {
        if (!Enum.IsDefined(options.Strategy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Strategy,
                "Strategy must be a defined AllSamplingStrategy enum value.");
        }

        if (options.DefaultSamplingRate < 0.0 || options.DefaultSamplingRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.DefaultSamplingRate,
                "DefaultSamplingRate must be between 0.0 and 1.0.");
        }

        if (!Enum.IsDefined(options.ErrorMinLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.ErrorMinLevel,
                "ErrorMinLevel must be a defined LogLevel enum value.");
        }

        foreach (var kvp in options.EventRates)
        {
            if (string.IsNullOrEmpty(kvp.Key))
            {
                throw new ArgumentException(
                    "EventRates keys must not be empty.",
                    nameof(options));
            }

            if (kvp.Key == "*")
            {
                throw new ArgumentException(
                    "Bare wildcard \"*\" is not allowed in EventRates. "
                    + "Use DefaultSamplingRate to set a global sampling rate, "
                    + "or use a qualified prefix wildcard like \"db.query.*\".",
                    nameof(options));
            }

            if (kvp.Value < 0.0 || kvp.Value > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    kvp.Value,
                    $"EventRates value for \"{kvp.Key}\" must be between 0.0 and 1.0.");
            }
        }
    }

    /// <summary>
    /// Thread-safe random double generation using <see cref="ThreadStaticAttribute"/>.
    /// Each thread gets its own <see cref="Random"/> instance to avoid contention.
    /// </summary>
    /// <remarks>
    /// CA5394 is suppressed because this is probabilistic sampling for telemetry
    /// volume control, not a security-sensitive context. Cryptographic randomness
    /// is unnecessary and would add overhead for no security benefit.
    /// </remarks>
#pragma warning disable CA5394 // Random is not security-sensitive here — sampling is non-security
    private static double NextDouble()
    {
        t_random ??= Random.Shared;
        return t_random.NextDouble();
    }
#pragma warning restore CA5394
}
