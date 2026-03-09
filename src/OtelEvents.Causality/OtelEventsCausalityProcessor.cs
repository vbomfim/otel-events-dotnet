using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OtelEvents.Causality;

/// <summary>
/// OTEL Log Processor that adds causal linking attributes to LogRecords.
/// Generates a unique event ID (UUID v7, time-sortable) for every LogRecord
/// and reads the parent event ID from the ambient <see cref="OtelEventsCausalityContext"/>.
/// </summary>
/// <remarks>
/// Register in the OTEL pipeline:
/// <code>
/// builder.Logging.AddOpenTelemetry(options =>
///     options.AddProcessor(new OtelEventsCausalityProcessor()));
/// </code>
/// </remarks>
public sealed class OtelEventsCausalityProcessor : BaseProcessor<LogRecord>
{
    /// <summary>
    /// Called when a LogRecord is ready to be exported.
    /// Enriches the record with causal linking attributes.
    /// </summary>
    /// <param name="logRecord">The LogRecord to enrich.</param>
    public override void OnEnd(LogRecord logRecord)
    {
        // Generate unique event ID (UUID v7 — time-sortable)
        var eventId = Uuid7.FormatEventId();

        // Read parent event ID from ambient context
        var parentEventId = OtelEventsCausalityContext.CurrentParentEventId;

        // Build the new attributes list
        var existingAttributes = logRecord.Attributes;
        var newCount = (existingAttributes?.Count ?? 0) + 1 + (parentEventId is not null ? 1 : 0);
        var attributes = new List<KeyValuePair<string, object?>>(newCount);

        // Preserve existing attributes
        if (existingAttributes is not null)
        {
            foreach (var attr in existingAttributes)
            {
                attributes.Add(attr);
            }
        }

        // Add causal linking attributes
        attributes.Add(new KeyValuePair<string, object?>("all.event_id", eventId));

        if (parentEventId is not null)
        {
            attributes.Add(new KeyValuePair<string, object?>("all.parent_event_id", parentEventId));
        }

        logRecord.Attributes = attributes;
    }
}
