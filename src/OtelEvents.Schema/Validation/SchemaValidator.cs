using System.Text.RegularExpressions;
using OtelEvents.Schema.Models;

namespace OtelEvents.Schema.Validation;

/// <summary>
/// Validates <see cref="SchemaDocument"/> instances against OTEL_SCHEMA_001–025 rules.
/// Operates on already-parsed documents (post-parse validation).
/// </summary>
public sealed partial class SchemaValidator
{
    /// <summary>Maximum allowed events across all merged schemas.</summary>
    internal const int MaxEventCount = 500;

    /// <summary>Maximum allowed fields per event.</summary>
    internal const int MaxFieldsPerEvent = 50;

    // Regex: lowercase alphanumeric + dots, must have at least one dot (legacy format)
    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$")]
    private static partial Regex DotNamespacedEventNameRegex();

    // Regex: PascalCase C# identifier (starts with uppercase letter, no dots)
    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalCaseEventNameRegex();

    // Regex: component name — lowercase alphanumeric + hyphens
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ComponentNameRegex();

    // Regex: semver (simplified — major.minor.patch with optional pre-release)
    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)*)?(\+[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)*)?$")]
    private static partial Regex SemverRegex();

    // Regex: valid .NET identifier segments separated by dots
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex MeterNameRegex();

    // Regex: message template placeholder extraction
    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex CSharpIdentifierRegex();

    /// <summary>
    /// Validates a single schema document.
    /// </summary>
    public ValidationResult Validate(SchemaDocument document)
    {
        return Validate([document]);
    }

    /// <summary>
    /// Validates multiple schema documents (merged schema).
    /// Enforces cross-document rules like unique event names/IDs and event count limits.
    /// </summary>
    public ValidationResult Validate(IReadOnlyList<SchemaDocument> documents)
    {
        var errors = new List<SchemaError>();

        // Collect all shared fields and enums across all documents for ref resolution
        var allSharedFields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase);
        var allEnums = new Dictionary<string, EnumDefinition>(StringComparer.OrdinalIgnoreCase);
        var allEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allEventIds = new HashSet<int>();
        var allComponentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalEvents = 0;

        foreach (var doc in documents)
        {
            // OTEL_SCHEMA_010: Semver version
            ValidateSemver(doc.Schema.Version, errors);

            // OTEL_SCHEMA_013: Meter name
            var meterName = doc.Schema.MeterName ?? doc.Schema.Namespace;
            ValidateMeterName(meterName, errors);

            // OTEL_SCHEMA_021: Namespace validation
            ValidateNamespace(doc.Schema.Namespace, errors);

            // OTEL_SCHEMA_022: Schema name validation
            ValidateSchemaName(doc.Schema.Name, errors);

            // Collect shared fields
            foreach (var field in doc.Fields)
            {
                allSharedFields[field.Name] = field;
            }

            // OTEL_SCHEMA_009: Enum non-empty + collect
            foreach (var enumDef in doc.Enums)
            {
                if (enumDef.Values.Count == 0)
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.EmptyEnum,
                        Message = $"Enum '{enumDef.Name}' must have at least one value."
                    });
                }

                // OTEL_SCHEMA_020: Duplicate enum values
                var distinctValues = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in enumDef.Values)
                {
                    if (!distinctValues.Add(value))
                    {
                        errors.Add(new SchemaError
                        {
                            Code = ErrorCodes.DuplicateEnumValue,
                            Message = $"Enum '{enumDef.Name}' has duplicate value '{value}'."
                        });
                    }

                    // OTEL_SCHEMA_023: Enum value must be valid C# identifier
                    ValidateEnumValue(value, enumDef.Name, errors);
                }

                allEnums[enumDef.Name] = enumDef;
            }

            // Validate each event
            foreach (var evt in doc.Events)
            {
                totalEvents++;

                // OTEL_SCHEMA_001: Unique event names
                if (!allEventNames.Add(evt.Name))
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.DuplicateEventName,
                        Message = $"Duplicate event name '{evt.Name}'."
                    });
                }

                // OTEL_SCHEMA_012: Unique event IDs
                if (!allEventIds.Add(evt.Id))
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.DuplicateEventId,
                        Message = $"Duplicate event id '{evt.Id}' on event '{evt.Name}'."
                    });
                }

                // OTEL_SCHEMA_006: Event name format
                ValidateEventNameFormat(evt.Name, errors);

                // OTEL_SCHEMA_011: Reserved prefix on event name
                ValidateReservedPrefix(evt.Name, "Event name", errors);

                // OTEL_SCHEMA_003: Message template placeholders match fields
                ValidateMessageTemplate(evt, errors);

                // OTEL_SCHEMA_018: Field count limit
                if (evt.Fields.Count > MaxFieldsPerEvent)
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.FieldCountExceeded,
                        Message = $"Event '{evt.Name}' has {evt.Fields.Count} fields, exceeding the maximum of {MaxFieldsPerEvent}."
                    });
                }

                // Validate each field
                foreach (var field in evt.Fields)
                {
                    ValidateField(field, evt.Name, allSharedFields, allEnums, errors);
                }

                // Validate each metric
                foreach (var metric in evt.Metrics)
                {
                    ValidateMetric(metric, evt.Name, errors);
                }

                // OTEL_SCHEMA_028: Event type validity
                ValidateEventType(evt, errors);
            }

            // Validate each component
            foreach (var component in doc.Components)
            {
                // OTEL_SCHEMA_032: Unique component names
                if (!allComponentNames.Add(component.Name))
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.DuplicateComponentName,
                        Message = $"Duplicate component name '{component.Name}'."
                    });
                }

                ValidateComponent(component, errors);
            }
        }

        // OTEL_SCHEMA_029/030/031: Cross-event transaction rules
        ValidateTransactionRules(documents, errors);

        // OTEL_SCHEMA_017: Event count limit (across merged schemas)
        if (totalEvents > MaxEventCount)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.EventCountExceeded,
                Message = $"Total event count ({totalEvents}) exceeds the maximum of {MaxEventCount}."
            });
        }

        // OTEL_SCHEMA_025: Schema version compatibility (same major version required)
        ValidateVersionCompatibility(documents, errors);

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    private static void ValidateSemver(string version, List<SchemaError> errors)
    {
        if (!SemverRegex().IsMatch(version))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidSemver,
                Message = $"Schema version '{version}' is not valid semver."
            });
        }
    }

    private static void ValidateMeterName(string meterName, List<SchemaError> errors)
    {
        if (!MeterNameRegex().IsMatch(meterName))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidMeterName,
                Message = $"Meter name '{meterName}' is not a valid .NET identifier."
            });
        }
    }

    private static void ValidateEventNameFormat(string eventName, List<SchemaError> errors)
    {
        if (!DotNamespacedEventNameRegex().IsMatch(eventName) && !PascalCaseEventNameRegex().IsMatch(eventName))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidEventNameFormat,
                Message = $"Event name '{eventName}' must be either PascalCase (e.g., 'OrderPlaced') or lowercase dot-namespaced (e.g., 'order.placed')."
            });
        }
    }

    private static void ValidateReservedPrefix(string name, string context, List<SchemaError> errors)
    {
        if (name.StartsWith("otel_events.", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.ReservedPrefix,
                Message = $"{context} '{name}' must not start with the reserved 'otel_events.' prefix."
            });
        }
    }

    private static void ValidateMessageTemplate(EventDefinition evt, List<SchemaError> errors)
    {
        var placeholders = PlaceholderRegex().Matches(evt.Message)
            .Select(m => m.Groups[1].Value)
            .ToList();

        var fieldNames = new HashSet<string>(
            evt.Fields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var placeholder in placeholders)
        {
            if (!fieldNames.Contains(placeholder))
            {
                errors.Add(new SchemaError
                {
                    Code = ErrorCodes.MessageTemplateMismatch,
                    Message = $"Event '{evt.Name}' message placeholder '{{{placeholder}}}' does not match any field."
                });
            }
        }
    }

    private static void ValidateField(
        FieldDefinition field,
        string eventName,
        Dictionary<string, FieldDefinition> sharedFields,
        Dictionary<string, EnumDefinition> enums,
        List<SchemaError> errors)
    {
        // OTEL_SCHEMA_011: Reserved prefix on field name
        if (field.Name.StartsWith("otel_events.", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.ReservedPrefix,
                Message = $"Field name '{field.Name}' in event '{eventName}' must not start with the reserved 'otel_events.' prefix."
            });
        }

        // OTEL_SCHEMA_005: Removed — all fields are strings, no type validation needed.
        // OTEL_SCHEMA_004: Removed — ref field referencing has been removed.
        // OTEL_SCHEMA_007: Removed — required fields no longer need a type/ref (all are strings).

        // OTEL_SCHEMA_014: Sensitivity validity
        if (field.RawSensitivity is not null &&
            !SensitivityExtensions.TryParseSensitivity(field.RawSensitivity, out _))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidSensitivity,
                Message = $"Field '{field.Name}' in event '{eventName}' has invalid sensitivity '{field.RawSensitivity}'."
            });
        }

        // OTEL_SCHEMA_015: maxLength validity
        if (field.RawMaxLength is not null)
        {
            if (!int.TryParse(field.RawMaxLength, out var ml) || ml <= 0)
            {
                errors.Add(new SchemaError
                {
                    Code = ErrorCodes.InvalidMaxLength,
                    Message = $"Field '{field.Name}' in event '{eventName}' has invalid maxLength '{field.RawMaxLength}' — must be a positive integer."
                });
            }
        }
    }

    private static void ValidateMetric(MetricDefinition metric, string eventName, List<SchemaError> errors)
    {
        // OTEL_SCHEMA_008: Metric type validity
        if (metric.RawType is not null && !MetricTypeExtensions.TryParseMetricType(metric.RawType, out _))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidMetricType,
                Message = $"Metric '{metric.Name}' in event '{eventName}' has invalid type '{metric.RawType}'."
            });
        }
    }

    private static void ValidateNamespace(string ns, List<SchemaError> errors)
    {
        if (string.IsNullOrEmpty(ns) || !NamespaceRegex().IsMatch(ns))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidNamespace,
                Message = $"Schema namespace '{ns}' is not a valid .NET namespace (must be dot-separated valid identifiers)."
            });
        }
    }

    private static void ValidateSchemaName(string name, List<SchemaError> errors)
    {
        if (string.IsNullOrEmpty(name) || !CSharpIdentifierRegex().IsMatch(name))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidSchemaName,
                Message = $"Schema name '{name}' is not a valid C# identifier."
            });
        }
    }

    private static void ValidateEnumValue(string value, string enumName, List<SchemaError> errors)
    {
        if (string.IsNullOrEmpty(value) || !CSharpIdentifierRegex().IsMatch(value))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidEnumValue,
                Message = $"Enum value '{value}' in enum '{enumName}' is not a valid C# identifier (must match ^[A-Za-z_][A-Za-z0-9_]*$)."
            });
        }
    }

    private static void ValidateVersionCompatibility(
        IReadOnlyList<SchemaDocument> documents,
        List<SchemaError> errors)
    {
        if (documents.Count <= 1)
            return;

        var firstMajor = ExtractMajorVersion(documents[0].Schema.Version);
        if (firstMajor < 0)
            return; // Invalid semver already reported by OTEL_SCHEMA_010

        for (var i = 1; i < documents.Count; i++)
        {
            var major = ExtractMajorVersion(documents[i].Schema.Version);
            if (major < 0)
                continue; // Invalid semver already reported

            if (major != firstMajor)
            {
                errors.Add(new SchemaError
                {
                    Code = ErrorCodes.IncompatibleSchemaVersion,
                    Message = $"Schema '{documents[i].Schema.Name}' version '{documents[i].Schema.Version}' " +
                              $"has major version {major}, which is incompatible with major version {firstMajor} " +
                              $"from schema '{documents[0].Schema.Name}'."
                });
            }
        }
    }

    /// <summary>
    /// Extracts the major version number from a semver string.
    /// Returns -1 if the version is not valid semver.
    /// </summary>
    private static int ExtractMajorVersion(string version)
    {
        var dotIndex = version.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex <= 0)
            return -1;

        return int.TryParse(version.AsSpan(0, dotIndex), out var major) ? major : -1;
    }

    // ── OTEL_SCHEMA_028: Event type validity ────────────────────────

    private static void ValidateEventType(EventDefinition evt, List<SchemaError> errors)
    {
        if (evt.RawEventType is not null && !EventTypeExtensions.TryParseEventType(evt.RawEventType, out _))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidEventType,
                Message = $"Event '{evt.Name}' has invalid type '{evt.RawEventType}' — must be one of: start, success, failure, event."
            });
        }
    }

    // ── OTEL_SCHEMA_029/030/031: Transaction rules ──────────────────

    private static void ValidateTransactionRules(
        IReadOnlyList<SchemaDocument> documents,
        List<SchemaError> errors)
    {
        // Collect all start events across all documents
        var startEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in documents)
        {
            foreach (var evt in doc.Events)
            {
                if (evt.EventType == EventType.Start)
                {
                    startEventNames.Add(evt.Name);
                }
            }
        }

        // Validate each event's transaction constraints
        foreach (var doc in documents)
        {
            foreach (var evt in doc.Events)
            {
                // OTEL_SCHEMA_031: Start events must NOT have a parent
                if (evt.EventType == EventType.Start && evt.ParentEvent is not null)
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.StartEventWithParent,
                        Message = $"Start event '{evt.Name}' must not have a 'parent' field — start events create new transaction scopes."
                    });
                }

                // OTEL_SCHEMA_029: Success/failure events MUST have a parent
                if (evt.EventType is EventType.Success or EventType.Failure && evt.ParentEvent is null)
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.MissingParentEvent,
                        Message = $"{evt.EventType} event '{evt.Name}' must have a 'parent' field referencing a start event."
                    });
                }

                // OTEL_SCHEMA_030: Parent must reference a valid start event
                if (evt.ParentEvent is not null && !startEventNames.Contains(evt.ParentEvent))
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.InvalidParentEvent,
                        Message = $"Event '{evt.Name}' references parent '{evt.ParentEvent}' which is not a valid start event."
                    });
                }
            }
        }
    }

    // ── OTEL_SCHEMA_032–039: Component validation ───────────────────

    private static void ValidateComponent(ComponentDefinition component, List<SchemaError> errors)
    {
        // OTEL_SCHEMA_040: Detect malformed (unparseable) numeric values.
        // The parser returns -1 as a sentinel for values that are present but not parseable.
        ValidateMalformedValue(component.Name, "healthyAbove", component.HealthyAbove, errors);
        ValidateMalformedValue(component.Name, "degradedAbove", component.DegradedAbove, errors);
        ValidateMalformedValue(component.Name, "minimumSignals", component.MinimumSignals, errors);
        ValidateMalformedValue(component.Name, "window", component.WindowSeconds, errors);
        ValidateMalformedValue(component.Name, "cooldown", component.CooldownSeconds, errors);

        if (component.ResponseTime is not null)
        {
            ValidateMalformedValue(component.Name, "responseTime.percentile", component.ResponseTime.Percentile, errors);
            ValidateMalformedValue(component.Name, "responseTime.degradedAfter", component.ResponseTime.DegradedAfterMs, errors);
            ValidateMalformedValue(component.Name, "responseTime.unhealthyAfter", component.ResponseTime.UnhealthyAfterMs, errors);
        }

        // OTEL_SCHEMA_033: Component name format
        if (!ComponentNameRegex().IsMatch(component.Name))
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidComponentNameFormat,
                Message = $"Component name '{component.Name}' must be lowercase alphanumeric with hyphens (e.g., 'orders-db')."
            });
        }

        // OTEL_SCHEMA_034: Threshold range 0.0–1.0
        if (component.HealthyAbove is < 0.0 or > 1.0)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidComponentThreshold,
                Message = $"Component '{component.Name}' healthyAbove ({component.HealthyAbove}) must be between 0.0 and 1.0."
            });
        }

        if (component.DegradedAbove is < 0.0 or > 1.0)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidComponentThreshold,
                Message = $"Component '{component.Name}' degradedAbove ({component.DegradedAbove}) must be between 0.0 and 1.0."
            });
        }

        // OTEL_SCHEMA_035: healthyAbove > degradedAbove
        if (component.HealthyAbove > 0 && component.DegradedAbove > 0 &&
            component.HealthyAbove <= component.DegradedAbove)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidThresholdOrder,
                Message = $"Component '{component.Name}' healthyAbove ({component.HealthyAbove}) must be greater than degradedAbove ({component.DegradedAbove})."
            });
        }

        // OTEL_SCHEMA_036: minimumSignals >= 1 (reject negative values from malformed YAML)
        if (component.MinimumSignals < 0)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidMinimumSignals,
                Message = $"Component '{component.Name}' minimumSignals ({component.MinimumSignals}) must be at least 1."
            });
        }

        // OTEL_SCHEMA_037: window > 0
        if (component.WindowSeconds < 0)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.InvalidComponentWindow,
                Message = $"Component '{component.Name}' window ({component.WindowSeconds}s) must be greater than 0."
            });
        }

        // OTEL_SCHEMA_038/039: Signal validation
        foreach (var signal in component.Signals)
        {
            if (string.IsNullOrWhiteSpace(signal.Event))
            {
                errors.Add(new SchemaError
                {
                    Code = ErrorCodes.InvalidSignalEventName,
                    Message = $"Signal in component '{component.Name}' has an empty event name."
                });
            }

            foreach (var key in signal.Match.Keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add(new SchemaError
                    {
                        Code = ErrorCodes.InvalidSignalMatchKey,
                        Message = $"Signal in component '{component.Name}' has an empty match filter key."
                    });
                }
            }
        }
    }

    /// <summary>
    /// Reports a validation error if a parsed numeric value is -1 (sentinel for malformed YAML).
    /// </summary>
    private static void ValidateMalformedValue(string componentName, string fieldName, double value, List<SchemaError> errors)
    {
        // The parser uses -1 as a sentinel for present-but-unparseable values.
        // A value of 0 means the field was absent (null in YAML), which is valid (uses default).
        if (value < 0)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.MalformedValue,
                Message = $"Component '{componentName}' has a malformed value for '{fieldName}'. Expected a valid number or duration."
            });
        }
    }

    /// <summary>
    /// Reports a validation error if a parsed integer value is -1 (sentinel for malformed YAML).
    /// </summary>
    private static void ValidateMalformedValue(string componentName, string fieldName, int value, List<SchemaError> errors)
    {
        if (value < 0)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.MalformedValue,
                Message = $"Component '{componentName}' has a malformed value for '{fieldName}'. Expected a valid integer."
            });
        }
    }
}
