namespace OtelEvents.Causality;

/// <summary>
/// Convenience class for creating causal scopes.
/// Sets the parent event ID in <see cref="OtelEventsCausalityContext"/> for the duration of the scope,
/// so all events emitted within the scope have the specified parent.
/// </summary>
/// <example>
/// <code>
/// using var scope = OtelEventsCausalScope.Begin();
/// // All events emitted here get parentEventId auto-generated
/// // scope.ElapsedMilliseconds gives duration since scope creation
/// </code>
/// </example>
public static class OtelEventsCausalScope
{
    /// <summary>
    /// Begins a causal scope with an auto-generated UUID v7 event ID.
    /// Returns a <see cref="CausalScopeHandle"/> that tracks elapsed time and restores the previous parent on dispose.
    /// </summary>
    /// <returns>A disposable scope handle with <see cref="CausalScopeHandle.ElapsedMilliseconds"/>.</returns>
    public static CausalScopeHandle Begin()
    {
        var eventId = Uuid7.FormatEventId();
        return new CausalScopeHandle(eventId);
    }

    /// <summary>
    /// Begins a causal scope with the specified event ID as the parent.
    /// Returns a <see cref="CausalScopeHandle"/> that tracks elapsed time and restores the previous parent on dispose.
    /// </summary>
    /// <param name="eventId">The event ID to set as the causal parent.</param>
    /// <returns>A disposable scope handle with <see cref="CausalScopeHandle.ElapsedMilliseconds"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="eventId"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventId"/> is empty.</exception>
    public static CausalScopeHandle Begin(string eventId)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentException.ThrowIfNullOrEmpty(eventId);

        return new CausalScopeHandle(eventId);
    }
}
