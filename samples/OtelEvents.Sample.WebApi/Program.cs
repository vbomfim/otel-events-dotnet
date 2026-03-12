using OtelEvents.Causality;
using OpenTelemetry.Logs;
using OtelEvents.AspNetCore;
using OtelEvents.Exporter.Json;
using OtelEvents.HealthChecks;
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

builder.Services.AddOtelEventsAspNetCore(options =>
{
    options.ExcludePaths = ["/health"];
});

builder.Services.AddHealthChecks();
builder.Services.AddOtelEventsHealthChecks();

// ─── Event subscriptions — react to events in-process ───────────────────────

builder.Services.AddOtelEventsSubscriptions(subs =>
{
    subs.On("order.placed", (ctx, ct) =>
    {
        Console.WriteLine($"📦 Order {ctx.GetAttribute<string>("OrderId")} placed for ${ctx.GetAttribute<double>("Amount")}");
        return Task.CompletedTask;
    });

    subs.On("order.failed", (ctx, ct) =>
    {
        Console.WriteLine($"⚠️ Order {ctx.GetAttribute<string>("OrderId")} failed: {ctx.GetAttribute<string>("Reason")}");
        return Task.CompletedTask;
    });

    subs.On("order.completed", (ctx, ct) =>
    {
        Console.WriteLine($"✅ Order {ctx.GetAttribute<string>("OrderId")} shipped via {ctx.GetAttribute<string>("Carrier")}");
        return Task.CompletedTask;
    });
});

var app = builder.Build();
app.MapHealthChecks("/health");

// ─── POST /orders — Full order transaction in a single request ──────────────
//
// Demonstrates typed transactions:
//   order.placed    (type: start)   → creates scope, starts timer
//   order.note.added (type: event)  → plain event within the scope
//   order.completed (type: success) → closes scope, records duration
//   order.failed    (type: failure) → closes scope as failure, records duration
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
        logger.OrderNoteAdded(orderId, "Validating payment...");

        // Simulate async processing
        await Task.Delay(Random.Shared.Next(50, 200));

        // Simulate occasional failures
        if (double.TryParse(request.Amount, out var amt) && amt > 999)
            throw new InvalidOperationException("Amount exceeds processing limit");

        logger.OrderNoteAdded(orderId, "Payment validated, reserving inventory");
        await Task.Delay(Random.Shared.Next(20, 100));

        // type: success — closes transaction, records duration
        logger.OrderCompleted(orderId, "DHL Express");

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
        logger.OrderFailed(orderId, ex.Message, ex);

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
    logger.OrderNoteAdded(id, request.Note);
    return Results.Ok(new { orderId = id, note = request.Note });
});

app.Run();

// ─── Request models ─────────────────────────────────────────────────────────

internal sealed record OrderRequest(string CustomerId, string Amount);
internal sealed record NoteRequest(string Note);
