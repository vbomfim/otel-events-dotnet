using All.Schema.Models;
using All.Schema.Packaging;
using All.Schema.Validation;

namespace All.Schema.Parsing;

/// <summary>
/// Merges multiple <see cref="SchemaDocument"/> instances into a unified schema.
/// Resolves imports (local files and NuGet packages) and validates cross-document constraints.
/// </summary>
public sealed class SchemaMerger
{
    private readonly SchemaParser _parser;
    private readonly SchemaValidator _validator;
    private readonly SchemaPackageResolver? _packageResolver;

    /// <summary>
    /// Creates a merger for local-only schema resolution.
    /// Package imports (<c>package:</c> prefix) will fail with an error.
    /// </summary>
    public SchemaMerger(SchemaParser parser, SchemaValidator validator)
    {
        _parser = parser;
        _validator = validator;
    }

    /// <summary>
    /// Creates a merger with cross-package schema resolution support.
    /// </summary>
    /// <param name="parser">The YAML schema parser.</param>
    /// <param name="validator">The schema validator.</param>
    /// <param name="packageResolver">Resolver for <c>package:</c> imports from NuGet packages.</param>
    public SchemaMerger(SchemaParser parser, SchemaValidator validator, SchemaPackageResolver packageResolver)
    {
        _parser = parser;
        _validator = validator;
        _packageResolver = packageResolver;
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

        // Resolve imports BEFORE adding this document — dependencies must come first
        // so the validator can collect shared fields/enums before validating refs.
        var baseDir = Path.GetFullPath(Path.GetDirectoryName(fullPath) ?? ".");
        var baseDirNormalized = baseDir.EndsWith(Path.DirectorySeparatorChar)
            ? baseDir
            : baseDir + Path.DirectorySeparatorChar;
        foreach (var import in doc.Imports)
        {
            if (SchemaPackageResolver.IsPackageImport(import))
            {
                ResolvePackageImport(import, documents, errors, visited);
                continue;
            }

            var importPath = Path.GetFullPath(Path.Combine(baseDir, import));

            // Path traversal guard: import must stay within the schema directory
            if (!importPath.StartsWith(baseDirNormalized, StringComparison.Ordinal))
            {
                errors.Add(new SchemaError
                {
                    Code = ErrorCodes.ImportPathTraversal,
                    Message = $"Import '{import}' resolves to '{importPath}' which is outside the schema directory '{baseDir}'."
                });
                continue;
            }

            ResolveAndParse(importPath, documents, errors, visited);
        }

        // Add this document AFTER all imports are resolved — ensures dependency order
        documents.Add(doc);
    }

    private void ResolvePackageImport(
        string import,
        List<SchemaDocument> documents,
        List<SchemaError> errors,
        HashSet<string> visited)
    {
        var resolvedPath = _packageResolver?.Resolve(import);

        if (resolvedPath is null)
        {
            errors.Add(new SchemaError
            {
                Code = ErrorCodes.PackageSchemaNotFound,
                Message = $"Package import '{import}' could not be resolved. "
                          + "Ensure the NuGet package is referenced and contains the schema file."
            });
            return;
        }

        // Delegate to the standard resolve-and-parse with circular import protection
        ResolveAndParse(resolvedPath, documents, errors, visited);
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
