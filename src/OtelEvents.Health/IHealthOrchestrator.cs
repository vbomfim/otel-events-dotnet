// <copyright file="IHealthOrchestrator.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health;

/// <summary>
/// Top-level coordinator that owns all <see cref="IDependencyMonitor"/> instances.
/// Produces aggregate <see cref="HealthReport"/> and <see cref="ReadinessReport"/>
/// for probe endpoints and provides a read-only health state for shutdown decisions.
/// </summary>
public interface IHealthOrchestrator : IHealthStateReader, IHealthReportProvider, ISignalRecorder
{
    /// <summary>
    /// Gets a specific dependency's monitor, or <c>null</c> if the dependency is not registered.
    /// </summary>
    /// <param name="id">The dependency identifier.</param>
    /// <returns>The monitor for the dependency, or <c>null</c>.</returns>
    IDependencyMonitor? GetMonitor(DependencyId id);

    /// <summary>
    /// Gets all registered dependency identifiers.
    /// </summary>
    IReadOnlyCollection<DependencyId> RegisteredDependencies { get; }
}
