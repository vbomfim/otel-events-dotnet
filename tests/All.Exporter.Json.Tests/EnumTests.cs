namespace All.Exporter.Json.Tests;

/// <summary>
/// Tests for the enum type definitions: <see cref="AllJsonOutput"/>,
/// <see cref="AllEnvironmentProfile"/>, and <see cref="ExceptionDetailLevel"/>.
/// </summary>
public sealed class EnumTests
{
    [Theory]
    [InlineData(AllJsonOutput.Stdout, 0)]
    [InlineData(AllJsonOutput.Stderr, 1)]
    [InlineData(AllJsonOutput.File, 2)]
    public void AllJsonOutput_HasExpectedValues(AllJsonOutput value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    [Theory]
    [InlineData(AllEnvironmentProfile.Development, 0)]
    [InlineData(AllEnvironmentProfile.Staging, 1)]
    [InlineData(AllEnvironmentProfile.Production, 2)]
    public void AllEnvironmentProfile_HasExpectedValues(AllEnvironmentProfile value, int expected)
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
