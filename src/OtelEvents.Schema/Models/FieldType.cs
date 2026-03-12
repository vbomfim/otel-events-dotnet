namespace OtelEvents.Schema.Models;

/// <summary>
/// Supported field types in the YAML schema.
/// Deprecated: All fields are now strings. This enum is retained for backward compatibility only.
/// </summary>
[Obsolete("All fields are now strings. FieldType is retained for backward-compatible YAML parsing only.")]
public enum FieldType
{
    String
}

/// <summary>
/// Extension methods for FieldType parsing and validation.
/// Deprecated: All fields are now strings. Retained for backward-compatible YAML parsing.
/// </summary>
[Obsolete("All fields are now strings. FieldType parsing is retained for backward compatibility only.")]
public static class FieldTypeExtensions
{
    private static readonly Dictionary<string, FieldType> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = FieldType.String,
        ["int"] = FieldType.String,
        ["long"] = FieldType.String,
        ["double"] = FieldType.String,
        ["bool"] = FieldType.String,
        ["datetime"] = FieldType.String,
        ["duration"] = FieldType.String,
        ["guid"] = FieldType.String,
        ["enum"] = FieldType.String,
        ["string[]"] = FieldType.String,
        ["int[]"] = FieldType.String,
        ["map"] = FieldType.String
    };

    /// <summary>
    /// Checks whether a YAML type string is a recognized type name.
    /// All recognized types map to String (retained for backward compatibility).
    /// </summary>
    public static bool IsRecognizedTypeName(string yamlType)
    {
        return TypeMap.ContainsKey(yamlType);
    }

    /// <summary>
    /// Returns the set of valid YAML type names (for backward-compatible parsing).
    /// </summary>
    public static IReadOnlyCollection<string> ValidTypeNames => TypeMap.Keys;
}
