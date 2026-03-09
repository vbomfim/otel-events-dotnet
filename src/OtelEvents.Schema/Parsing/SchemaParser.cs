using System.Globalization;
using OtelEvents.Schema.Models;
using OtelEvents.Schema.Validation;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace OtelEvents.Schema.Parsing;

/// <summary>
/// Parses .all.yaml schema files into <see cref="SchemaDocument"/> instances.
/// Uses YamlDotNet with safe loading mode and enforces resource limits.
/// </summary>
public sealed class SchemaParser
{
    /// <summary>Maximum allowed file size in bytes (1 MB).</summary>
    internal const int MaxFileSizeBytes = 1_048_576;

    /// <summary>Maximum allowed YAML nesting depth.</summary>
    internal const int MaxNestingDepth = 20;

    /// <summary>
    /// Parses a YAML schema from a string content with a known byte size.
    /// </summary>
    /// <param name="yamlContent">The YAML string content.</param>
    /// <param name="fileSizeBytes">The original file size in bytes.</param>
    /// <returns>A parse result with either the document or errors.</returns>
    public ParseResult Parse(string yamlContent, long fileSizeBytes)
    {
        if (fileSizeBytes > MaxFileSizeBytes)
        {
            return ParseResult.Failure(new SchemaError
            {
                Code = ErrorCodes.FileSizeExceeded,
                Message = $"Schema file size ({fileSizeBytes} bytes) exceeds the maximum allowed size of {MaxFileSizeBytes} bytes."
            });
        }

        try
        {
            ValidateNoAliasesOrAnchors(yamlContent);

            var yaml = new YamlStream();
            using var reader = new StringReader(yamlContent);
            yaml.Load(reader);

            if (yaml.Documents.Count == 0)
            {
                return ParseResult.Failure(new SchemaError
                {
                    Code = ErrorCodes.InvalidSemver,
                    Message = "YAML document is empty — no schema found."
                });
            }

            var root = yaml.Documents[0].RootNode as YamlMappingNode;
            if (root is null)
            {
                return ParseResult.Failure(new SchemaError
                {
                    Code = ErrorCodes.InvalidSemver,
                    Message = "YAML root must be a mapping node."
                });
            }

            ValidateNestingDepth(root, 0);

            var header = ParseHeader(root);
            var imports = ParseImports(root);
            var fields = ParseSharedFields(root);
            var enums = ParseEnums(root);
            var events = ParseEvents(root);

            var document = new SchemaDocument
            {
                Schema = header,
                Imports = imports,
                Fields = fields,
                Enums = enums,
                Events = events
            };

            return ParseResult.Success(document);
        }
        catch (NestingDepthException ex)
        {
            return ParseResult.Failure(new SchemaError
            {
                Code = ErrorCodes.FileSizeExceeded,
                Message = ex.Message
            });
        }
        catch (YamlException ex)
        {
            return ParseResult.Failure(new SchemaError
            {
                Code = ErrorCodes.InvalidSemver,
                Message = $"YAML parse error: {ex.Message}"
            });
        }
        catch (SchemaParseException ex)
        {
            return ParseResult.Failure(new SchemaError
            {
                Code = ex.ErrorCode,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Parses a YAML schema from a file path.
    /// </summary>
    public ParseResult ParseFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            return ParseResult.Failure(new SchemaError
            {
                Code = ErrorCodes.FileSizeExceeded,
                Message = $"Schema file not found: {filePath}"
            });
        }

        var content = File.ReadAllText(filePath);
        return Parse(content, fileInfo.Length);
    }

    /// <summary>
    /// Pre-scans the YAML content to reject anchors (&amp;) and aliases (*).
    /// These features can be used for YAML bomb attacks (exponential expansion).
    /// </summary>
    private static void ValidateNoAliasesOrAnchors(string yamlContent)
    {
        using var reader = new StringReader(yamlContent);
        var parser = new Parser(reader);

        while (parser.MoveNext())
        {
            var current = parser.Current;

            if (current is AnchorAlias)
            {
                throw new SchemaParseException(ErrorCodes.YamlAliasRejected,
                    "YAML aliases (*) are not allowed in schema files — they can be used for YAML bomb attacks.");
            }

            if (current is NodeEvent nodeEvent && !nodeEvent.Anchor.IsEmpty)
            {
                throw new SchemaParseException(ErrorCodes.YamlAliasRejected,
                    "YAML anchors (&) are not allowed in schema files — they can be used for YAML bomb attacks.");
            }
        }
    }

    private static void ValidateNestingDepth(YamlNode node, int depth)
    {
        if (depth > MaxNestingDepth)
        {
            throw new NestingDepthException(
                $"YAML nesting depth exceeds maximum of {MaxNestingDepth}.");
        }

        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var entry in mapping.Children)
                {
                    ValidateNestingDepth(entry.Value, depth + 1);
                }
                break;
            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children)
                {
                    ValidateNestingDepth(item, depth + 1);
                }
                break;
        }
    }

    private static SchemaHeader ParseHeader(YamlMappingNode root)
    {
        var schemaNode = GetOptionalMapping(root, "schema")
            ?? throw new SchemaParseException(ErrorCodes.InvalidSemver, "Missing required 'schema' block.");

        return new SchemaHeader
        {
            Name = GetRequiredScalar(schemaNode, "name", "schema.name"),
            Version = GetRequiredScalar(schemaNode, "version", "schema.version"),
            Namespace = GetRequiredScalar(schemaNode, "namespace", "schema.namespace"),
            Description = GetOptionalScalar(schemaNode, "description"),
            MeterName = GetOptionalScalar(schemaNode, "meterName"),
            MeterLifecycle = ParseMeterLifecycle(GetOptionalScalar(schemaNode, "meterLifecycle"))
        };
    }

    private static List<string> ParseImports(YamlMappingNode root)
    {
        var importsNode = GetOptionalSequence(root, "imports");
        if (importsNode is null) return [];

        return importsNode.Children
            .OfType<YamlScalarNode>()
            .Select(n => n.Value!)
            .ToList();
    }

    private static List<FieldDefinition> ParseSharedFields(YamlMappingNode root)
    {
        var fieldsNode = GetOptionalMapping(root, "fields");
        if (fieldsNode is null) return [];

        var fields = new List<FieldDefinition>();
        foreach (var entry in fieldsNode.Children)
        {
            var name = ((YamlScalarNode)entry.Key).Value!;
            fields.Add(ParseFieldDefinition(name, entry.Value));
        }
        return fields;
    }

    private static List<EnumDefinition> ParseEnums(YamlMappingNode root)
    {
        var enumsNode = GetOptionalMapping(root, "enums");
        if (enumsNode is null) return [];

        var enums = new List<EnumDefinition>();
        foreach (var entry in enumsNode.Children)
        {
            var name = ((YamlScalarNode)entry.Key).Value!;
            var enumMapping = entry.Value as YamlMappingNode
                ?? throw new SchemaParseException(ErrorCodes.EmptyEnum, $"Enum '{name}' must be a mapping.");

            var description = GetOptionalScalar(enumMapping, "description");
            var valuesNode = GetOptionalSequence(enumMapping, "values");

            var values = valuesNode?.Children
                .OfType<YamlScalarNode>()
                .Select(n => n.Value!)
                .ToList() ?? [];

            enums.Add(new EnumDefinition
            {
                Name = name,
                Description = description,
                Values = values
            });
        }
        return enums;
    }

    private static List<EventDefinition> ParseEvents(YamlMappingNode root)
    {
        var eventsNode = GetOptionalMapping(root, "events");
        if (eventsNode is null) return [];

        var events = new List<EventDefinition>();
        foreach (var entry in eventsNode.Children)
        {
            var name = ((YamlScalarNode)entry.Key).Value!;
            var eventMapping = entry.Value as YamlMappingNode
                ?? throw new SchemaParseException(ErrorCodes.InvalidEventNameFormat,
                    $"Event '{name}' must be a mapping.");

            var idStr = GetRequiredScalar(eventMapping, "id", $"events.{name}.id");
            if (!int.TryParse(idStr, out var id))
            {
                throw new SchemaParseException(ErrorCodes.DuplicateEventId,
                    $"Event '{name}' has invalid id '{idStr}' — must be an integer.");
            }

            var severityStr = GetRequiredScalar(eventMapping, "severity", $"events.{name}.severity");
            if (!SeverityExtensions.TryParseSeverity(severityStr, out var severity))
            {
                throw new SchemaParseException(ErrorCodes.InvalidSeverity,
                    $"Event '{name}' has invalid severity '{severityStr}'.");
            }

            var message = GetRequiredScalar(eventMapping, "message", $"events.{name}.message");
            var description = GetOptionalScalar(eventMapping, "description");
            var exceptionFlag = GetOptionalScalarBool(eventMapping, "exception");

            var fields = ParseEventFields(eventMapping, name);
            var metrics = ParseEventMetrics(eventMapping, name);
            var tags = ParseEventTags(eventMapping);

            events.Add(new EventDefinition
            {
                Name = name,
                Id = id,
                Severity = severity,
                Description = description,
                Message = message,
                Exception = exceptionFlag,
                Fields = fields,
                Metrics = metrics,
                Tags = tags
            });
        }
        return events;
    }

    private static List<FieldDefinition> ParseEventFields(YamlMappingNode eventNode, string eventName)
    {
        var fieldsNode = GetOptionalMapping(eventNode, "fields");
        if (fieldsNode is null) return [];

        var fields = new List<FieldDefinition>();
        foreach (var entry in fieldsNode.Children)
        {
            var name = ((YamlScalarNode)entry.Key).Value!;
            fields.Add(ParseFieldDefinition(name, entry.Value));
        }
        return fields;
    }

    private static FieldDefinition ParseFieldDefinition(string name, YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            var typeStr = GetOptionalScalar(mapping, "type");
            FieldType? fieldType = null;
            if (typeStr is not null)
            {
                if (FieldTypeExtensions.TryParseFieldType(typeStr, out var parsed))
                    fieldType = parsed;
                else
                    fieldType = null; // Will be caught by validation
            }

            var sensitivityStr = GetOptionalScalar(mapping, "sensitivity");
            var sensitivity = Sensitivity.Public;
            if (sensitivityStr is not null)
            {
                _ = SensitivityExtensions.TryParseSensitivity(sensitivityStr, out sensitivity);
            }

            var maxLengthStr = GetOptionalScalar(mapping, "maxLength");
            int? maxLength = null;
            if (maxLengthStr is not null && int.TryParse(maxLengthStr, out var ml))
            {
                maxLength = ml;
            }
            else if (maxLengthStr is not null)
            {
                // Store as a signal for validation: non-numeric maxLength
                maxLength = -1;
            }

            var refValue = GetOptionalScalar(mapping, "ref");
            var description = GetOptionalScalar(mapping, "description");
            var required = GetOptionalScalarBool(mapping, "required");
            var index = GetOptionalScalarBool(mapping, "index");
            var unit = GetOptionalScalar(mapping, "unit");

            var examples = GetOptionalSequence(mapping, "examples")?
                .Children.OfType<YamlScalarNode>().Select(n => n.Value!).ToList();

            var values = GetOptionalSequence(mapping, "values")?
                .Children.OfType<YamlScalarNode>().Select(n => n.Value!).ToList();

            // Preserve the raw type string for validation when type is invalid
            return new FieldDefinition
            {
                Name = name,
                Type = fieldType,
                Description = description,
                Required = required,
                Sensitivity = sensitivity,
                MaxLength = maxLength,
                Index = index,
                Ref = refValue,
                Unit = unit,
                Examples = examples,
                Values = values,
                RawType = typeStr,
                RawSensitivity = sensitivityStr,
                RawMaxLength = maxLengthStr
            };
        }

        // Simple scalar value — treat as type shorthand
        if (node is YamlScalarNode scalar)
        {
            FieldType? fieldType = null;
            if (scalar.Value is not null && FieldTypeExtensions.TryParseFieldType(scalar.Value, out var parsed))
                fieldType = parsed;

            return new FieldDefinition
            {
                Name = name,
                Type = fieldType,
                RawType = scalar.Value
            };
        }

        throw new SchemaParseException(ErrorCodes.InvalidType,
            $"Field '{name}' has an invalid structure.");
    }

    private static List<MetricDefinition> ParseEventMetrics(YamlMappingNode eventNode, string eventName)
    {
        var metricsNode = GetOptionalMapping(eventNode, "metrics");
        if (metricsNode is null) return [];

        var metrics = new List<MetricDefinition>();
        foreach (var entry in metricsNode.Children)
        {
            var name = ((YamlScalarNode)entry.Key).Value!;
            var metricMapping = entry.Value as YamlMappingNode
                ?? throw new SchemaParseException(ErrorCodes.InvalidMetricType,
                    $"Metric '{name}' in event '{eventName}' must be a mapping.");

            var typeStr = GetRequiredScalar(metricMapping, "type", $"events.{eventName}.metrics.{name}.type");
            if (!MetricTypeExtensions.TryParseMetricType(typeStr, out var metricType))
            {
                // Store with a placeholder — validation will catch this
                metrics.Add(new MetricDefinition
                {
                    Name = name,
                    Type = MetricType.Counter, // placeholder
                    RawType = typeStr,
                    Unit = GetOptionalScalar(metricMapping, "unit"),
                    Description = GetOptionalScalar(metricMapping, "description")
                });
                continue;
            }

            var buckets = GetOptionalSequence(metricMapping, "buckets")?
                .Children.OfType<YamlScalarNode>()
                .Select(n => double.Parse(n.Value!, CultureInfo.InvariantCulture))
                .ToList();

            var labels = GetOptionalSequence(metricMapping, "labels")?
                .Children.OfType<YamlScalarNode>()
                .Select(n => n.Value!)
                .ToList();

            metrics.Add(new MetricDefinition
            {
                Name = name,
                Type = metricType,
                RawType = typeStr,
                Unit = GetOptionalScalar(metricMapping, "unit"),
                Description = GetOptionalScalar(metricMapping, "description"),
                Buckets = buckets,
                Labels = labels
            });
        }
        return metrics;
    }

    private static List<string> ParseEventTags(YamlMappingNode eventNode)
    {
        var tagsNode = GetOptionalSequence(eventNode, "tags");
        if (tagsNode is null) return [];

        return tagsNode.Children
            .OfType<YamlScalarNode>()
            .Select(n => n.Value!)
            .ToList();
    }

    // ── Meter lifecycle parsing ────────────────────────────────────

    private static MeterLifecycle ParseMeterLifecycle(string? value)
    {
        if (value is null)
            return MeterLifecycle.Static;

        return value.ToUpperInvariant() switch
        {
            "STATIC" => MeterLifecycle.Static,
            "DI" => MeterLifecycle.DI,
            _ => throw new SchemaParseException(ErrorCodes.InvalidMeterLifecycle,
                $"Invalid meterLifecycle '{value}' — must be 'static' or 'di'.")
        };
    }

    // ── YAML helpers ────────────────────────────────────────────────────

    private static YamlMappingNode? GetOptionalMapping(YamlMappingNode parent, string key)
    {
        var scalarKey = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(scalarKey, out var node))
            return node as YamlMappingNode;
        return null;
    }

    private static YamlSequenceNode? GetOptionalSequence(YamlMappingNode parent, string key)
    {
        var scalarKey = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(scalarKey, out var node))
            return node as YamlSequenceNode;
        return null;
    }

    private static string GetRequiredScalar(YamlMappingNode parent, string key, string path)
    {
        var scalarKey = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(scalarKey, out var node) && node is YamlScalarNode scalar && scalar.Value is not null)
            return scalar.Value;
        throw new SchemaParseException(ErrorCodes.InvalidSemver, $"Missing required field '{path}'.");
    }

    private static string? GetOptionalScalar(YamlMappingNode parent, string key)
    {
        var scalarKey = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(scalarKey, out var node) && node is YamlScalarNode scalar)
            return scalar.Value;
        return null;
    }

    private static bool GetOptionalScalarBool(YamlMappingNode parent, string key)
    {
        var value = GetOptionalScalar(parent, key);
        return value is not null && bool.TryParse(value, out var result) && result;
    }
}

/// <summary>
/// Internal exception for parse errors that should be converted to SchemaError.
/// </summary>
internal sealed class SchemaParseException : Exception
{
    public string ErrorCode { get; }

    public SchemaParseException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Internal exception for nesting depth violations.
/// </summary>
internal sealed class NestingDepthException : Exception
{
    public NestingDepthException(string message) : base(message) { }
}
