namespace All.Schema.Models;

/// <summary>
/// Supported field types in the YAML schema.
/// Maps YAML type strings to strongly-typed enum values.
/// </summary>
public enum FieldType
{
    String,
    Int,
    Long,
    Double,
    Bool,
    DateTime,
    Duration,
    Guid,
    Enum,
    StringArray,
    IntArray,
    Map
}

/// <summary>
/// Extension methods for FieldType parsing and validation.
/// </summary>
public static class FieldTypeExtensions
{
    private static readonly Dictionary<string, FieldType> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = FieldType.String,
        ["int"] = FieldType.Int,
        ["long"] = FieldType.Long,
        ["double"] = FieldType.Double,
        ["bool"] = FieldType.Bool,
        ["datetime"] = FieldType.DateTime,
        ["duration"] = FieldType.Duration,
        ["guid"] = FieldType.Guid,
        ["enum"] = FieldType.Enum,
        ["string[]"] = FieldType.StringArray,
        ["int[]"] = FieldType.IntArray,
        ["map"] = FieldType.Map
    };

    /// <summary>
    /// Tries to parse a YAML type string into a <see cref="FieldType"/>.
    /// </summary>
    public static bool TryParseFieldType(string yamlType, out FieldType fieldType)
    {
        return TypeMap.TryGetValue(yamlType, out fieldType);
    }

    /// <summary>
    /// Returns the set of valid YAML type names.
    /// </summary>
    public static IReadOnlyCollection<string> ValidTypeNames => TypeMap.Keys;
}
