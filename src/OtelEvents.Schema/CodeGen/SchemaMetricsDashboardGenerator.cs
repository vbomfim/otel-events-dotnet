using System.Text;
using System.Text.Json;
using OtelEvents.Schema.Models;

namespace OtelEvents.Schema.CodeGen;

/// <summary>
/// Generates Grafana dashboard JSON and OTEL Collector configuration
/// from <see cref="SchemaDocument"/> metric definitions.
/// Each metric produces a panel: counters → rate timeseries,
/// histograms → percentile timeseries, gauges → stat panels.
/// </summary>
public sealed class SchemaMetricsDashboardGenerator
{
    private const int PanelWidth = 12;
    private const int PanelHeight = 8;
    private const int GrafanaSchemaVersion = 39;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Generates both Grafana dashboard JSON and OTEL Collector config
    /// as <see cref="GeneratedFile"/> instances.
    /// </summary>
    /// <param name="doc">The parsed schema document.</param>
    /// <returns>Generated dashboard and collector config files.</returns>
    public IReadOnlyList<GeneratedFile> GenerateFiles(SchemaDocument doc)
    {
        var dashboardJson = GenerateDashboardJson(doc);
        var collectorYaml = GenerateCollectorConfig(doc);

        var schemaName = ToSafeFileName(doc.Schema.Name);

        return
        [
            new GeneratedFile($"{schemaName}-dashboard.json", dashboardJson),
            new GeneratedFile($"{schemaName}-otel-collector.yaml", collectorYaml)
        ];
    }

    /// <summary>
    /// Generates a Grafana dashboard JSON from schema metrics.
    /// Counter metrics produce rate() panels, histograms produce
    /// histogram_quantile() panels, and gauges produce stat panels.
    /// </summary>
    /// <param name="doc">The parsed schema document.</param>
    /// <returns>Valid Grafana dashboard JSON string.</returns>
    public string GenerateDashboardJson(SchemaDocument doc)
    {
        var metrics = CollectAllMetrics(doc);
        var panels = BuildPanels(metrics);
        var meterName = doc.Schema.MeterName ?? doc.Schema.Namespace;

        var dashboard = new Dictionary<string, object?>
        {
            ["uid"] = null,
            ["title"] = $"{doc.Schema.Name} — Performance Dashboard",
            ["description"] = $"Auto-generated from {doc.Schema.Name} v{doc.Schema.Version} schema (meter: {meterName})",
            ["schemaVersion"] = GrafanaSchemaVersion,
            ["version"] = 1,
            ["editable"] = true,
            ["time"] = new Dictionary<string, string>
            {
                ["from"] = "now-1h",
                ["to"] = "now"
            },
            ["refresh"] = "10s",
            ["templating"] = BuildTemplating(),
            ["panels"] = panels,
            ["tags"] = new[] { "otel-events", "auto-generated", doc.Schema.Name.ToUpperInvariant() },
            ["annotations"] = new Dictionary<string, object> { ["list"] = Array.Empty<object>() }
        };

        return JsonSerializer.Serialize(dashboard, JsonOptions);
    }

    /// <summary>
    /// Generates an OTEL Collector configuration that receives OTLP telemetry
    /// and exports metrics to Prometheus for Grafana consumption.
    /// </summary>
    /// <param name="doc">The parsed schema document.</param>
    /// <returns>OTEL Collector YAML configuration string.</returns>
    public string GenerateCollectorConfig(SchemaDocument doc)
    {
        var meterName = doc.Schema.MeterName ?? doc.Schema.Namespace;
        var sb = new StringBuilder();

        sb.AppendLine($"# OTEL Collector configuration for {doc.Schema.Name}");
        sb.AppendLine($"# Auto-generated from schema v{doc.Schema.Version}");
        sb.AppendLine($"# Meter: {meterName}");
        sb.AppendLine();
        sb.AppendLine("receivers:");
        sb.AppendLine("  otlp:");
        sb.AppendLine("    protocols:");
        sb.AppendLine("      grpc:");
        sb.AppendLine("        endpoint: \"0.0.0.0:4317\"");
        sb.AppendLine("      http:");
        sb.AppendLine("        endpoint: \"0.0.0.0:4318\"");
        sb.AppendLine();
        sb.AppendLine("processors:");
        sb.AppendLine("  batch:");
        sb.AppendLine("    timeout: 5s");
        sb.AppendLine("    send_batch_size: 1024");
        sb.AppendLine();
        sb.AppendLine("exporters:");
        sb.AppendLine("  prometheus:");
        sb.AppendLine("    endpoint: \"0.0.0.0:8889\"");
        sb.AppendLine("    namespace: \"\"");
        sb.AppendLine("    send_timestamps: true");
        sb.AppendLine("    metric_expiration: 5m");
        sb.AppendLine("    resource_to_telemetry_conversion:");
        sb.AppendLine("      enabled: true");
        sb.AppendLine();
        sb.AppendLine("  logging:");
        sb.AppendLine("    verbosity: basic");
        sb.AppendLine();
        sb.AppendLine("service:");
        sb.AppendLine("  pipelines:");
        sb.AppendLine("    metrics:");
        sb.AppendLine("      receivers: [otlp]");
        sb.AppendLine("      processors: [batch]");
        sb.AppendLine("      exporters: [prometheus]");
        sb.AppendLine("    logs:");
        sb.AppendLine("      receivers: [otlp]");
        sb.AppendLine("      processors: [batch]");
        sb.AppendLine("      exporters: [logging]");
        sb.AppendLine("    traces:");
        sb.AppendLine("      receivers: [otlp]");
        sb.AppendLine("      processors: [batch]");
        sb.AppendLine("      exporters: [logging]");

        return sb.ToString();
    }

    /// <summary>
    /// Converts a dotted metric name to Prometheus-compatible underscore format.
    /// Example: "http.request.count" → "http_request_count".
    /// </summary>
    /// <param name="name">Dotted metric name from schema.</param>
    /// <returns>Prometheus-compatible metric name.</returns>
    public static string ToPrometheusName(string name) =>
        name.Replace('.', '_');

    // ═══════════════════════════════════════════════════════════════
    // PRIVATE — Panel builders
    // ═══════════════════════════════════════════════════════════════

    private static List<(MetricDefinition Metric, string EventName)> CollectAllMetrics(SchemaDocument doc)
    {
        var metrics = new List<(MetricDefinition, string)>();
        foreach (var evt in doc.Events)
        {
            foreach (var metric in evt.Metrics)
            {
                metrics.Add((metric, evt.Name));
            }
        }
        return metrics;
    }

    private static List<Dictionary<string, object>> BuildPanels(
        List<(MetricDefinition Metric, string EventName)> metrics)
    {
        var panels = new List<Dictionary<string, object>>();
        var panelId = 1;

        for (var i = 0; i < metrics.Count; i++)
        {
            var (metric, eventName) = metrics[i];
            var xPos = (i % 2) * PanelWidth;
            var yPos = (i / 2) * PanelHeight;

            var panel = metric.Type switch
            {
                MetricType.Counter => BuildCounterPanel(metric, eventName, panelId, xPos, yPos),
                MetricType.Histogram => BuildHistogramPanel(metric, eventName, panelId, xPos, yPos),
                MetricType.Gauge => BuildGaugePanel(metric, eventName, panelId, xPos, yPos),
                _ => BuildCounterPanel(metric, eventName, panelId, xPos, yPos)
            };

            panels.Add(panel);
            panelId++;
        }

        return panels;
    }

    private static Dictionary<string, object> BuildCounterPanel(
        MetricDefinition metric, string eventName, int id, int x, int y)
    {
        var promName = ToPrometheusName(metric.Name);
        var labels = metric.Labels ?? [];
        var legendSuffix = labels.Count > 0
            ? " by " + string.Join(", ", labels)
            : "";

        var query = labels.Count > 0
            ? $"sum(rate({promName}_total[$__rate_interval])) by ({string.Join(", ", labels)})"
            : $"rate({promName}_total[$__rate_interval])";

        var legendFormat = labels.Count > 0
            ? string.Join(" ", labels.Select(l => $"{{{{{l}}}}}"))
            : $"{promName}";

        return BuildTimeSeriesPanel(
            id: id,
            title: $"{metric.Description ?? NamingHelper.ToPascalCase(metric.Name)} Rate{legendSuffix}",
            query: query,
            legendFormat: legendFormat,
            unit: metric.Unit is not null ? $"{metric.Unit}/s" : "reqps",
            x: x,
            y: y);
    }

    private static Dictionary<string, object> BuildHistogramPanel(
        MetricDefinition metric, string eventName, int id, int x, int y)
    {
        var promName = ToPrometheusName(metric.Name);
        var unit = metric.Unit ?? "s";

        var targets = new List<Dictionary<string, object>>
        {
            BuildTarget($"histogram_quantile(0.5, sum(rate({promName}_bucket[$__rate_interval])) by (le))", "p50"),
            BuildTarget($"histogram_quantile(0.95, sum(rate({promName}_bucket[$__rate_interval])) by (le))", "p95"),
            BuildTarget($"histogram_quantile(0.99, sum(rate({promName}_bucket[$__rate_interval])) by (le))", "p99")
        };

        return new Dictionary<string, object>
        {
            ["id"] = id,
            ["type"] = "timeseries",
            ["title"] = $"{metric.Description ?? NamingHelper.ToPascalCase(metric.Name)} Percentiles",
            ["gridPos"] = new Dictionary<string, int>
            {
                ["x"] = x, ["y"] = y, ["w"] = PanelWidth, ["h"] = PanelHeight
            },
            ["datasource"] = new Dictionary<string, string>
            {
                ["type"] = "prometheus",
                ["uid"] = "${datasource}"
            },
            ["targets"] = targets,
            ["fieldConfig"] = new Dictionary<string, object>
            {
                ["defaults"] = new Dictionary<string, object>
                {
                    ["unit"] = unit,
                    ["custom"] = new Dictionary<string, object>
                    {
                        ["drawStyle"] = "line",
                        ["lineWidth"] = 2,
                        ["fillOpacity"] = 10
                    }
                }
            }
        };
    }

    private static Dictionary<string, object> BuildGaugePanel(
        MetricDefinition metric, string eventName, int id, int x, int y)
    {
        var promName = ToPrometheusName(metric.Name);

        return new Dictionary<string, object>
        {
            ["id"] = id,
            ["type"] = "stat",
            ["title"] = metric.Description ?? NamingHelper.ToPascalCase(metric.Name),
            ["gridPos"] = new Dictionary<string, int>
            {
                ["x"] = x, ["y"] = y, ["w"] = PanelWidth, ["h"] = PanelHeight
            },
            ["datasource"] = new Dictionary<string, string>
            {
                ["type"] = "prometheus",
                ["uid"] = "${datasource}"
            },
            ["targets"] = new List<Dictionary<string, object>>
            {
                BuildTarget(promName, promName)
            },
            ["fieldConfig"] = new Dictionary<string, object>
            {
                ["defaults"] = new Dictionary<string, object>
                {
                    ["unit"] = metric.Unit ?? "short"
                }
            }
        };
    }

    private static Dictionary<string, object> BuildTimeSeriesPanel(
        int id, string title, string query, string legendFormat, string unit, int x, int y)
    {
        return new Dictionary<string, object>
        {
            ["id"] = id,
            ["type"] = "timeseries",
            ["title"] = title,
            ["gridPos"] = new Dictionary<string, int>
            {
                ["x"] = x, ["y"] = y, ["w"] = PanelWidth, ["h"] = PanelHeight
            },
            ["datasource"] = new Dictionary<string, string>
            {
                ["type"] = "prometheus",
                ["uid"] = "${datasource}"
            },
            ["targets"] = new List<Dictionary<string, object>>
            {
                BuildTarget(query, legendFormat)
            },
            ["fieldConfig"] = new Dictionary<string, object>
            {
                ["defaults"] = new Dictionary<string, object>
                {
                    ["unit"] = unit,
                    ["custom"] = new Dictionary<string, object>
                    {
                        ["drawStyle"] = "line",
                        ["lineWidth"] = 2,
                        ["fillOpacity"] = 10
                    }
                }
            }
        };
    }

    private static Dictionary<string, object> BuildTarget(string expr, string legendFormat)
    {
        return new Dictionary<string, object>
        {
            ["expr"] = expr,
            ["legendFormat"] = legendFormat,
            ["refId"] = "A",
            ["datasource"] = new Dictionary<string, string>
            {
                ["type"] = "prometheus",
                ["uid"] = "${datasource}"
            }
        };
    }

    private static Dictionary<string, object> BuildTemplating()
    {
        return new Dictionary<string, object>
        {
            ["list"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["name"] = "datasource",
                    ["type"] = "datasource",
                    ["query"] = "prometheus",
                    ["current"] = new Dictionary<string, string>
                    {
                        ["text"] = "default",
                        ["value"] = "default"
                    }
                }
            }
        };
    }

    private static string ToSafeFileName(string name) =>
        name.Replace(' ', '-')
            .Replace('.', '-');
}
