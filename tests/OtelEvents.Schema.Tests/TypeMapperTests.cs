using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Models;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for TypeMapper — validates that all fields map to "string"
/// and severity-to-LogLevel mapping works correctly.
/// </summary>
public class TypeMapperTests
{
    // ── All Fields Are Strings ─────────────────────────────────────

    [Fact]
    public void GetFieldCSharpType_AnyField_ReturnsString()
    {
        var field = new FieldDefinition { Name = "amount" };
        Assert.Equal("string", TypeMapper.GetFieldCSharpType(field));
    }

    [Fact]
    public void GetFieldCSharpType_RequiredField_ReturnsString()
    {
        var field = new FieldDefinition { Name = "orderId", Required = true };
        Assert.Equal("string", TypeMapper.GetFieldCSharpType(field));
    }

    [Fact]
    public void GetFieldCSharpType_FieldWithSensitivity_ReturnsString()
    {
        var field = new FieldDefinition
        {
            Name = "userId",
            Sensitivity = Sensitivity.Pii
        };
        Assert.Equal("string", TypeMapper.GetFieldCSharpType(field));
    }

    // ── Severity → LogLevel Mapping ────────────────────────────────

    [Theory]
    [InlineData(Severity.Trace, "LogLevel.Trace")]
    [InlineData(Severity.Debug, "LogLevel.Debug")]
    [InlineData(Severity.Info, "LogLevel.Information")]
    [InlineData(Severity.Warn, "LogLevel.Warning")]
    [InlineData(Severity.Error, "LogLevel.Error")]
    [InlineData(Severity.Fatal, "LogLevel.Critical")]
    public void ToLogLevel_MapsAllSeverities(Severity severity, string expected)
    {
        Assert.Equal(expected, TypeMapper.ToLogLevel(severity));
    }

    // ── Metric CLR Types ───────────────────────────────────────────

    [Theory]
    [InlineData(MetricType.Counter, "long")]
    [InlineData(MetricType.Histogram, "double")]
    [InlineData(MetricType.Gauge, "double")]
    public void GetMetricClrType_ReturnsCorrectType(MetricType metricType, string expected)
    {
        Assert.Equal(expected, TypeMapper.GetMetricClrType(metricType));
    }
}
