// <copyright file="IStateGraph.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health;

/// <summary>
/// Immutable directed graph of valid health state transitions.
/// </summary>
public interface IStateGraph
{
    /// <summary>
    /// Gets the initial state for new dependencies.
    /// </summary>
    HealthState InitialState { get; }

    /// <summary>
    /// Returns all valid transitions from the given state.
    /// </summary>
    /// <param name="state">The source state to query transitions from.</param>
    /// <returns>All transitions originating from the given state.</returns>
    IReadOnlyList<StateTransition> GetTransitionsFrom(HealthState state);

    /// <summary>
    /// Gets all states in the graph.
    /// </summary>
    IReadOnlySet<HealthState> AllStates { get; }
}
