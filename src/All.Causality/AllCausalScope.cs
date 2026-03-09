namespace All.Causality;

/// <summary>
/// Convenience class for creating causal scopes.
/// Sets the parent event ID in <see cref="AllCausalityContext"/> for the duration of the scope,
/// so all events emitted within the scope have the specified parent.
/// </summary>
/// <example>
/// <code>
/// using var scope = AllCausalScope.Begin(eventId);
/// // All events emitted here get parentEventId = eventId
/// </code>
/// </example>
public static class AllCausalScope
{
    /// <summary>
    /// Begins a causal scope with the specified event ID as the parent.
    /// Returns an IDisposable that restores the previous parent on dispose.
    /// </summary>
    /// <param name="eventId">The event ID to set as the causal parent.</param>
    /// <returns>An IDisposable scope.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="eventId"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventId"/> is empty.</exception>
    public static IDisposable Begin(string eventId)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentException.ThrowIfNullOrEmpty(eventId);

        return AllCausalityContext.SetParent(eventId);
    }
}
