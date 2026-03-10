using System.Diagnostics.Metrics;

namespace OtelEvents.Subscriptions;

/// <summary>
/// Self-telemetry counters for the subscription processor and dispatch loop.
/// Uses OTEL's native <see cref="Meter"/> for self-monitoring.
/// </summary>
internal static class SubscriptionMetrics
{
    internal static readonly Meter Meter = new("otel_events.subscription", "1.0.0");

    internal static readonly Counter<long> EventsDispatched =
        Meter.CreateCounter<long>(
            "otel_events.subscription.events_dispatched",
            description: "Total events dispatched to subscription handlers");

    internal static readonly Counter<long> HandlerErrors =
        Meter.CreateCounter<long>(
            "otel_events.subscription.handler_errors",
            description: "Total errors caught from subscription handlers");

    internal static readonly Counter<long> ChannelFull =
        Meter.CreateCounter<long>(
            "otel_events.subscription.channel_full",
            description: "Total events dropped or displaced due to channel capacity");
}
