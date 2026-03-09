namespace All.Causality;

/// <summary>
/// IDisposable scope that sets the parent event ID in <see cref="AllCausalityContext"/>
/// and restores the previous value on dispose.
/// Used internally by <see cref="AllCausalityContext.SetParent"/> and <see cref="AllCausalScope.Begin"/>.
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
        _previousParentEventId = AllCausalityContext.CurrentParentEventId;
        AllCausalityContext.CurrentParentEventId = parentEventId;
    }

    /// <summary>
    /// Restores the previous parent event ID.
    /// </summary>
    public void Dispose()
    {
        AllCausalityContext.CurrentParentEventId = _previousParentEventId;
    }
}
