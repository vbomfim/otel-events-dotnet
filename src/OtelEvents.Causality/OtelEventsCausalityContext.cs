namespace OtelEvents.Causality;

/// <summary>
/// Ambient context for causal event linking.
/// Uses AsyncLocal to flow the current parent event ID across async boundaries.
/// Thread-safe by design — AsyncLocal provides per-async-flow isolation.
/// </summary>
public static class OtelEventsCausalityContext
{
    private static readonly AsyncLocal<string?> s_parentEventId = new();
    private static readonly AsyncLocal<CausalScopeHandle?> s_currentScope = new();

    /// <summary>
    /// Gets or sets the current parent event ID in the ambient async context.
    /// Returns null when no causal scope is active.
    /// </summary>
    public static string? CurrentParentEventId
    {
        get => s_parentEventId.Value;
        set => s_parentEventId.Value = value;
    }

    /// <summary>
    /// Gets or sets the current causal scope handle.
    /// Used by <see cref="OtelEventsCausalityProcessor"/> to read elapsed time automatically.
    /// </summary>
    public static CausalScopeHandle? CurrentScope
    {
        get => s_currentScope.Value;
        internal set => s_currentScope.Value = value;
    }

    /// <summary>
    /// Sets the parent event ID for the duration of the returned scope.
    /// Restores the previous value when the scope is disposed.
    /// </summary>
    /// <param name="parentEventId">The event ID to set as the current parent.</param>
    /// <returns>An IDisposable scope that restores the previous parent on dispose.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parentEventId"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="parentEventId"/> is empty.</exception>
    public static IDisposable SetParent(string parentEventId)
    {
        ArgumentNullException.ThrowIfNull(parentEventId);
        ArgumentException.ThrowIfNullOrEmpty(parentEventId);

        return new CausalityScope(parentEventId);
    }
}
