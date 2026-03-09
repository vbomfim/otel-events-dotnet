using Microsoft.Extensions.Logging;

namespace OtelEvents.HealthChecks;

/// <summary>
/// LoggerMessage-based event source for health check events.
/// Uses high-performance source-generated logging via <see cref="LoggerMessageAttribute"/>.
/// </summary>
/// <remarks>
/// Event IDs follow the spec allocation: 10401–10499 for OtelEvents.HealthChecks.
/// </remarks>
internal static partial class OtelEventsHealthCheckEventSource
{
    /// <summary>
    /// Emits <c>health.check.executed</c> — fired for every health check in each poll cycle.
    /// </summary>
    [LoggerMessage(
        EventId = 10401,
        EventName = "health.check.executed",
        Level = LogLevel.Debug,
        Message = "Health check {healthComponent} completed with {healthStatus} in {healthDurationMs}ms {healthDescription}")]
    internal static partial void HealthCheckExecuted(
        this ILogger logger,
        string healthComponent,
        string healthStatus,
        double healthDurationMs,
        string? healthDescription = null);

    /// <summary>
    /// Emits <c>health.state.changed</c> — fired only on status transitions.
    /// </summary>
    [LoggerMessage(
        EventId = 10402,
        EventName = "health.state.changed",
        Level = LogLevel.Warning,
        Message = "Health state changed: {healthComponent} {healthPreviousStatus} → {healthStatus} in {healthDurationMs}ms: {healthDescription}")]
    internal static partial void HealthStateChanged(
        this ILogger logger,
        string healthComponent,
        string healthPreviousStatus,
        string healthStatus,
        double healthDurationMs,
        string? healthDescription = null);

    /// <summary>
    /// Emits <c>health.report.completed</c> — fired after all checks in a cycle complete.
    /// </summary>
    [LoggerMessage(
        EventId = 10403,
        EventName = "health.report.completed",
        Level = LogLevel.Debug,
        Message = "Health report completed: {healthOverallStatus} ({healthTotalChecks} checks) in {healthDurationMs}ms")]
    internal static partial void HealthReportCompleted(
        this ILogger logger,
        string healthOverallStatus,
        int healthTotalChecks,
        double healthDurationMs);

    /// <summary>
    /// Emits a warning when the bounded state dictionary reaches capacity.
    /// </summary>
    [LoggerMessage(
        EventId = 10499,
        EventName = "health.state.capacity.exceeded",
        Level = LogLevel.Warning,
        Message = "Health check state tracking capacity exceeded ({capacity}). New component '{healthComponent}' will not be tracked for state changes. This indicates a configuration issue — health check names should be static and finite.")]
    internal static partial void StateCapacityExceeded(
        this ILogger logger,
        int capacity,
        string healthComponent);
}
