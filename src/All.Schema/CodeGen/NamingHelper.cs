using System.Text;

namespace All.Schema.CodeGen;

/// <summary>
/// Naming convention helpers for converting YAML names to C# identifiers.
/// </summary>
public static class NamingHelper
{
    /// <summary>
    /// Converts a dot/underscore/hyphen-separated name to PascalCase.
    /// Examples: "order.placed" → "OrderPlaced", "http_method" → "HttpMethod".
    /// </summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var sb = new StringBuilder(name.Length);
        var capitalizeNext = true;

        foreach (var ch in name)
        {
            if (ch is '.' or '_' or '-')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
            capitalizeNext = false;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts a name to camelCase for use as method parameters.
    /// Examples: "OrderId" → "orderId", "userId" → "userId".
    /// </summary>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var pascal = ToPascalCase(name);
        if (pascal.Length == 0)
            return "_";
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    /// <summary>
    /// Converts a dot-separated event name to a C# method name.
    /// Example: "order.placed" → "OrderPlaced".
    /// </summary>
    public static string ToMethodName(string eventName) => ToPascalCase(eventName);

    /// <summary>
    /// Converts a dot-separated metric name to a C# field name with "s_" prefix.
    /// Example: "order.placed.count" → "s_orderPlacedCount".
    /// </summary>
    public static string ToMetricFieldName(string metricName) =>
        "s_" + ToCamelCase(metricName);

    /// <summary>
    /// Converts a dot-separated metric name to a PascalCase property name.
    /// Used in DI mode where metrics are instance properties.
    /// Example: "order.placed.count" → "OrderPlacedCount".
    /// </summary>
    public static string ToMetricPropertyName(string metricName) =>
        ToPascalCase(metricName);

    /// <summary>
    /// Gets the last segment of a dot-separated name.
    /// Example: "order.placed.amount" → "amount".
    /// </summary>
    public static string GetLastSegment(string dottedName)
    {
        var lastDot = dottedName.LastIndexOf('.');
        return lastDot >= 0 ? dottedName[(lastDot + 1)..] : dottedName;
    }

    /// <summary>
    /// Sanitizes a string to be a valid C# identifier.
    /// Replaces invalid characters with underscores.
    /// </summary>
    public static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "_";

        var sb = new StringBuilder(name.Length);

        // First character must be letter or underscore
        var first = name[0];
        sb.Append(char.IsLetter(first) || first == '_' ? first : '_');

        for (var i = 1; i < name.Length; i++)
        {
            var ch = name[i];
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        return sb.ToString();
    }
}
