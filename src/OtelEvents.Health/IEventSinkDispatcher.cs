// <copyright file="IEventSinkDispatcher.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health;

/// <summary>
/// Fan-out dispatcher that sends health events to all registered
/// <see cref="IHealthEventSink"/> implementations.
/// <para>
/// <strong>Error isolation:</strong> Each sink call is wrapped in try-catch —
/// one failing sink does not block other sinks or the calling health evaluation.
/// </para>
/// <para>
/// <strong>Rate limiting:</strong> Configurable max events per second per sink
/// to prevent event storm amplification (Security Finding #12).
/// </para>
/// <para>
/// <strong>Timeout:</strong> Each sink call is subject to a configurable timeout
/// (default 5 s) to prevent slow sinks from blocking dispatch.
/// </para>
/// </summary>
public interface IEventSinkDispatcher
{
    /// <summary>
    /// Dispatches a dependency health state transition event to all registered sinks.
    /// </summary>
    /// <param name="healthEvent">The state transition event to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all sinks have been invoked (or timed out).</returns>
    Task DispatchAsync(HealthEvent healthEvent, CancellationToken ct = default);

    /// <summary>
    /// Dispatches a tenant health status change event to all registered sinks.
    /// </summary>
    /// <param name="tenantEvent">The tenant health change event to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all sinks have been invoked (or timed out).</returns>
    Task DispatchTenantEventAsync(TenantHealthEvent tenantEvent, CancellationToken ct = default);
}
