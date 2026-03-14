// <copyright file="ISystemClock.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

namespace OtelEvents.Health;

/// <summary>
/// Abstraction over system time to enable deterministic testing.
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
