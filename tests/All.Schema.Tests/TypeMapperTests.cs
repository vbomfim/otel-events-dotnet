using All.Schema.CodeGen;
using All.Schema.Models;

namespace All.Schema.Tests;

/// <summary>
/// Tests for TypeMapper — validates YAML-to-C# type mapping
/// and severity-to-LogLevel mapping.
/// </summary>
public class TypeMapperTests
{
    // ── C# Type Mapping ────────────────────────────────────────────

    [Theory]
    [InlineData(FieldType.String, "string")]
    [InlineData(FieldType.Int, "int")]
    [InlineData(FieldType.Long, "long")]
    [InlineData(FieldType.Double, "double")]
    [InlineData(FieldType.Bool, "bool")]
    [InlineData(FieldType.DateTime, "DateTimeOffset")]
    [InlineData(FieldType.Duration, "TimeSpan")]
    [InlineData(FieldType.Guid, "Guid")]
    [InlineData(FieldType.StringArray, "string[]")]
    [InlineData(FieldType.IntArray, "int[]")]
    [InlineData(FieldType.Map, "Dictionary<string, string>")]
    public void ToCSharpType_MapsAllFieldTypes(FieldType fieldType, string expected)
    {
        Assert.Equal(expected, TypeMapper.ToCSharpType(fieldType));
    }

    [Fact]
    public void ToCSharpType_EnumFieldType_ReturnsString()
    {
        // Enum base mapping returns "string"; actual enum type resolved via GetFieldCSharpType
        Assert.Equal("string", TypeMapper.ToCSharpType(FieldType.Enum));
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

    // ── Field C# Type Resolution ───────────────────────────────────

    [Fact]
    public void GetFieldCSharpType_EnumWithRef_ReturnsPascalCaseRefName()
    {
        var field = new FieldDefinition
        {
            Name = "method",
            Type = FieldType.Enum,
            Ref = "http_method"
        };

        Assert.Equal("HttpMethod", TypeMapper.GetFieldCSharpType(field));
    }

    [Fact]
    public void GetFieldCSharpType_EnumWithInlineValues_ReturnsPascalCaseFieldName()
    {
        var field = new FieldDefinition
        {
            Name = "status",
            Type = FieldType.Enum,
            Values = ["active", "inactive", "pending"]
        };

        Assert.Equal("Status", TypeMapper.GetFieldCSharpType(field));
    }

    [Fact]
    public void GetFieldCSharpType_EnumWithoutRefOrValues_ReturnsString()
    {
        var field = new FieldDefinition
        {
            Name = "category",
            Type = FieldType.Enum
        };

        Assert.Equal("string", TypeMapper.GetFieldCSharpType(field));
    }

    [Fact]
    public void GetFieldCSharpType_NonEnumType_ReturnsMappedType()
    {
        var field = new FieldDefinition
        {
            Name = "amount",
            Type = FieldType.Double
        };

        Assert.Equal("double", TypeMapper.GetFieldCSharpType(field));
    }

    [Fact]
    public void GetFieldCSharpType_NullType_ReturnsObject()
    {
        var field = new FieldDefinition
        {
            Name = "unknown",
            Type = null
        };

        Assert.Equal("object", TypeMapper.GetFieldCSharpType(field));
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

    // ── Numeric Type Check ─────────────────────────────────────────

    [Theory]
    [InlineData(FieldType.Int, true)]
    [InlineData(FieldType.Long, true)]
    [InlineData(FieldType.Double, true)]
    [InlineData(FieldType.String, false)]
    [InlineData(FieldType.Bool, false)]
    [InlineData(FieldType.DateTime, false)]
    public void IsNumericType_ReturnsCorrectResult(FieldType fieldType, bool expected)
    {
        Assert.Equal(expected, TypeMapper.IsNumericType(fieldType));
    }
}
