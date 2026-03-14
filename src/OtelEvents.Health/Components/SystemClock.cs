// <copyright file="SystemClock.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

namespace OtelEvents.Health.Components;

/// <summary>
/// Default clock implementation backed by <see cref="TimeProvider"/>.
/// </summary>
internal sealed class SystemClock : ISystemClock
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemClock"/> class.
    /// </summary>
    /// <param name="timeProvider">The underlying time provider.</param>
    public SystemClock(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
}
