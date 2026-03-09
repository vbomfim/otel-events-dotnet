namespace OtelEvents.Schema.Models;

/// <summary>
/// Controls how the OTEL Meter instance is created in generated code.
/// </summary>
public enum MeterLifecycle
{
    /// <summary>
    /// Static Meter instance (default). Creates a <c>private static readonly Meter</c>.
    /// Suitable for long-lived services where the Meter is never disposed.
    /// </summary>
    Static,

    /// <summary>
    /// DI-friendly Meter via <c>IMeterFactory</c> constructor injection.
    /// Generates an instance class implementing <c>IDisposable</c> with
    /// a service registration extension method.
    /// </summary>
    DI
}
