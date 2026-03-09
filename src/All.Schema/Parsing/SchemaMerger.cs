using All.Schema.Models;
using All.Schema.Validation;

namespace All.Schema.Parsing;

/// <summary>
/// Merges multiple <see cref="SchemaDocument"/> instances into a unified schema.
/// Resolves imports and validates cross-document constraints.
/// </summary>
public sealed class SchemaMerger
{
    private readonly SchemaParser _parser;
    private readonly SchemaValidator _validator;

    public SchemaMerger(SchemaParser parser, SchemaValidator validator)
    {
        _parser = parser;
        _validator = validator;
    }

    /// <summary>
    /// Merges multiple already-parsed schema documents.
    /// Validates the merged result using <see cref="SchemaValidator"/>.
    /// </summary>
    /// <param name="documents">The parsed schema documents to merge.</param>
    /// <returns>The merged document and any validation errors.</returns>
    public MergeResult Merge(IReadOnlyList<SchemaDocument> documents)
    {
        if (documents.Count == 0)
        {
            return MergeResult.Failure([new SchemaError
            {
                Code = ErrorCodes.InvalidSemver,
                Message = "No schema documents to merge."
            }]);
        }

        // Use the first document's header as the primary
        var primary = documents[0];

        var mergedFields = new List<FieldDefinition>();
        var mergedEnums = new List<EnumDefinition>();
        var mergedEvents = new List<EventDefinition>();
        var mergedImports = new List<string>();

        foreach (var doc in documents)
        {
            mergedFields.AddRange(doc.Fields);
            mergedEnums.AddRange(doc.Enums);
            mergedEvents.AddRange(doc.Events);
            mergedImports.AddRange(doc.Imports);
        }

        var mergedDoc = new SchemaDocument
        {
            Schema = primary.Schema,
            Imports = mergedImports,
            Fields = mergedFields,
            Enums = mergedEnums,
            Events = mergedEvents
        };

        var validationResult = _validator.Validate(documents);

        return new MergeResult(mergedDoc, validationResult.Errors);
    }

    /// <summary>
    /// Parses and merges schema files from disk, resolving imports.
    /// </summary>
    /// <param name="primaryFilePath">The primary schema file path.</param>
    /// <returns>The merged result.</returns>
    public MergeResult MergeFromFile(string primaryFilePath)
    {
        var documents = new List<SchemaDocument>();
        var parseErrors = new List<SchemaError>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ResolveAndParse(primaryFilePath, documents, parseErrors, visited);

        if (parseErrors.Count > 0)
        {
            return MergeResult.Failure(parseErrors);
        }

        return Merge(documents);
    }

    private void ResolveAndParse(
        string filePath,
        List<SchemaDocument> documents,
        List<SchemaError> errors,
        HashSet<string> visited)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!visited.Add(fullPath))
            return; // Already processed — avoid circular imports

        var result = _parser.ParseFile(fullPath);
        if (!result.IsSuccess)
        {
            errors.AddRange(result.Errors);
            return;
        }

        var doc = result.Document!;
        documents.Add(doc);

        // Resolve imports relative to the current file's directory
        var baseDir = Path.GetDirectoryName(fullPath) ?? ".";
        foreach (var import in doc.Imports)
        {
            var importPath = Path.Combine(baseDir, import);
            ResolveAndParse(importPath, documents, errors, visited);
        }
    }
}

/// <summary>
/// Result of merging multiple schema documents.
/// </summary>
public sealed class MergeResult
{
    /// <summary>The merged schema document. Null if merge failed.</summary>
    public SchemaDocument? Document { get; }

    /// <summary>All errors (parse + validation).</summary>
    public IReadOnlyList<SchemaError> Errors { get; }

    /// <summary>Whether the merge succeeded with no errors.</summary>
    public bool IsSuccess => Document is not null && Errors.Count == 0;

    internal MergeResult(SchemaDocument? document, IReadOnlyList<SchemaError> errors)
    {
        Document = document;
        Errors = errors;
    }

    internal static MergeResult Failure(IReadOnlyList<SchemaError> errors) => new(null, errors);
}
