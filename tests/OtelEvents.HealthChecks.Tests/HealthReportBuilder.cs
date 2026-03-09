using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OtelEvents.HealthChecks.Tests;

/// <summary>
/// Helper methods for building <see cref="HealthReport"/> instances in tests.
/// </summary>
internal static class HealthReportBuilder
{
    /// <summary>
    /// Creates a <see cref="HealthReport"/> with the specified entries.
    /// </summary>
    public static HealthReport Create(
        params (string Name, HealthStatus Status, TimeSpan Duration, string? Description)[] entries)
    {
        var dict = new Dictionary<string, HealthReportEntry>();

        foreach (var (name, status, duration, description) in entries)
        {
            dict[name] = new HealthReportEntry(
                status: status,
                description: description,
                duration: duration,
                exception: null,
                data: null);
        }

        var totalDuration = entries.Length > 0
            ? TimeSpan.FromMilliseconds(entries.Sum(e => e.Duration.TotalMilliseconds))
            : TimeSpan.Zero;

        return new HealthReport(dict, totalDuration);
    }

    /// <summary>
    /// Creates a simple single-check healthy report.
    /// </summary>
    public static HealthReport CreateHealthy(
        string name = "TestCheck",
        TimeSpan? duration = null,
        string? description = null)
    {
        return Create((name, HealthStatus.Healthy, duration ?? TimeSpan.FromMilliseconds(10), description));
    }

    /// <summary>
    /// Creates a simple single-check degraded report.
    /// </summary>
    public static HealthReport CreateDegraded(
        string name = "TestCheck",
        TimeSpan? duration = null,
        string? description = null)
    {
        return Create((name, HealthStatus.Degraded, duration ?? TimeSpan.FromMilliseconds(50), description));
    }

    /// <summary>
    /// Creates a simple single-check unhealthy report.
    /// </summary>
    public static HealthReport CreateUnhealthy(
        string name = "TestCheck",
        TimeSpan? duration = null,
        string? description = null)
    {
        return Create((name, HealthStatus.Unhealthy, duration ?? TimeSpan.FromMilliseconds(100), description));
    }
}
