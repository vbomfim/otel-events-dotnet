// <copyright file="FakeGrpcHealthSource.cs" company="OtelEvents">
// Copyright (c) OtelEvents. All rights reserved.
// </copyright>

namespace OtelEvents.Health.Grpc.Tests;

/// <summary>
/// Configurable test double for <see cref="IGrpcHealthSource"/>.
/// </summary>
internal sealed class FakeGrpcHealthSource : IGrpcHealthSource
{
    public int ReadySubchannelCount { get; set; }
    public int TotalSubchannelCount { get; set; }
}
