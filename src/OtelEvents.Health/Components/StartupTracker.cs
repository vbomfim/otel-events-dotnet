// <copyright file="StartupTracker.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

using OtelEvents.Health.Contracts;

namespace OtelEvents.Health.Components;

/// <summary>
/// Default implementation of <see cref="IStartupTracker"/>.
/// Thread-safe via volatile reads/writes for simple status transitions.
/// </summary>
internal sealed class StartupTracker : IStartupTracker
{
    private volatile StartupStatus _status = StartupStatus.Starting;
    private volatile string? _failureReason;

    /// <inheritdoc />
    public StartupStatus Status => _status;

    /// <summary>
    /// Gets the reason for startup failure, if any.
    /// </summary>
    public string? FailureReason => _failureReason;

    /// <inheritdoc />
    public void MarkReady()
    {
        _status = StartupStatus.Ready;
    }

    /// <inheritdoc />
    public void MarkFailed(string? reason = null)
    {
        _failureReason = reason;
        _status = StartupStatus.Failed;
    }
}
