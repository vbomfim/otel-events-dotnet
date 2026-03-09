namespace All.Schema.Validation;

/// <summary>
/// Error code constants for schema validation rules (ALL_SCHEMA_001 through ALL_SCHEMA_018).
/// </summary>
public static class ErrorCodes
{
    /// <summary>No duplicate event names within merged schemas.</summary>
    public const string DuplicateEventName = "ALL_SCHEMA_001";

    /// <summary>Severity must be one of: TRACE, DEBUG, INFO, WARN, ERROR, FATAL.</summary>
    public const string InvalidSeverity = "ALL_SCHEMA_002";

    /// <summary>All {placeholders} in message must match a field name.</summary>
    public const string MessageTemplateMismatch = "ALL_SCHEMA_003";

    /// <summary>All ref values must resolve to a defined field or enum.</summary>
    public const string UnresolvedRef = "ALL_SCHEMA_004";

    /// <summary>Field types must be from the supported type list.</summary>
    public const string InvalidType = "ALL_SCHEMA_005";

    /// <summary>Event names must be dot-namespaced, lowercase, alphanumeric + dots only.</summary>
    public const string InvalidEventNameFormat = "ALL_SCHEMA_006";

    /// <summary>Required fields must have a type (directly or via ref).</summary>
    public const string RequiredFieldMissingType = "ALL_SCHEMA_007";

    /// <summary>Metric types must be: counter, histogram, gauge.</summary>
    public const string InvalidMetricType = "ALL_SCHEMA_008";

    /// <summary>Enum definitions must have at least one value.</summary>
    public const string EmptyEnum = "ALL_SCHEMA_009";

    /// <summary>Schema version must be valid semver.</summary>
    public const string InvalidSemver = "ALL_SCHEMA_010";

    /// <summary>Event/field names must not start with 'all.'.</summary>
    public const string ReservedPrefix = "ALL_SCHEMA_011";

    /// <summary>Each event id must be a unique integer.</summary>
    public const string DuplicateEventId = "ALL_SCHEMA_012";

    /// <summary>Meter name must be a valid .NET identifier (dot-separated).</summary>
    public const string InvalidMeterName = "ALL_SCHEMA_013";

    /// <summary>Sensitivity must be one of: public, internal, pii, credential.</summary>
    public const string InvalidSensitivity = "ALL_SCHEMA_014";

    /// <summary>maxLength must be a positive integer when specified.</summary>
    public const string InvalidMaxLength = "ALL_SCHEMA_015";

    /// <summary>Individual schema files must not exceed 1 MB.</summary>
    public const string FileSizeExceeded = "ALL_SCHEMA_016";

    /// <summary>Merged schemas must not define more than 500 events total.</summary>
    public const string EventCountExceeded = "ALL_SCHEMA_017";

    /// <summary>Individual events must not define more than 50 fields.</summary>
    public const string FieldCountExceeded = "ALL_SCHEMA_018";

    /// <summary>YAML anchors (&amp;) and aliases (*) are not allowed in schema files.</summary>
    public const string YamlAliasRejected = "ALL_SCHEMA_019";

    /// <summary>Enum values must not contain duplicates.</summary>
    public const string DuplicateEnumValue = "ALL_SCHEMA_020";

    /// <summary>Namespace must be a valid .NET namespace (dot-separated identifiers).</summary>
    public const string InvalidNamespace = "ALL_SCHEMA_021";

    /// <summary>Schema name must be a valid C# identifier.</summary>
    public const string InvalidSchemaName = "ALL_SCHEMA_022";

    /// <summary>Enum values must be valid C# identifiers.</summary>
    public const string InvalidEnumValue = "ALL_SCHEMA_023";

    /// <summary>Import path must not traverse outside the schema directory.</summary>
    public const string ImportPathTraversal = "ALL_SCHEMA_024";
}
