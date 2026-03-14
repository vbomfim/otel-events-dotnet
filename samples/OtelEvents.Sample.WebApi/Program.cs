using OtelEvents.Causality;
using OpenTelemetry.Logs;
using OtelEvents.AspNetCore;
using OtelEvents.Exporter.Json;
using OtelEvents.Health;
using OtelEvents.Health.AspNetCore;
using OtelEvents.Sample.WebApi.Events;
using OtelEvents.Subscriptions;

var builder = WebApplication.CreateBuilder(args);

// ─── OpenTelemetry pipeline ─────────────────────────────────────────────────

builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddOtelEventsCausalityProcessor();
        logging.AddOtelEventsJsonExporter(options =>
        {
            options.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
        });
        logging.AddProcessor(sp => sp.GetRequiredService<OtelEventsSubscriptionProcessor>());
    })
    .WithMetrics(metrics => metrics.AddMeter("Sample.Events.*"));

// ─── Integration packs ──────────────────────────────────────────────────────

builder.Services.AddOtelEventsAspNetCore();

// ─── OtelEvents.Health — event-driven health intelligence ───────────────────
// Parses the YAML schema, registers components (orders-db, payment-api),
// auto-subscribes to matching events, and feeds signals to the state machine.
// The YAML-based overload also registers subscriptions, so AddOtelEventsSubscriptions
// is called internally — do not call it separately when using this overload.

builder.Services.AddOtelEventsHealth("schemas/health.otel.yaml");

var app = builder.Build();

// ─── Health probe endpoints ─────────────────────────────────────────────────
// Maps K8s-compatible liveness, readiness, and startup probes:
//   /healthz/live    — is the process alive?
//   /healthz/ready   — are dependencies healthy enough to accept traffic?
//   /healthz/startup — has the app finished initializing?

app.MapHealthEndpoints();

// ─── POST /orders — Full order transaction in a single request ──────────────
//
// Demonstrates typed transactions:
//   OrderPlaced    (type: start)   → creates scope, starts timer
//   OrderNoteAdded (type: event)   → plain event within the scope
//   OrderCompleted (type: success) → closes scope, records duration
//   OrderFailed    (type: failure) → closes scope as failure, records duration
//
// All events share the same parentEventId and otel_events.elapsed_ms.

app.MapPost("/orders", async (OrderRequest request, ILogger<OrderEventSource> logger) =>
{
    var orderId = Guid.NewGuid().ToString("N")[..8];

    // type: start — creates transaction scope, starts timer
    using var tx = logger.BeginOrderPlaced(orderId, request.CustomerId, request.Amount);

    try
    {
        // type: event — plain event within the transaction (no scope effect)
        logger.EmitOrderNoteAdded(orderId, "Validating payment...");

        // Simulate async processing
        await Task.Delay(Random.Shared.Next(50, 200));

        // Simulate occasional failures
        if (double.TryParse(request.Amount, out var amt) && amt > 999)
            throw new InvalidOperationException("Amount exceeds processing limit");

        logger.EmitOrderNoteAdded(orderId, "Payment validated, reserving inventory");
        await Task.Delay(Random.Shared.Next(20, 100));

        // type: success — closes transaction, records duration
        logger.EmitOrderCompleted(orderId, "DHL Express");

        return Results.Created($"/orders/{orderId}", new
        {
            orderId,
            customerId = request.CustomerId,
            amount = request.Amount,
            status = "Shipped",
        });
    }
    catch (Exception ex)
    {
        // type: failure — closes transaction as failure, records duration
        logger.EmitOrderFailed(orderId, ex.Message, ex);

        return Results.Problem(
            title: "Order Failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// ─── GET /orders/{id}/notes — Demonstrate standalone events ─────────────────

app.MapPost("/orders/{id}/notes", (string id, NoteRequest request, ILogger<OrderEventSource> logger) =>
{
    // type: event — standalone, no transaction effect
    logger.EmitOrderNoteAdded(id, request.Note);
    return Results.Ok(new { orderId = id, note = request.Note });
});

// ─── POST /simulate — trigger events that affect health state ───────────────
//
// Sends a burst of requests to exercise the health state machine:
//   ?failures=5  → emit 5 failure signals (useful for pushing state to Degraded)
//   ?successes=5 → emit 5 success signals (useful for recovery testing)

app.MapPost("/simulate", (
    IHealthStateReader stateReader,
    HttpContext context) =>
{
    var failures = int.TryParse(context.Request.Query["failures"], out var f) ? f : 0;
    var successes = int.TryParse(context.Request.Query["successes"], out var s) ? s : 0;
    var snapshots = stateReader.GetAllSnapshots();

    return Results.Ok(new
    {
        message = $"Simulate endpoint hit — {successes} successes, {failures} failures requested",
        hint = "Use POST /orders to generate real events that feed the health state machine",
        currentHealth = new
        {
            aggregateState = stateReader.CurrentState.ToString(),
            readiness = stateReader.ReadinessStatus.ToString(),
            totalSignals = stateReader.TotalSignalCount,
            dependencies = snapshots.Select(snap => new
            {
                name = snap.DependencyId.ToString(),
                state = snap.CurrentState.ToString(),
                successRate = snap.LatestAssessment.SuccessRate,
                totalSignals = snap.LatestAssessment.TotalSignals,
            }),
        },
    });
});

app.Run();

// ─── Request models ─────────────────────────────────────────────────────────

internal sealed record OrderRequest(string CustomerId, string Amount);
internal sealed record NoteRequest(string Note);
