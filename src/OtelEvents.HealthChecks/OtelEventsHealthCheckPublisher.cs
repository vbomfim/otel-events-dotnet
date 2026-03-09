using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace OtelEvents.HealthChecks;

/// <summary>
/// <see cref="IHealthCheckPublisher"/> that emits schema-defined events for health check
/// execution results and state changes.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton via <see cref="OtelEventsHealthCheckExtensions.AddOtelEventsHealthChecks"/>.
/// Receives <see cref="HealthReport"/> after each poll cycle and emits:
/// </para>
/// <list type="bullet">
///   <item><c>health.check.executed</c> (10401) — per health check, every poll</item>
///   <item><c>health.state.changed</c> (10402) — only on status transitions</item>
///   <item><c>health.report.completed</c> (10403) — aggregate per cycle</item>
/// </list>
/// </remarks>
internal sealed class OtelEventsHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<OtelEventsHealthCheckPublisher> _logger;
    private readonly OtelEventsHealthCheckOptions _options;
    private readonly ConcurrentDictionary<string, HealthStatus> _previousStates = new();

    /// <summary>
    /// Maximum number of unique health check components to track for state changes.
    /// Exceeding this limit indicates a configuration issue with dynamically generated names.
    /// </summary>
    internal const int MaxTrackedComponents = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtelEventsHealthCheckPublisher"/> class.
    /// </summary>
    /// <param name="logger">Logger for emitting structured health check events.</param>
    /// <param name="options">Configuration options controlling event emission.</param>
    public OtelEventsHealthCheckPublisher(
        ILogger<OtelEventsHealthCheckPublisher> logger,
        OtelEventsHealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Publishes health check events from the given <see cref="HealthReport"/>.
    /// </summary>
    /// <param name="report">The health report containing all check results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        foreach (var entry in report.Entries)
        {
            EmitExecutedEvent(entry.Key, entry.Value);
            DetectStateChange(entry.Key, entry.Value);
        }

        EmitReportCompletedEvent(report);

        return Task.CompletedTask;
    }

    private void EmitExecutedEvent(string componentName, HealthReportEntry entry)
    {
        if (!_options.EmitExecutedEvents)
        {
            return;
        }

        if (_options.SuppressHealthyExecutedEvents && entry.Status == HealthStatus.Healthy)
        {
            return;
        }

        _logger.HealthCheckExecuted(
            healthComponent: componentName,
            healthStatus: MapStatus(entry.Status),
            healthDurationMs: entry.Duration.TotalMilliseconds,
            healthDescription: entry.Description);
    }

    private void DetectStateChange(string componentName, HealthReportEntry entry)
    {
        if (!_options.EmitStateChangedEvents)
        {
            return;
        }

        if (_previousStates.TryGetValue(componentName, out var previousStatus))
        {
            if (previousStatus != entry.Status)
            {
                _logger.HealthStateChanged(
                    healthComponent: componentName,
                    healthPreviousStatus: MapStatus(previousStatus),
                    healthStatus: MapStatus(entry.Status),
                    healthDurationMs: entry.Duration.TotalMilliseconds,
                    healthDescription: entry.Description);

                _previousStates[componentName] = entry.Status;
            }
        }
        else
        {
            // First time seeing this component — record initial state, no event
            if (_previousStates.Count >= MaxTrackedComponents)
            {
                _logger.StateCapacityExceeded(MaxTrackedComponents, componentName);
                return;
            }

            _previousStates.TryAdd(componentName, entry.Status);
        }
    }

    private void EmitReportCompletedEvent(HealthReport report)
    {
        if (!_options.EmitReportCompletedEvents)
        {
            return;
        }

        _logger.HealthReportCompleted(
            healthOverallStatus: MapStatus(report.Status),
            healthTotalChecks: report.Entries.Count,
            healthDurationMs: report.TotalDuration.TotalMilliseconds);
    }

    /// <summary>
    /// Maps <see cref="HealthStatus"/> enum to its string representation.
    /// </summary>
    private static string MapStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "Healthy",
        HealthStatus.Degraded => "Degraded",
        HealthStatus.Unhealthy => "Unhealthy",
        _ => status.ToString(),
    };

    /// <summary>
    /// Exposes the tracked state count for testing purposes.
    /// </summary>
    internal int TrackedComponentCount => _previousStates.Count;
}
