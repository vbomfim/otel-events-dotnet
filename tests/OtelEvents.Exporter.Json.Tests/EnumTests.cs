namespace OtelEvents.Exporter.Json.Tests;

/// <summary>
/// Tests for the enum type definitions: <see cref="OtelEventsJsonOutput"/>,
/// <see cref="OtelEventsEnvironmentProfile"/>, and <see cref="ExceptionDetailLevel"/>.
/// </summary>
public sealed class EnumTests
{
    [Theory]
    [InlineData(OtelEventsJsonOutput.Stdout, 0)]
    [InlineData(OtelEventsJsonOutput.Stderr, 1)]
    [InlineData(OtelEventsJsonOutput.File, 2)]
    public void OtelEventsJsonOutput_HasExpectedValues(OtelEventsJsonOutput value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    [Theory]
    [InlineData(OtelEventsEnvironmentProfile.Development, 0)]
    [InlineData(OtelEventsEnvironmentProfile.Staging, 1)]
    [InlineData(OtelEventsEnvironmentProfile.Production, 2)]
    public void OtelEventsEnvironmentProfile_HasExpectedValues(OtelEventsEnvironmentProfile value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    [Theory]
    [InlineData(ExceptionDetailLevel.Full, 0)]
    [InlineData(ExceptionDetailLevel.TypeAndMessage, 1)]
    [InlineData(ExceptionDetailLevel.TypeOnly, 2)]
    public void ExceptionDetailLevel_HasExpectedValues(ExceptionDetailLevel value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }
}
