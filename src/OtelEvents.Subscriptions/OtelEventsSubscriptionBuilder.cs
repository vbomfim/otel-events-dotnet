using Microsoft.Extensions.DependencyInjection;

namespace OtelEvents.Subscriptions;

/// <summary>
/// Builder for configuring event subscriptions via lambda handlers and DI-resolved handler classes.
/// Used within the <see cref="OtelEventsSubscriptionExtensions.AddOtelEventsSubscriptions"/> callback.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddOtelEventsSubscriptions(subs =>
/// {
///     subs.On("cosmosdb.throttled", async (ctx, ct) =>
///     {
///         var retryMs = ctx.GetAttribute&lt;long&gt;("retryAfterMs");
///         await Task.Delay(TimeSpan.FromMilliseconds(retryMs), ct);
///     });
///
///     subs.On("*.auth.failed", (ctx, ct) => Task.CompletedTask);
///
///     subs.AddHandler&lt;OrderPlacedHandler&gt;("order.placed");
/// });
/// </code>
/// </example>
public sealed class OtelEventsSubscriptionBuilder
{
    private readonly IServiceCollection _services;

    internal readonly List<SubscriptionRegistration> Registrations = [];

    internal OtelEventsSubscriptionBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Registers a lambda handler for events matching the specified pattern.
    /// </summary>
    /// <param name="eventPattern">
    /// Exact event name (e.g., <c>"order.placed"</c>) or wildcard pattern
    /// with a trailing <c>*</c> (e.g., <c>"cosmosdb.*"</c>).
    /// </param>
    /// <param name="handler">The async handler delegate to invoke when an event matches.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="eventPattern"/> is null or empty.
    /// </exception>
    public OtelEventsSubscriptionBuilder On(
        string eventPattern,
        Func<OtelEventContext, CancellationToken, Task> handler)
    {
        ValidatePattern(eventPattern);
        ArgumentNullException.ThrowIfNull(handler);

        Registrations.Add(new SubscriptionRegistration(eventPattern, handler));
        return this;
    }

    /// <summary>
    /// Registers a DI-resolved handler class for events matching the specified pattern.
    /// The handler is resolved from the service provider per invocation.
    /// </summary>
    /// <typeparam name="THandler">
    /// The handler type implementing <see cref="IOtelEventHandler"/>.
    /// Must be registered in DI separately or will be auto-registered as transient.
    /// </typeparam>
    /// <param name="eventPattern">
    /// Exact event name or wildcard pattern with a trailing <c>*</c>.
    /// </param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="eventPattern"/> is null or empty.
    /// </exception>
    public OtelEventsSubscriptionBuilder AddHandler<THandler>(string eventPattern)
        where THandler : class, IOtelEventHandler
    {
        ValidatePattern(eventPattern);

        // Auto-register the handler type as transient if not already registered
        _services.AddTransient<THandler>();

        Registrations.Add(new SubscriptionRegistration(eventPattern, typeof(THandler)));
        return this;
    }

    private static void ValidatePattern(string eventPattern)
    {
        if (string.IsNullOrEmpty(eventPattern))
        {
            throw new ArgumentException(
                "Event pattern must not be null or empty.",
                nameof(eventPattern));
        }

        if (eventPattern == "*")
        {
            throw new ArgumentException(
                "Bare wildcard \"*\" is not allowed. Use a qualified "
                + "prefix wildcard like \"health.*\".",
                nameof(eventPattern));
        }
    }
}

/// <summary>
/// Internal registration record holding either a lambda handler or a DI handler type.
/// </summary>
internal sealed class SubscriptionRegistration
{
    /// <summary>The event pattern (exact name or wildcard with trailing *).</summary>
    public string EventPattern { get; }

    /// <summary>The prefix for wildcard matching (pattern without trailing *), or null for exact match.</summary>
    public string? WildcardPrefix { get; }

    /// <summary>Whether this registration uses a wildcard pattern.</summary>
    public bool IsWildcard { get; }

    /// <summary>Lambda handler, if registered via <see cref="OtelEventsSubscriptionBuilder.On"/>.</summary>
    public Func<OtelEventContext, CancellationToken, Task>? LambdaHandler { get; }

    /// <summary>DI handler type, if registered via <see cref="OtelEventsSubscriptionBuilder.AddHandler{THandler}"/>.</summary>
    public Type? HandlerType { get; }

    public SubscriptionRegistration(
        string eventPattern,
        Func<OtelEventContext, CancellationToken, Task> lambdaHandler)
    {
        EventPattern = eventPattern;
        LambdaHandler = lambdaHandler;

        if (eventPattern.EndsWith('*'))
        {
            IsWildcard = true;
            WildcardPrefix = eventPattern[..^1]; // strip trailing '*'
        }
    }

    public SubscriptionRegistration(string eventPattern, Type handlerType)
    {
        EventPattern = eventPattern;
        HandlerType = handlerType;

        if (eventPattern.EndsWith('*'))
        {
            IsWildcard = true;
            WildcardPrefix = eventPattern[..^1];
        }
    }
}
