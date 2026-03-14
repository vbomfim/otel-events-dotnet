// <copyright file="ITimerBudgetValidator.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health;

/// <summary>
/// Validates that all registered component policies have timer budgets
/// consistent with Kubernetes and ASP.NET hosting constraints.
/// Intended for startup-time validation to surface misconfiguration early.
/// </summary>
public interface ITimerBudgetValidator
{
    /// <summary>
    /// Validates all registered component policies against Kubernetes timing constraints.
    /// Returns a list of warnings; an empty list means all checks passed.
    /// </summary>
    /// <param name="policies">The per-dependency health policies to validate.</param>
    /// <param name="options">Kubernetes and hosting timing constraints.</param>
    /// <returns>A read-only list of timer budget warnings. Empty when all checks pass.</returns>
    IReadOnlyList<TimerBudgetWarning> Validate(
        IReadOnlyDictionary<DependencyId, HealthPolicy> policies,
        TimerBudgetOptions options);
}
