using All.Schema.Models;
using All.Schema.Validation;

namespace All.Schema.Parsing;

/// <summary>
/// Result of parsing a YAML schema file.
/// Contains either a parsed document or parse-level errors.
/// </summary>
public sealed class ParseResult
{
    /// <summary>The parsed schema document. Null if parsing failed.</summary>
    public SchemaDocument? Document { get; }

    /// <summary>Parse errors encountered (file size, YAML syntax, etc.).</summary>
    public IReadOnlyList<SchemaError> Errors { get; }

    /// <summary>Whether parsing succeeded.</summary>
    public bool IsSuccess => Document is not null && Errors.Count == 0;

    private ParseResult(SchemaDocument? document, IReadOnlyList<SchemaError> errors)
    {
        Document = document;
        Errors = errors;
    }

    internal static ParseResult Success(SchemaDocument document) => new(document, []);
    internal static ParseResult Failure(IReadOnlyList<SchemaError> errors) => new(null, errors);
    internal static ParseResult Failure(SchemaError error) => new(null, [error]);
}
