// <copyright file="IPolicyEvaluator.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health;

/// <summary>
/// Pure-function evaluator: given signals + policy → health assessment.
/// Stateless and side-effect free.
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>
    /// Evaluates the given signals against a health policy to produce an assessment.
    /// </summary>
    /// <param name="signals">The signals within the evaluation window.</param>
    /// <param name="policy">The health policy to evaluate against.</param>
    /// <param name="currentState">The current health state of the dependency.</param>
    /// <param name="evaluatedAt">The point in time at which evaluation occurs.</param>
    /// <returns>A health assessment with the recommended state.</returns>
    HealthAssessment Evaluate(
        IReadOnlyList<HealthSignal> signals,
        HealthPolicy policy,
        HealthState currentState,
        DateTimeOffset evaluatedAt);
}
