namespace OtelEvents.Subscriptions;

/// <summary>
/// Interface for DI-resolved event subscription handlers.
/// <para>
/// Implement this interface and register with
/// <see cref="OtelEventsSubscriptionBuilder.AddHandler{THandler}"/> to handle events
/// resolved through the dependency injection container.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public class OrderPlacedHandler : IOtelEventHandler
/// {
///     public Task HandleAsync(OtelEventContext context, CancellationToken cancellationToken)
///     {
///         var orderId = context.GetAttribute&lt;string&gt;("orderId");
///         // process the order event
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IOtelEventHandler
{
    /// <summary>
    /// Handles an event subscription notification.
    /// </summary>
    /// <param name="context">The immutable event context snapshot.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative shutdown.</param>
    /// <returns>A task representing the async handler operation.</returns>
    Task HandleAsync(OtelEventContext context, CancellationToken cancellationToken);
}
