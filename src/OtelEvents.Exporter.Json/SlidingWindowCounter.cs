namespace OtelEvents.Exporter.Json;

/// <summary>
/// Thread-safe counter that tracks events within a sliding time window.
/// Resets the counter when the window expires.
/// </summary>
/// <remarks>
/// Uses <see cref="TimeProvider"/> for timestamp resolution, enabling
/// deterministic testing with a fake time source.
/// Thread safety is achieved via <see cref="Interlocked"/> and
/// <see cref="Volatile"/> operations. Occasional off-by-one at window
/// boundaries is acceptable for a best-effort rate limiter.
/// </remarks>
internal sealed class SlidingWindowCounter
{
    private readonly TimeSpan _windowDuration;
    private readonly TimeProvider _timeProvider;
    private long _windowStartTimestamp;
    private int _count;

    /// <summary>
    /// Initializes a new <see cref="SlidingWindowCounter"/>.
    /// </summary>
    /// <param name="windowDuration">Duration of each rate-limiting window.</param>
    /// <param name="timeProvider">Time source for timestamp resolution.</param>
    internal SlidingWindowCounter(TimeSpan windowDuration, TimeProvider timeProvider)
    {
        _windowDuration = windowDuration;
        _timeProvider = timeProvider;
        _windowStartTimestamp = timeProvider.GetTimestamp();
    }

    /// <summary>
    /// Attempts to increment the counter. Returns <c>true</c> if the event
    /// is within the rate limit, <c>false</c> if it would exceed it.
    /// </summary>
    /// <param name="maxCount">Maximum events allowed per window.</param>
    /// <returns><c>true</c> if the event should be forwarded; <c>false</c> if dropped.</returns>
    internal bool TryIncrement(int maxCount)
    {
        var now = _timeProvider.GetTimestamp();
        var windowStart = Volatile.Read(ref _windowStartTimestamp);
        var elapsed = _timeProvider.GetElapsedTime(windowStart, now);

        if (elapsed >= _windowDuration)
        {
            // Window expired — reset counter and start new window
            Volatile.Write(ref _windowStartTimestamp, now);
            Volatile.Write(ref _count, 1);
            return true;
        }

        return Interlocked.Increment(ref _count) <= maxCount;
    }
}
