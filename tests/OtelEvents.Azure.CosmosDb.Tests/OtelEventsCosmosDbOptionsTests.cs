using OtelEvents.Azure.CosmosDb.Events;

namespace OtelEvents.Azure.CosmosDb.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsCosmosDbOptions"/> defaults and configuration.
/// Verifies PII-safe defaults per SPECIFICATION.md §15.5.
/// </summary>
public sealed class OtelEventsCosmosDbOptionsTests
{
    [Fact]
    public void Defaults_CaptureQueryText_IsFalse()
    {
        var options = new OtelEventsCosmosDbOptions();
        Assert.False(options.CaptureQueryText);
    }

    [Fact]
    public void Defaults_EnableCausalScope_IsTrue()
    {
        var options = new OtelEventsCosmosDbOptions();
        Assert.True(options.EnableCausalScope);
    }

    [Fact]
    public void Defaults_CaptureRegion_IsTrue()
    {
        var options = new OtelEventsCosmosDbOptions();
        Assert.True(options.CaptureRegion);
    }

    [Fact]
    public void Defaults_RuThreshold_IsZero()
    {
        var options = new OtelEventsCosmosDbOptions();
        Assert.Equal(0, options.RuThreshold);
    }

    [Fact]
    public void Defaults_LatencyThresholdMs_IsZero()
    {
        var options = new OtelEventsCosmosDbOptions();
        Assert.Equal(0, options.LatencyThresholdMs);
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new OtelEventsCosmosDbOptions
        {
            CaptureQueryText = true,
            EnableCausalScope = false,
            CaptureRegion = false,
            RuThreshold = 10.0,
            LatencyThresholdMs = 100.0,
        };

        Assert.True(options.CaptureQueryText);
        Assert.False(options.EnableCausalScope);
        Assert.False(options.CaptureRegion);
        Assert.Equal(10.0, options.RuThreshold);
        Assert.Equal(100.0, options.LatencyThresholdMs);
    }
}
