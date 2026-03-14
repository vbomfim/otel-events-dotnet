// <copyright file="HealthSignalBridge.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using System.Globalization;
using OtelEvents.Health.Contracts;
using OtelEvents.Schema.Models;
using OtelEvents.Subscriptions;

namespace OtelEvents.Health;

/// <summary>
/// Auto-subscribe bridge between otel-events subscriptions and the Health state machine.
/// <para>
/// At startup, reads <see cref="ComponentDefinition"/> instances (parsed from YAML)
/// and registers otel-events subscriptions for each signal mapping. When an event
/// fires and matches the component's filters, the bridge classifies the outcome,
/// extracts latency, and records a <see cref="HealthSignal"/> via <see cref="ISignalRecorder"/>.
/// </para>
/// </summary>
/// <remarks>
/// The bridge uses late binding for <see cref="ISignalRecorder"/> to support DI timing:
/// subscription lambdas are registered at configuration time, but <see cref="ISignalRecorder"/>
/// is only available after the service provider is built. Call <see cref="Bind"/> to set
/// the recorder. Events received before binding are silently dropped.
/// </remarks>
internal sealed class HealthSignalBridge
{
    private readonly IReadOnlyList<ComponentDefinition> _components;
    private volatile ISignalRecorder? _recorder;

    /// <summary>
    /// Initializes a new instance for late binding via <see cref="Bind"/>.
    /// Used in production DI where <see cref="ISignalRecorder"/> is not yet available.
    /// </summary>
    /// <param name="components">Component definitions from parsed YAML.</param>
    internal HealthSignalBridge(IReadOnlyList<ComponentDefinition> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components;
    }

    /// <summary>
    /// Initializes a new instance with a pre-bound recorder. Used for unit testing.
    /// </summary>
    /// <param name="components">Component definitions from parsed YAML.</param>
    /// <param name="recorder">The signal recorder to use immediately.</param>
    internal HealthSignalBridge(IReadOnlyList<ComponentDefinition> components, ISignalRecorder recorder)
        : this(components)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        _recorder = recorder;
    }

    /// <summary>
    /// Binds the <see cref="ISignalRecorder"/> for signal recording.
    /// Called after the DI container is built.
    /// </summary>
    /// <param name="recorder">The signal recorder resolved from DI.</param>
    internal void Bind(ISignalRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        _recorder = recorder;
    }

    /// <summary>
    /// Registers otel-events subscriptions for all component signal mappings.
    /// Each signal mapping becomes a subscription that routes matching events
    /// to <see cref="HandleSignal"/> for classification and recording.
    /// </summary>
    /// <param name="builder">The subscription builder to register handlers with.</param>
    internal void RegisterSubscriptions(OtelEventsSubscriptionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var component in _components)
        {
            var depId = new DependencyId(component.Name);

            foreach (var signal in component.Signals)
            {
                var mapping = signal; // capture for closure
                builder.On(signal.Event, (ctx, ct) =>
                {
                    HandleSignal(depId, mapping, ctx);
                    return Task.CompletedTask;
                });
            }
        }
    }

    /// <summary>
    /// Processes a single event against a signal mapping.
    /// Checks match filters, classifies outcome, extracts latency,
    /// and records the health signal if all criteria are met.
    /// </summary>
    /// <param name="depId">The dependency identifier for the owning component.</param>
    /// <param name="mapping">The signal mapping that triggered this handler.</param>
    /// <param name="ctx">The event context snapshot.</param>
    internal void HandleSignal(DependencyId depId, SignalMapping mapping, OtelEventContext ctx)
    {
        var recorder = _recorder;
        if (recorder is null)
        {
            return; // Not yet bound — silently drop
        }

        if (!MatchesFilters(mapping.Match, ctx))
        {
            return;
        }

        var outcome = ClassifyOutcome(ctx.EventName);
        if (outcome is null)
        {
            return; // Unclassifiable (e.g., *.started) — skip
        }

        var latency = ExtractLatency(ctx);

        recorder.RecordSignal(depId, new HealthSignal(
            ctx.Timestamp,
            depId,
            outcome.Value,
            latency));
    }

    /// <summary>
    /// Classifies an event name suffix into a <see cref="SignalOutcome"/>.
    /// Returns <c>null</c> for unrecognized or skippable suffixes (e.g., <c>*.started</c>).
    /// </summary>
    /// <param name="eventName">The full event name (e.g., "http.request.failed").</param>
    /// <returns>The classified outcome, or <c>null</c> if the event should be skipped.</returns>
    internal static SignalOutcome? ClassifyOutcome(string eventName)
    {
        if (eventName.EndsWith(".failed", StringComparison.OrdinalIgnoreCase))
        {
            return SignalOutcome.Failure;
        }

        if (eventName.EndsWith(".throttled", StringComparison.OrdinalIgnoreCase))
        {
            return SignalOutcome.Failure;
        }

        if (eventName.EndsWith(".completed", StringComparison.OrdinalIgnoreCase))
        {
            return SignalOutcome.Success;
        }

        if (eventName.EndsWith(".executed", StringComparison.OrdinalIgnoreCase))
        {
            return SignalOutcome.Success;
        }

        // *.started is a timing marker — skip, don't record
        // Unknown suffixes are also skipped to avoid false signals
        return null;
    }

    /// <summary>
    /// Checks whether all match filters in the signal mapping are satisfied
    /// by the event context attributes. All filters must match (AND logic).
    /// </summary>
    /// <param name="match">The match filters from the signal mapping.</param>
    /// <param name="ctx">The event context to check against.</param>
    /// <returns><c>true</c> if all filters match (or no filters defined); <c>false</c> otherwise.</returns>
    internal static bool MatchesFilters(
        IReadOnlyDictionary<string, string> match,
        OtelEventContext ctx)
    {
        if (match.Count == 0)
        {
            return true;
        }

        foreach (var (key, pattern) in match)
        {
            var value = ctx.GetAttribute<string>(key);
            if (value is null)
            {
                return false;
            }

            if (!MatchesPattern(value, pattern))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Matches a value against a pattern. Supports exact match and trailing <c>*</c> wildcard.
    /// </summary>
    /// <param name="value">The actual attribute value from the event.</param>
    /// <param name="pattern">The pattern from the signal mapping (e.g., "/api/orders/*").</param>
    /// <returns><c>true</c> if the value matches the pattern.</returns>
    internal static bool MatchesPattern(string value, string pattern)
    {
        if (pattern.EndsWith('*'))
        {
            var prefix = pattern[..^1];
            return value.StartsWith(prefix, StringComparison.Ordinal);
        }

        return string.Equals(value, pattern, StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts latency from the event context's <c>durationMs</c> attribute.
    /// Supports <see cref="double"/>, <see cref="long"/>, <see cref="int"/>,
    /// <see cref="float"/>, and parseable <see cref="string"/> values.
    /// </summary>
    /// <param name="ctx">The event context to extract latency from.</param>
    /// <returns>The latency as <see cref="TimeSpan"/>, or <c>null</c> if not present or unparseable.</returns>
    internal static TimeSpan? ExtractLatency(OtelEventContext ctx)
    {
        if (!ctx.Attributes.TryGetValue("durationMs", out var val))
        {
            return null;
        }

        return val switch
        {
            double d => TimeSpan.FromMilliseconds(d),
            long l => TimeSpan.FromMilliseconds(l),
            int i => TimeSpan.FromMilliseconds(i),
            float f => TimeSpan.FromMilliseconds(f),
            string s when double.TryParse(s, CultureInfo.InvariantCulture, out var parsed)
                => TimeSpan.FromMilliseconds(parsed),
            _ => null,
        };
    }
}
