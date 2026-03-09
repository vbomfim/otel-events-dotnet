using System.Diagnostics;

namespace OtelEvents.Causality;

/// <summary>
/// A disposable handle returned by <see cref="OtelEventsCausalScope.Begin()"/>.
/// Tracks elapsed time since scope creation and restores the previous causal parent on dispose.
/// </summary>
public sealed class CausalScopeHandle : IDisposable
{
    private readonly string? _previousParentEventId;
    private readonly CausalScopeHandle? _previousScope;
    private readonly long _startTimestamp;

    /// <summary>
    /// The event ID assigned to this scope (used as parentEventId for child events).
    /// </summary>
    public string EventId { get; }

    /// <summary>
    /// Elapsed milliseconds since this scope was created.
    /// Use this instead of a separate Stopwatch for duration tracking.
    /// </summary>
    public double ElapsedMilliseconds => Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;

    internal CausalScopeHandle(string eventId)
    {
        EventId = eventId;
        _startTimestamp = Stopwatch.GetTimestamp();
        _previousParentEventId = OtelEventsCausalityContext.CurrentParentEventId;
        _previousScope = OtelEventsCausalityContext.CurrentScope;
        OtelEventsCausalityContext.CurrentParentEventId = eventId;
        OtelEventsCausalityContext.CurrentScope = this;
    }

    /// <summary>
    /// Restores the previous parent event ID and scope.
    /// </summary>
    public void Dispose()
    {
        OtelEventsCausalityContext.CurrentParentEventId = _previousParentEventId;
        OtelEventsCausalityContext.CurrentScope = _previousScope;
    }
}
