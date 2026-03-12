using OtelEvents.Causality;
using OpenTelemetry.Logs;
using OtelEvents.AspNetCore;
using OtelEvents.Exporter.Json;
using OtelEvents.HealthChecks;
using OtelEvents.Sample.WebApi.Events;
using OtelEvents.Subscriptions;

// ─── Builder ────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry: logging pipeline with causality + JSONL exporter + subscriptions
builder.Services.AddOpenTelemetry()
    .WithLogging(logging =>
    {
        logging.AddOtelEventsCausalityProcessor();
        logging.AddOtelEventsJsonExporter(options =>
        {
            options.EnvironmentProfile = OtelEventsEnvironmentProfile.Development;
        });
        // Subscription processor — dispatches matching events to handlers
        logging.AddProcessor(sp => sp.GetRequiredService<OtelEventsSubscriptionProcessor>());
    })
    .WithMetrics(metrics => metrics.AddMeter("Sample.Events.*"));

// OtelEvents integration packs
builder.Services.AddOtelEventsAspNetCore(options =>
{
    options.ExcludePaths = ["/health"];
});

// Health checks with structured event emission
builder.Services.AddHealthChecks();
builder.Services.AddOtelEventsHealthChecks(options =>
{
    options.EmitStateChangedEvents = true;
    options.EmitReportCompletedEvents = true;
});

// Event subscriptions — react to events in-process
builder.Services.AddOtelEventsSubscriptions(subs =>
{
    subs.On("order.placed", (ctx, ct) =>
    {
        var orderId = ctx.GetAttribute<string>("OrderId");
        var amount = ctx.GetAttribute<double>("Amount");
        Console.WriteLine($"📦 Subscription: Order {orderId} placed for ${amount}");
        return Task.CompletedTask;
    });

    subs.On("order.failed", (ctx, ct) =>
    {
        var orderId = ctx.GetAttribute<string>("OrderId");
        var reason = ctx.GetAttribute<string>("Reason");
        Console.WriteLine($"⚠️ Subscription: Order {orderId} failed — {reason}");
        return Task.CompletedTask;
    });

    subs.On("order.*", (ctx, ct) =>
    {
        Console.WriteLine($"📊 Subscription: Event '{ctx.EventName}' observed at {ctx.Timestamp:HH:mm:ss.fff}");
        return Task.CompletedTask;
    });
});

// ─── App ────────────────────────────────────────────────────────────────────

var app = builder.Build();

app.MapHealthChecks("/health");

// ─── POST /orders — Place an order ──────────────────────────────────────────

app.MapPost("/orders", (OrderRequest request, ILogger<OrderEventSource> logger) =>
{
    var orderId = Guid.NewGuid().ToString("N")[..8];

    // Causal scope: all events emitted within this block share a parent

    logger.OrderPlaced(orderId, request.CustomerId, request.Amount);

    return Results.Created($"/orders/{orderId}", new
    {
        orderId,
        request.CustomerId,
        request.Amount,
        status = "Placed",
    });
});

// ─── POST /orders/{id}/complete — Complete an order ─────────────────────────

app.MapPost("/orders/{id}/complete", (string id, ILogger<OrderEventSource> logger) =>
{

    logger.OrderCompleted(id, "Shipped");

    return Results.Ok(new
    {
        orderId = id,
        status = "Shipped",
    });
});

// ─── POST /orders/{id}/fail — Simulate order failure ────────────────────────

app.MapPost("/orders/{id}/fail", (string id, ILogger<OrderEventSource> logger) =>
{

    var exception = new InvalidOperationException("Payment gateway timeout");

    logger.OrderFailed(id, "Payment processing failed", exception);

    return Results.Problem(
        title: "Order Failed",
        detail: $"Order {id} could not be processed",
        statusCode: StatusCodes.Status500InternalServerError);
});

app.Run();

// ─── Request models ─────────────────────────────────────────────────────────

/// <summary>Request body for creating a new order.</summary>
internal sealed record OrderRequest(string CustomerId, double Amount);
