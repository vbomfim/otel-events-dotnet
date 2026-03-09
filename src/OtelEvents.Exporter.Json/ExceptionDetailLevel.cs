namespace OtelEvents.Exporter.Json;

/// <summary>Controls how much exception detail is included in the JSON envelope.</summary>
public enum ExceptionDetailLevel
{
    /// <summary>Type, message, stack trace (method names only), inner exceptions.</summary>
    Full,

    /// <summary>Type and message only. Default for Production/Staging.</summary>
    TypeAndMessage,

    /// <summary>Exception type name only. Minimal disclosure.</summary>
    TypeOnly,
}
