namespace OtelEvents.HealthChecks.Tests;

/// <summary>
/// Tests for <see cref="OtelEventsHealthCheckOptions"/> defaults and configuration.
/// </summary>
public sealed class OtelEventsHealthCheckOptionsTests
{
    [Fact]
    public void Defaults_EmitExecutedEvents_IsTrue()
    {
        var options = new OtelEventsHealthCheckOptions();
        Assert.True(options.EmitExecutedEvents);
    }

    [Fact]
    public void Defaults_EmitStateChangedEvents_IsTrue()
    {
        var options = new OtelEventsHealthCheckOptions();
        Assert.True(options.EmitStateChangedEvents);
    }

    [Fact]
    public void Defaults_EmitReportCompletedEvents_IsTrue()
    {
        var options = new OtelEventsHealthCheckOptions();
        Assert.True(options.EmitReportCompletedEvents);
    }

    [Fact]
    public void Defaults_SuppressHealthyExecutedEvents_IsFalse()
    {
        var options = new OtelEventsHealthCheckOptions();
        Assert.False(options.SuppressHealthyExecutedEvents);
    }

    [Fact]
    public void Defaults_EnableCausalScope_IsTrue()
    {
        var options = new OtelEventsHealthCheckOptions();
        Assert.True(options.EnableCausalScope);
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new OtelEventsHealthCheckOptions
        {
            EmitExecutedEvents = false,
            EmitStateChangedEvents = false,
            EmitReportCompletedEvents = false,
            SuppressHealthyExecutedEvents = true,
            EnableCausalScope = false,
        };

        Assert.False(options.EmitExecutedEvents);
        Assert.False(options.EmitStateChangedEvents);
        Assert.False(options.EmitReportCompletedEvents);
        Assert.True(options.SuppressHealthyExecutedEvents);
        Assert.False(options.EnableCausalScope);
    }
}
