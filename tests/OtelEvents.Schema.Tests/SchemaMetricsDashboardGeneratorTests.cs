using System.Text.Json;
using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Models;

namespace OtelEvents.Schema.Tests;

/// <summary>
/// Tests for SchemaMetricsDashboardGenerator — validates Grafana dashboard JSON
/// and OTEL Collector config generation from schema metric definitions.
/// </summary>
public class SchemaMetricsDashboardGeneratorTests
{
    private readonly SchemaMetricsDashboardGenerator _generator = new();

    // ── Helpers ────────────────────────────────────────────────────

    private static SchemaDocument CreateSchemaWithMetrics(
        List<EventDefinition> events,
        string name = "TestService",
        string ns = "Test.Events",
        string? meterName = null) => new()
    {
        Schema = new SchemaHeader
        {
            Name = name,
            Version = "1.0.0",
            Namespace = ns,
            MeterName = meterName
        },
        Events = events
    };

    private static EventDefinition CreateEvent(
        string name,
        int id,
        Severity severity = Severity.Info,
        string message = "Test event",
        List<MetricDefinition>? metrics = null) => new()
    {
        Name = name,
        Id = id,
        Severity = severity,
        Message = message,
        Metrics = metrics ?? []
    };

    // ═══════════════════════════════════════════════════════════════
    // 1. BASIC STRUCTURE — GenerateDashboardJson
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_EmptySchema_ReturnsValidJson()
    {
        var doc = CreateSchemaWithMetrics([]);

        var json = _generator.GenerateDashboardJson(doc);

        Assert.False(string.IsNullOrWhiteSpace(json));
        var jsonDoc = JsonDocument.Parse(json);
        Assert.NotNull(jsonDoc);
    }

    [Fact]
    public void GenerateDashboardJson_EmptySchema_HasDashboardTitle()
    {
        var doc = CreateSchemaWithMetrics([], name: "OrderService");

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var title = jsonDoc.RootElement.GetProperty("title").GetString();
        Assert.Contains("OrderService", title);
    }

    [Fact]
    public void GenerateDashboardJson_HasRequiredGrafanaFields()
    {
        var doc = CreateSchemaWithMetrics([]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        // Grafana required fields
        Assert.True(root.TryGetProperty("title", out _));
        Assert.True(root.TryGetProperty("panels", out _));
        Assert.True(root.TryGetProperty("time", out _));
        Assert.True(root.TryGetProperty("templating", out _));
        Assert.True(root.TryGetProperty("schemaVersion", out _));
    }

    [Fact]
    public void GenerateDashboardJson_HasNullUidForImport()
    {
        var doc = CreateSchemaWithMetrics([]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        // uid should be null for importable dashboards
        Assert.True(root.TryGetProperty("uid", out var uid));
        Assert.Equal(JsonValueKind.Null, uid.ValueKind);
    }

    [Fact]
    public void GenerateDashboardJson_HasPrometheusDataSource()
    {
        var doc = CreateSchemaWithMetrics([]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var templating = jsonDoc.RootElement.GetProperty("templating");
        var list = templating.GetProperty("list");
        Assert.True(list.GetArrayLength() > 0);

        var dsVar = list[0];
        Assert.Equal("datasource", dsVar.GetProperty("name").GetString());
        Assert.Equal("prometheus", dsVar.GetProperty("query").GetString());
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. COUNTER METRICS → Rate Panels
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_CounterMetric_CreatesRatePanel()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.request.count",
                    Type = MetricType.Counter,
                    Unit = "requests",
                    Description = "Total HTTP requests"
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var panels = jsonDoc.RootElement.GetProperty("panels");
        Assert.True(panels.GetArrayLength() > 0);

        // Should contain a panel with rate() query for counter
        Assert.Contains("rate(", json);
        Assert.Contains("http_request_count", json);
    }

    [Fact]
    public void GenerateDashboardJson_CounterMetric_PanelHasTimeSeries()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.request.count",
                    Type = MetricType.Counter,
                    Description = "Total HTTP requests"
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var panels = jsonDoc.RootElement.GetProperty("panels");
        var hasTimeSeries = false;
        foreach (var panel in panels.EnumerateArray())
        {
            if (panel.GetProperty("type").GetString() == "timeseries")
            {
                hasTimeSeries = true;
                break;
            }
        }
        Assert.True(hasTimeSeries, "Counter metric should generate a timeseries panel");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. HISTOGRAM METRICS → Percentile Panels
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_HistogramMetric_CreatesPercentilePanel()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.request.duration",
                    Type = MetricType.Histogram,
                    Unit = "ms",
                    Description = "Request duration"
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var panels = jsonDoc.RootElement.GetProperty("panels");
        Assert.True(panels.GetArrayLength() > 0);

        // Should contain histogram_quantile for p50/p95/p99
        Assert.Contains("histogram_quantile", json);
    }

    [Fact]
    public void GenerateDashboardJson_HistogramMetric_HasP50P95P99Queries()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.request.duration",
                    Type = MetricType.Histogram,
                    Unit = "ms",
                    Description = "Request duration"
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);

        Assert.Contains("0.5", json);   // p50
        Assert.Contains("0.95", json);  // p95
        Assert.Contains("0.99", json);  // p99
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. GAUGE METRICS → Stat Panels
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_GaugeMetric_CreatesStatPanel()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("app.health.changed", 9002, metrics:
            [
                new MetricDefinition
                {
                    Name = "app.health.status",
                    Type = MetricType.Gauge,
                    Description = "Current health status"
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var panels = jsonDoc.RootElement.GetProperty("panels");

        var hasStat = false;
        foreach (var panel in panels.EnumerateArray())
        {
            if (panel.GetProperty("type").GetString() == "stat")
            {
                hasStat = true;
                break;
            }
        }
        Assert.True(hasStat, "Gauge metric should generate a stat panel");
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. MULTIPLE METRICS — Mixed Types
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_MultipleMetrics_CreatesOnePanelPerMetric()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.request.count",
                    Type = MetricType.Counter,
                    Description = "Request count"
                },
                new MetricDefinition
                {
                    Name = "http.request.duration",
                    Type = MetricType.Histogram,
                    Unit = "ms",
                    Description = "Request duration"
                }
            ]),
            CreateEvent("http.request.failed", 1002, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.error.count",
                    Type = MetricType.Counter,
                    Description = "Error count"
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var panels = jsonDoc.RootElement.GetProperty("panels");
        // At least 3 panels for 3 metrics
        Assert.True(panels.GetArrayLength() >= 3,
            $"Expected at least 3 panels for 3 metrics, got {panels.GetArrayLength()}");
    }

    [Fact]
    public void GenerateDashboardJson_MultipleEvents_AllMetricsRepresented()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.response.count",
                    Type = MetricType.Counter
                }
            ]),
            CreateEvent("http.request.failed", 1002, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.error.count",
                    Type = MetricType.Counter
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);

        Assert.Contains("http_response_count", json);
        Assert.Contains("http_error_count", json);
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. PANEL LAYOUT — Positions are valid
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_Panels_HaveGridPositions()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.request.count",
                    Type = MetricType.Counter
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var panels = jsonDoc.RootElement.GetProperty("panels");
        foreach (var panel in panels.EnumerateArray())
        {
            Assert.True(panel.TryGetProperty("gridPos", out var gridPos));
            Assert.True(gridPos.TryGetProperty("x", out _));
            Assert.True(gridPos.TryGetProperty("y", out _));
            Assert.True(gridPos.TryGetProperty("w", out _));
            Assert.True(gridPos.TryGetProperty("h", out _));
        }
    }

    [Fact]
    public void GenerateDashboardJson_Panels_HaveIncrementingIds()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition { Name = "m1", Type = MetricType.Counter },
                new MetricDefinition { Name = "m2", Type = MetricType.Histogram }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var panels = jsonDoc.RootElement.GetProperty("panels");
        var ids = new HashSet<int>();
        foreach (var panel in panels.EnumerateArray())
        {
            var id = panel.GetProperty("id").GetInt32();
            Assert.True(ids.Add(id), $"Duplicate panel ID: {id}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. METRIC NAME SANITIZATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_DottedMetricName_ConvertedToUnderscores()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("test.event", 1, metrics:
            [
                new MetricDefinition
                {
                    Name = "my.service.request.total",
                    Type = MetricType.Counter
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);

        // Prometheus convention: dots → underscores
        Assert.Contains("my_service_request_total", json);
        Assert.DoesNotContain("my.service.request.total", json);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. COUNTER WITH LABELS → PromQL with label grouping
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_CounterWithLabels_IncludesLabelInQuery()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.response.count",
                    Type = MetricType.Counter,
                    Labels = ["httpMethod", "httpStatusCode"]
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);

        // Labels should appear in the PromQL legend or query
        Assert.Contains("httpMethod", json);
    }

    // ═══════════════════════════════════════════════════════════════
    // 9. OTEL COLLECTOR CONFIG GENERATION
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateCollectorConfig_ReturnsValidYaml()
    {
        var doc = CreateSchemaWithMetrics([]);

        var yaml = _generator.GenerateCollectorConfig(doc);

        Assert.False(string.IsNullOrWhiteSpace(yaml));
        Assert.Contains("receivers:", yaml);
        Assert.Contains("exporters:", yaml);
        Assert.Contains("service:", yaml);
    }

    [Fact]
    public void GenerateCollectorConfig_HasOtlpReceiver()
    {
        var doc = CreateSchemaWithMetrics([]);

        var yaml = _generator.GenerateCollectorConfig(doc);

        Assert.Contains("otlp:", yaml);
        Assert.Contains("grpc:", yaml);
        Assert.Contains("http:", yaml);
    }

    [Fact]
    public void GenerateCollectorConfig_HasPrometheusExporter()
    {
        var doc = CreateSchemaWithMetrics([]);

        var yaml = _generator.GenerateCollectorConfig(doc);

        Assert.Contains("prometheus:", yaml);
    }

    [Fact]
    public void GenerateCollectorConfig_HasServicePipeline()
    {
        var doc = CreateSchemaWithMetrics([]);

        var yaml = _generator.GenerateCollectorConfig(doc);

        Assert.Contains("pipelines:", yaml);
        Assert.Contains("metrics:", yaml);
    }

    [Fact]
    public void GenerateCollectorConfig_IncludesSchemaNameAsComment()
    {
        var doc = CreateSchemaWithMetrics([], name: "OrderService");

        var yaml = _generator.GenerateCollectorConfig(doc);

        Assert.Contains("OrderService", yaml);
    }

    // ═══════════════════════════════════════════════════════════════
    // 10. GENERATED FILE OUTPUT
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateFiles_ReturnsGrafanaAndCollectorFiles()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.request.count",
                    Type = MetricType.Counter
                }
            ])
        ]);

        var files = _generator.GenerateFiles(doc);

        Assert.Contains(files, f => f.FileName.EndsWith(".json"));
        Assert.Contains(files, f => f.FileName.EndsWith(".yaml"));
    }

    [Fact]
    public void GenerateFiles_GrafanaFileIsValidJson()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("test.event", 1, metrics:
            [
                new MetricDefinition { Name = "test.count", Type = MetricType.Counter }
            ])
        ]);

        var files = _generator.GenerateFiles(doc);
        var grafanaFile = files.First(f => f.FileName.EndsWith(".json"));

        // Must parse without exception
        var jsonDoc = JsonDocument.Parse(grafanaFile.Content);
        Assert.NotNull(jsonDoc);
    }

    [Fact]
    public void GenerateFiles_CollectorFileHasRequiredSections()
    {
        var doc = CreateSchemaWithMetrics([]);

        var files = _generator.GenerateFiles(doc);
        var collectorFile = files.First(f => f.FileName.EndsWith(".yaml"));

        Assert.Contains("receivers:", collectorFile.Content);
        Assert.Contains("exporters:", collectorFile.Content);
        Assert.Contains("service:", collectorFile.Content);
    }

    // ═══════════════════════════════════════════════════════════════
    // 11. EDGE CASES
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDashboardJson_EventsWithNoMetrics_ReturnsEmptyPanels()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("order.placed", 1001)
        ]);

        var json = _generator.GenerateDashboardJson(doc);
        using var jsonDoc = JsonDocument.Parse(json);

        var panels = jsonDoc.RootElement.GetProperty("panels");
        Assert.Equal(0, panels.GetArrayLength());
    }

    [Fact]
    public void GenerateDashboardJson_UsesSchemaVersion()
    {
        var doc = new SchemaDocument
        {
            Schema = new SchemaHeader
            {
                Name = "TestService",
                Version = "2.1.0",
                Namespace = "Test.Events"
            },
            Events = []
        };

        var json = _generator.GenerateDashboardJson(doc);

        Assert.Contains("2.1.0", json);
    }

    [Fact]
    public void GenerateDashboardJson_UsesMeterNameInDescription()
    {
        var doc = CreateSchemaWithMetrics([], name: "TestService", meterName: "my.custom.meter");

        var json = _generator.GenerateDashboardJson(doc);

        Assert.Contains("my.custom.meter", json);
    }

    [Fact]
    public void GenerateDashboardJson_HistogramMetric_UsesUnitInAxisLabel()
    {
        var doc = CreateSchemaWithMetrics(
        [
            CreateEvent("http.request.completed", 1001, metrics:
            [
                new MetricDefinition
                {
                    Name = "http.request.duration",
                    Type = MetricType.Histogram,
                    Unit = "ms",
                    Description = "Request duration"
                }
            ])
        ]);

        var json = _generator.GenerateDashboardJson(doc);

        Assert.Contains("ms", json);
    }

    // ═══════════════════════════════════════════════════════════════
    // 12. PROMETHEUS METRIC NAME HELPER
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("http.request.count", "http_request_count")]
    [InlineData("app.health.changes", "app_health_changes")]
    [InlineData("simple", "simple")]
    [InlineData("a.b.c.d", "a_b_c_d")]
    public void ToPrometheusName_ConvertsDots(string input, string expected)
    {
        var result = SchemaMetricsDashboardGenerator.ToPrometheusName(input);
        Assert.Equal(expected, result);
    }
}
