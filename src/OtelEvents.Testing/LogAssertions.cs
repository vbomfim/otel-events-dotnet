using Microsoft.Extensions.Logging;

namespace OtelEvents.Testing;

/// <summary>
/// Fluent assertion extensions for <see cref="InMemoryLogExporter"/> and
/// <see cref="ExportedLogRecord"/>. Provides common test assertion patterns
/// with clear failure messages.
/// </summary>
public static class LogAssertions
{
    /// <summary>
    /// Asserts that at least one log record with the specified event name was emitted.
    /// </summary>
    /// <param name="exporter">The exporter containing captured records.</param>
    /// <param name="eventName">The expected event name to find.</param>
    /// <exception cref="Xunit.Sdk.XunitException">
    /// Thrown when no record with the specified event name is found.
    /// </exception>
    public static void AssertEventEmitted(this InMemoryLogExporter exporter, string eventName)
    {
        var records = exporter.LogRecords;
        var found = records.Any(r => r.EventName == eventName);
        if (!found)
        {
            var emittedEvents = records.Count > 0
                ? string.Join(", ", records.Select(r => $"'{r.EventName}'"))
                : "(none)";

            throw new Xunit.Sdk.XunitException(
                $"Expected event '{eventName}' to be emitted, but it was not found. " +
                $"Emitted events: {emittedEvents}");
        }
    }

    /// <summary>
    /// Asserts that no log records with <see cref="LogLevel.Error"/> or
    /// <see cref="LogLevel.Critical"/> severity were emitted.
    /// </summary>
    /// <param name="exporter">The exporter containing captured records.</param>
    /// <exception cref="Xunit.Sdk.XunitException">
    /// Thrown when error-level records are found.
    /// </exception>
    public static void AssertNoErrors(this InMemoryLogExporter exporter)
    {
        var errors = exporter.LogRecords
            .Where(r => r.LogLevel >= LogLevel.Error)
            .ToList();

        if (errors.Count > 0)
        {
            var details = string.Join(Environment.NewLine,
                errors.Select(r =>
                    $"  [{r.LogLevel}] {r.EventName}: {r.FormattedMessage}" +
                    (r.Exception is not null ? $" ({r.Exception.GetType().Name}: {r.Exception.Message})" : "")));

            throw new Xunit.Sdk.XunitException(
                $"Expected no errors, but found {errors.Count} error-level record(s):{Environment.NewLine}{details}");
        }
    }

    /// <summary>
    /// Asserts that exactly one log record with the specified event name was emitted
    /// and returns it for further assertions.
    /// </summary>
    /// <param name="exporter">The exporter containing captured records.</param>
    /// <param name="eventName">The expected event name.</param>
    /// <returns>The single matching <see cref="ExportedLogRecord"/>.</returns>
    /// <exception cref="Xunit.Sdk.XunitException">
    /// Thrown when zero or more than one matching record is found.
    /// </exception>
    public static ExportedLogRecord AssertSingle(this InMemoryLogExporter exporter, string eventName)
    {
        var records = exporter.LogRecords;
        var matches = records
            .Where(r => r.EventName == eventName)
            .ToList();

        if (matches.Count == 0)
        {
            var emittedEvents = records.Count > 0
                ? string.Join(", ", records.Select(r => $"'{r.EventName}'"))
                : "(none)";

            throw new Xunit.Sdk.XunitException(
                $"Expected exactly one '{eventName}' event, but found none. " +
                $"Emitted events: {emittedEvents}");
        }

        if (matches.Count > 1)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected exactly one '{eventName}' event, but found {matches.Count}.");
        }

        return matches[0];
    }

    /// <summary>
    /// Asserts that the record contains an attribute with the specified key and expected value.
    /// </summary>
    /// <param name="record">The exported log record to inspect.</param>
    /// <param name="key">The attribute key to look for.</param>
    /// <param name="expected">The expected attribute value.</param>
    /// <exception cref="Xunit.Sdk.XunitException">
    /// Thrown when the attribute is missing or has a different value.
    /// </exception>
    public static void AssertAttribute(this ExportedLogRecord record, string key, object? expected)
    {
        if (!record.Attributes.TryGetValue(key, out var actual))
        {
            var availableKeys = record.Attributes.Count > 0
                ? string.Join(", ", record.Attributes.Keys.Select(k => $"'{k}'"))
                : "(none)";

            throw new Xunit.Sdk.XunitException(
                $"Expected attribute '{key}' not found. Available attributes: {availableKeys}");
        }

        if (!Equals(actual, expected))
        {
            throw new Xunit.Sdk.XunitException(
                $"Attribute '{key}' expected value '{expected}' (type: {expected?.GetType().Name ?? "null"}) " +
                $"but found '{actual}' (type: {actual?.GetType().Name ?? "null"}).");
        }
    }
}
