namespace OtelEvents.Schema.Validation;

/// <summary>
/// Error code constants for schema validation rules (OTEL_SCHEMA_001 through OTEL_SCHEMA_025).
/// </summary>
public static class ErrorCodes
{
    /// <summary>No duplicate event names within merged schemas.</summary>
    public const string DuplicateEventName = "OTEL_SCHEMA_001";

    /// <summary>Severity must be one of: TRACE, DEBUG, INFO, WARN, ERROR, FATAL.</summary>
    public const string InvalidSeverity = "OTEL_SCHEMA_002";

    /// <summary>All {placeholders} in message must match a field name.</summary>
    public const string MessageTemplateMismatch = "OTEL_SCHEMA_003";

    /// <summary>All ref values must resolve to a defined field or enum.</summary>
    public const string UnresolvedRef = "OTEL_SCHEMA_004";

    /// <summary>Field types must be from the supported type list.</summary>
    public const string InvalidType = "OTEL_SCHEMA_005";

    /// <summary>Event names must be PascalCase or dot-namespaced, lowercase, alphanumeric + dots only.</summary>
    public const string InvalidEventNameFormat = "OTEL_SCHEMA_006";

    /// <summary>Required fields must have a type (directly or via ref).</summary>
    public const string RequiredFieldMissingType = "OTEL_SCHEMA_007";

    /// <summary>Metric types must be: counter, histogram, gauge.</summary>
    public const string InvalidMetricType = "OTEL_SCHEMA_008";

    /// <summary>Enum definitions must have at least one value.</summary>
    public const string EmptyEnum = "OTEL_SCHEMA_009";

    /// <summary>Schema version must be valid semver.</summary>
    public const string InvalidSemver = "OTEL_SCHEMA_010";

    /// <summary>Event/field names must not start with 'otel_events.'.</summary>
    public const string ReservedPrefix = "OTEL_SCHEMA_011";

    /// <summary>Each event id must be a unique integer.</summary>
    public const string DuplicateEventId = "OTEL_SCHEMA_012";

    /// <summary>Meter name must be a valid .NET identifier (dot-separated).</summary>
    public const string InvalidMeterName = "OTEL_SCHEMA_013";

    /// <summary>Sensitivity must be one of: public, internal, pii, credential.</summary>
    public const string InvalidSensitivity = "OTEL_SCHEMA_014";

    /// <summary>maxLength must be a positive integer when specified.</summary>
    public const string InvalidMaxLength = "OTEL_SCHEMA_015";

    /// <summary>Individual schema files must not exceed 1 MB.</summary>
    public const string FileSizeExceeded = "OTEL_SCHEMA_016";

    /// <summary>Merged schemas must not define more than 500 events total.</summary>
    public const string EventCountExceeded = "OTEL_SCHEMA_017";

    /// <summary>Individual events must not define more than 50 fields.</summary>
    public const string FieldCountExceeded = "OTEL_SCHEMA_018";

    /// <summary>YAML anchors (&amp;) and aliases (*) are not allowed in schema files.</summary>
    public const string YamlAliasRejected = "OTEL_SCHEMA_019";

    /// <summary>Enum values must not contain duplicates.</summary>
    public const string DuplicateEnumValue = "OTEL_SCHEMA_020";

    /// <summary>Namespace must be a valid .NET namespace (dot-separated identifiers).</summary>
    public const string InvalidNamespace = "OTEL_SCHEMA_021";

    /// <summary>Schema name must be a valid C# identifier.</summary>
    public const string InvalidSchemaName = "OTEL_SCHEMA_022";

    /// <summary>Enum values must be valid C# identifiers.</summary>
    public const string InvalidEnumValue = "OTEL_SCHEMA_023";

    /// <summary>Import path must not traverse outside the schema directory.</summary>
    public const string ImportPathTraversal = "OTEL_SCHEMA_024";

    /// <summary>All schemas in a merged set must share the same major version.</summary>
    public const string IncompatibleSchemaVersion = "OTEL_SCHEMA_025";

    /// <summary>meterLifecycle must be one of: static, di.</summary>
    public const string InvalidMeterLifecycle = "OTEL_SCHEMA_026";

    /// <summary>Referenced package schema could not be resolved from NuGet package content.</summary>
    public const string PackageSchemaNotFound = "OTEL_SCHEMA_027";

    /// <summary>Event type must be one of: start, success, failure, event.</summary>
    public const string InvalidEventType = "OTEL_SCHEMA_028";

    /// <summary>Success and failure events must have a parent field referencing a start event.</summary>
    public const string MissingParentEvent = "OTEL_SCHEMA_029";

    /// <summary>Parent must reference a valid start event name in the schema.</summary>
    public const string InvalidParentEvent = "OTEL_SCHEMA_030";

    /// <summary>Start events must not have a parent field.</summary>
    public const string StartEventWithParent = "OTEL_SCHEMA_031";

    /// <summary>Duplicate component name across merged schemas.</summary>
    public const string DuplicateComponentName = "OTEL_SCHEMA_032";

    /// <summary>Component name must be alphanumeric with hyphens only.</summary>
    public const string InvalidComponentNameFormat = "OTEL_SCHEMA_033";

    /// <summary>Component threshold must be between 0.0 and 1.0.</summary>
    public const string InvalidComponentThreshold = "OTEL_SCHEMA_034";

    /// <summary>Component healthyAbove must be greater than degradedAbove.</summary>
    public const string InvalidThresholdOrder = "OTEL_SCHEMA_035";

    /// <summary>Component minimumSignals must be at least 1.</summary>
    public const string InvalidMinimumSignals = "OTEL_SCHEMA_036";

    /// <summary>Component window must be greater than 0.</summary>
    public const string InvalidComponentWindow = "OTEL_SCHEMA_037";

    /// <summary>Signal event name must be a non-empty string.</summary>
    public const string InvalidSignalEventName = "OTEL_SCHEMA_038";

    /// <summary>Signal match filter keys must be non-empty strings.</summary>
    public const string InvalidSignalMatchKey = "OTEL_SCHEMA_039";
}
