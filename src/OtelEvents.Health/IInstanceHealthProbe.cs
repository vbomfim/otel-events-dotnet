// <copyright file="IInstanceHealthProbe.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health;

/// <summary>
/// Instance discovery and probing for quorum evaluation.
/// Implementations report the health status of individual instances
/// in a backend pool (e.g., gRPC subchannels, load-balanced replicas).
/// </summary>
public interface IInstanceHealthProbe
{
    /// <summary>
    /// Probes all known instances and returns their health results.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the probing operation.</param>
    /// <returns>A read-only list of health results, one per discovered instance.</returns>
    Task<IReadOnlyList<InstanceHealthResult>> ProbeAllAsync(CancellationToken cancellationToken = default);
}
