using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace All.Exporter.Json;

/// <summary>
/// OTEL <see cref="BaseProcessor{T}"/> that filters log records by severity level.
/// Events below the configured minimum severity are dropped (not forwarded
/// to the inner processor). Supports per-event-name severity overrides
/// with exact and wildcard matching.
/// </summary>
/// <remarks>
/// This processor wraps an inner <see cref="BaseProcessor{T}"/> and conditionally
/// forwards records. It must be configured to wrap the downstream processor
/// (typically a batch export processor) to actually prevent dropped events
/// from reaching exporters.
/// </remarks>
public sealed class AllSeverityFilterProcessor : BaseProcessor<LogRecord>
{
    private static readonly Meter SelfMeter = new("all.processor.severity_filter");

    private readonly AllSeverityFilterOptions _options;
    private readonly BaseProcessor<LogRecord> _innerProcessor;
    private readonly Counter<long> _eventsDropped;
    private readonly Dictionary<string, LogLevel> _exactOverrides;
    private readonly List<KeyValuePair<string, LogLevel>> _wildcardOverrides;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="AllSeverityFilterProcessor"/>
    /// that wraps the specified inner processor.
    /// </summary>
    /// <param name="options">Filter configuration options.</param>
    /// <param name="innerProcessor">
    /// The downstream processor to forward passing records to.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="innerProcessor"/> is null.
    /// </exception>
    public AllSeverityFilterProcessor(
        AllSeverityFilterOptions options,
        BaseProcessor<LogRecord> innerProcessor)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(innerProcessor);

        _options = options;
        _innerProcessor = innerProcessor;

        _eventsDropped = SelfMeter.CreateCounter<long>(
            "all.processor.severity_filter.events_dropped",
            description: "Total events dropped by severity filter");

        // Pre-partition overrides into exact and wildcard for fast lookup
        _exactOverrides = [];
        _wildcardOverrides = [];

        foreach (var kvp in _options.EventNameOverrides)
        {
            if (kvp.Key.EndsWith('*'))
            {
                _wildcardOverrides.Add(
                    new KeyValuePair<string, LogLevel>(
                        kvp.Key[..^1], // strip trailing '*'
                        kvp.Value));
            }
            else
            {
                _exactOverrides[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        var minLevel = GetMinLogLevel(data);

        if (!ShouldProcess(data.LogLevel, minLevel))
        {
            _eventsDropped.Add(1);
            return;
        }

        _innerProcessor.OnEnd(data);
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
    /// Determines if a log record's severity passes the filter.
    /// </summary>
    private static bool ShouldProcess(LogLevel recordLevel, LogLevel minLevel)
    {
        // LogLevel.None means the event should never be logged
        if (recordLevel == LogLevel.None)
        {
            return false;
        }

        return recordLevel >= minLevel;
    }

    /// <summary>
    /// Resolves the effective minimum log level for a given record,
    /// considering per-event-name overrides.
    /// Exact match takes precedence over wildcard match.
    /// Falls back to global <see cref="AllSeverityFilterOptions.MinSeverity"/>.
    /// </summary>
    private LogLevel GetMinLogLevel(LogRecord record)
    {
        var eventName = record.EventId.Name;

        if (string.IsNullOrEmpty(eventName))
        {
            return _options.MinSeverity;
        }

        // Exact match first (O(1) dictionary lookup)
        if (_exactOverrides.TryGetValue(eventName, out var exactLevel))
        {
            return exactLevel;
        }

        // Wildcard match (prefix-based, O(n) scan over wildcard rules)
        foreach (var wildcard in _wildcardOverrides)
        {
            if (eventName.StartsWith(wildcard.Key, StringComparison.Ordinal))
            {
                return wildcard.Value;
            }
        }

        return _options.MinSeverity;
    }
}
