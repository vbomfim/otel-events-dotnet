using System.Text.RegularExpressions;

namespace OtelEvents.Azure.CosmosDb;

/// <summary>
/// Sanitizes CosmosDB SQL query text by replacing string literals with <c>?</c>
/// placeholders to prevent PII leakage in telemetry.
/// </summary>
/// <remarks>
/// <para>
/// CosmosDB SQL uses single-quoted string literals. This sanitizer replaces all
/// single-quoted values with <c>?</c> regardless of content. This ensures that
/// personal data (names, emails, addresses) embedded in query literals is never
/// logged, even when <see cref="OtelEventsCosmosDbOptions.CaptureQueryText"/>
/// is enabled.
/// </para>
/// <para>
/// The query text is also truncated to <see cref="DefaultMaxLength"/> (2048 characters)
/// per the schema field definition (<c>maxLength: 2048</c>).
/// </para>
/// </remarks>
internal static partial class CosmosQuerySanitizer
{
    /// <summary>
    /// Default maximum length for sanitized query text, per schema definition.
    /// </summary>
    internal const int DefaultMaxLength = 2048;

    /// <summary>
    /// Matches single-quoted string literals in CosmosDB SQL.
    /// Handles escaped quotes (backslash-escaped) within strings.
    /// Examples: 'John', 'O\'Brien', '', '123 Main St'
    /// </summary>
    [GeneratedRegex(@"'(?:[^'\\]|\\.)*'")]
    private static partial Regex StringLiteralRegex();

    /// <summary>
    /// Sanitizes the given query text by replacing all single-quoted string literals
    /// with <c>?</c> placeholders, then truncating to <paramref name="maxLength"/>.
    /// </summary>
    /// <param name="queryText">The raw SQL query text. May be null or empty.</param>
    /// <param name="maxLength">Maximum output length. Default: 2048.</param>
    /// <returns>Sanitized query text, or empty string if input is null/empty.</returns>
    internal static string Sanitize(string? queryText, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrEmpty(queryText))
        {
            return string.Empty;
        }

        var result = StringLiteralRegex().Replace(queryText, "?");

        if (result.Length > maxLength)
        {
            result = result[..maxLength];
        }

        return result;
    }
}
