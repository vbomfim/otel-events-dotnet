using System.Text;
using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Models;

namespace OtelEvents.Schema.Documentation;

/// <summary>
/// Generates Markdown documentation from a <see cref="SchemaDocument"/>.
/// Produces a complete event catalog with table of contents, event details,
/// field tables, metrics, enums, and shared field definitions.
/// </summary>
public sealed class SchemaDocumentationGenerator
{
    /// <summary>
    /// Generates a <see cref="GeneratedFile"/> containing Markdown documentation.
    /// </summary>
    /// <param name="doc">The parsed schema document.</param>
    /// <returns>A generated file with .md extension and Markdown content.</returns>
    public GeneratedFile GenerateDocumentation(SchemaDocument doc)
    {
        var content = GenerateMarkdown(doc);
        var fileName = $"{doc.Schema.Name}.md";
        return new GeneratedFile(fileName, content);
    }

    /// <summary>
    /// Generates Markdown documentation string from a schema document.
    /// </summary>
    /// <param name="doc">The parsed schema document.</param>
    /// <returns>Complete Markdown documentation as a string.</returns>
    public string GenerateMarkdown(SchemaDocument doc)
    {
        var sb = new StringBuilder();

        AppendHeader(sb, doc.Schema);
        AppendTableOfContents(sb, doc);
        AppendEventsSection(sb, doc.Events);
        AppendEnumDefinitions(sb, doc.Enums);
        AppendSharedFields(sb, doc.Fields);
        AppendFooter(sb);

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════
    // HEADER
    // ═══════════════════════════════════════════════════════════════

    private static void AppendHeader(StringBuilder sb, SchemaHeader header)
    {
        sb.AppendLine($"# {header.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Version:** {header.Version}  ");
        sb.AppendLine($"**Namespace:** {header.Namespace}");
        sb.AppendLine();

        if (header.Description is not null)
        {
            sb.AppendLine(header.Description);
            sb.AppendLine();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TABLE OF CONTENTS
    // ═══════════════════════════════════════════════════════════════

    private static void AppendTableOfContents(StringBuilder sb, SchemaDocument doc)
    {
        if (doc.Events.Count == 0 && doc.Enums.Count == 0 && doc.Fields.Count == 0)
        {
            return;
        }

        sb.AppendLine("## Table of Contents");
        sb.AppendLine();

        if (doc.Events.Count > 0)
        {
            sb.AppendLine("### Events");
            sb.AppendLine();
            foreach (var evt in doc.Events)
            {
                var anchor = ToAnchor(evt.Name);
                sb.AppendLine($"- [{evt.Name}](#{anchor})");
            }
            sb.AppendLine();
        }

        if (doc.Enums.Count > 0)
        {
            sb.AppendLine("- [Enum Definitions](#enum-definitions)");

            foreach (var enumDef in doc.Enums)
            {
                var anchor = ToAnchor(enumDef.Name);
                sb.AppendLine($"  - [{enumDef.Name}](#{anchor})");
            }
            sb.AppendLine();
        }

        if (doc.Fields.Count > 0)
        {
            sb.AppendLine("- [Shared Fields](#shared-fields)");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════════════════
    // EVENTS SECTION
    // ═══════════════════════════════════════════════════════════════

    private static void AppendEventsSection(StringBuilder sb, List<EventDefinition> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        sb.AppendLine("## Events");
        sb.AppendLine();

        foreach (var evt in events)
        {
            AppendEvent(sb, evt);
        }
    }

    private static void AppendEvent(StringBuilder sb, EventDefinition evt)
    {
        sb.AppendLine($"### {evt.Name}");
        sb.AppendLine();

        if (evt.Description is not null)
        {
            sb.AppendLine(evt.Description);
            sb.AppendLine();
        }

        sb.AppendLine($"| Property | Value |");
        sb.AppendLine($"| --- | --- |");
        sb.AppendLine($"| **Event ID** | {evt.Id} |");
        sb.AppendLine($"| **Severity** | {evt.Severity} |");
        sb.AppendLine($"| **Message** | `{evt.Message}` |");
        sb.AppendLine($"| **Exception** | {(evt.Exception ? "Yes" : "No")} |");
        sb.AppendLine();

        AppendFieldsTable(sb, evt.Fields);
        AppendMetricsTable(sb, evt.Metrics);
        AppendTags(sb, evt.Tags);
    }

    // ═══════════════════════════════════════════════════════════════
    // FIELDS TABLE
    // ═══════════════════════════════════════════════════════════════

    private static void AppendFieldsTable(StringBuilder sb, List<FieldDefinition> fields)
    {
        if (fields.Count == 0)
        {
            return;
        }

        sb.AppendLine("#### Fields");
        sb.AppendLine();
        sb.AppendLine("| Name | Type | Required | Sensitivity | Description |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var field in fields)
        {
            var typeName = FormatFieldType(field);
            var required = field.Required ? "✓" : "";
            var sensitivity = FormatSensitivity(field.Sensitivity);
            var description = FormatFieldDescription(field);

            sb.AppendLine($"| {field.Name} | {typeName} | {required} | {sensitivity} | {description} |");
        }

        sb.AppendLine();
    }

    private static string FormatFieldType(FieldDefinition field)
    {
        // All fields are strings in the simplified schema
        return "string";
    }

    private static string FormatSensitivity(Sensitivity sensitivity)
    {
        return sensitivity switch
        {
            Sensitivity.Credential => "🔒 Credential",
            Sensitivity.Pii => "⚠️ Pii",
            Sensitivity.Internal => "⚠️ Internal",
            _ => "Public"
        };
    }

    private static string FormatFieldDescription(FieldDefinition field)
    {
        return field.Description ?? "";
    }

    // ═══════════════════════════════════════════════════════════════
    // METRICS TABLE
    // ═══════════════════════════════════════════════════════════════

    private static void AppendMetricsTable(StringBuilder sb, List<MetricDefinition> metrics)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        sb.AppendLine("#### Metrics");
        sb.AppendLine();
        sb.AppendLine("| Name | Type | Unit | Description |");
        sb.AppendLine("| --- | --- | --- | --- |");

        foreach (var metric in metrics)
        {
            var unit = metric.Unit ?? "—";
            var description = metric.Description ?? "";
            sb.AppendLine($"| {metric.Name} | {metric.Type} | {unit} | {description} |");
        }

        sb.AppendLine();

        foreach (var metric in metrics)
        {
            AppendMetricDetails(sb, metric);
        }
    }

    private static void AppendMetricDetails(StringBuilder sb, MetricDefinition metric)
    {
        var hasLabels = metric.Labels is { Count: > 0 };
        var hasBuckets = metric.Buckets is { Count: > 0 };

        if (!hasLabels && !hasBuckets)
        {
            return;
        }

        sb.AppendLine($"**{metric.Name}:**");

        if (hasLabels)
        {
            sb.AppendLine($"- **Labels:** {string.Join(", ", metric.Labels!.Select(l => $"`{l}`"))}");
        }

        if (hasBuckets)
        {
            sb.AppendLine($"- **Buckets:** [{string.Join(", ", metric.Buckets!)}]");
        }

        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════════════════
    // TAGS
    // ═══════════════════════════════════════════════════════════════

    private static void AppendTags(StringBuilder sb, List<string> tags)
    {
        if (tags.Count == 0)
        {
            return;
        }

        sb.AppendLine($"**Tags:** {string.Join(" ", tags.Select(t => $"`{t}`"))}");
        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════════════════
    // ENUM DEFINITIONS
    // ═══════════════════════════════════════════════════════════════

    private static void AppendEnumDefinitions(StringBuilder sb, List<EnumDefinition> enums)
    {
        if (enums.Count == 0)
        {
            return;
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Enum Definitions");
        sb.AppendLine();

        foreach (var enumDef in enums)
        {
            sb.AppendLine($"### {enumDef.Name}");
            sb.AppendLine();

            if (enumDef.Description is not null)
            {
                sb.AppendLine(enumDef.Description);
                sb.AppendLine();
            }

            sb.AppendLine("| Value |");
            sb.AppendLine("| --- |");
            foreach (var value in enumDef.Values)
            {
                sb.AppendLine($"| {value} |");
            }
            sb.AppendLine();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SHARED FIELDS
    // ═══════════════════════════════════════════════════════════════

    private static void AppendSharedFields(StringBuilder sb, List<FieldDefinition> fields)
    {
        if (fields.Count == 0)
        {
            return;
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Shared Fields");
        sb.AppendLine();
        sb.AppendLine("These fields are defined at the schema level and can be referenced by events using `ref`.");
        sb.AppendLine();
        sb.AppendLine("| Name | Type | Description |");
        sb.AppendLine("| --- | --- | --- |");

        foreach (var field in fields)
        {
            var typeName = FormatFieldType(field);
            var description = FormatFieldDescription(field);
            sb.AppendLine($"| {field.Name} | {typeName} | {description} |");
        }

        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════════════════
    // FOOTER
    // ═══════════════════════════════════════════════════════════════

    private static void AppendFooter(StringBuilder sb)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated by otel-events Schema Documentation Generator*");
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static string ToAnchor(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            switch (ch)
            {
                case '.':
                    break;
                case ' ':
                    sb.Append('-');
                    break;
                default:
                    sb.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }

        return sb.ToString();
    }
}
