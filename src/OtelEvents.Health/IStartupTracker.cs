// <copyright file="IStartupTracker.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health;

/// <summary>
/// Tracks the startup lifecycle of the application.
/// Allows components to signal when the application is ready to accept traffic
/// or when startup has failed.
/// </summary>
public interface IStartupTracker
{
    /// <summary>
    /// Gets the current startup status.
    /// </summary>
    StartupStatus Status { get; }

    /// <summary>
    /// Marks the application as ready to accept traffic.
    /// </summary>
    void MarkReady();

    /// <summary>
    /// Marks the application startup as failed.
    /// </summary>
    /// <param name="reason">Optional reason describing the failure.</param>
    void MarkFailed(string? reason = null);
}
