// <copyright file="IRecoveryProbeHandler.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health;

/// <summary>
/// User-provided probe logic executed by <see cref="IRecoveryProber"/>
/// to determine whether a dependency in CircuitOpen state has recovered.
/// </summary>
public interface IRecoveryProbeHandler
{
    /// <summary>
    /// Probes the specified dependency to check if it has recovered.
    /// </summary>
    /// <param name="id">The dependency to probe.</param>
    /// <param name="ct">Cancellation token for the probe operation.</param>
    /// <returns><c>true</c> if the dependency has recovered; otherwise, <c>false</c>.</returns>
    Task<bool> ProbeAsync(DependencyId id, CancellationToken ct);
}
