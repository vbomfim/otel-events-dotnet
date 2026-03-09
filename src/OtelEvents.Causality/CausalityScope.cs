namespace OtelEvents.Causality;

/// <summary>
/// IDisposable scope that sets the parent event ID in <see cref="OtelEventsCausalityContext"/>
/// and restores the previous value on dispose.
/// Used internally by <see cref="OtelEventsCausalityContext.SetParent"/> and <see cref="OtelEventsCausalScope.Begin"/>.
/// </summary>
internal sealed class CausalityScope : IDisposable
{
    private readonly string? _previousParentEventId;

    /// <summary>
    /// Creates a new causal scope, saving the current parent and setting the new one.
    /// </summary>
    /// <param name="parentEventId">The event ID to set as the current parent.</param>
    internal CausalityScope(string parentEventId)
    {
        _previousParentEventId = OtelEventsCausalityContext.CurrentParentEventId;
        OtelEventsCausalityContext.CurrentParentEventId = parentEventId;
    }

    /// <summary>
    /// Restores the previous parent event ID.
    /// </summary>
    public void Dispose()
    {
        OtelEventsCausalityContext.CurrentParentEventId = _previousParentEventId;
    }
}
