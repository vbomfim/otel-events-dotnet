namespace All.Schema.Packaging;

/// <summary>
/// Discovers .all.yaml schema files from NuGet package content directories
/// and resolves "package:" import paths to absolute file paths.
/// <para>
/// Import convention: <c>package:PackageName/schema-file.all.yaml</c>
/// </para>
/// </summary>
/// <remarks>
/// This resolver enables cross-package schema sharing. Teams publish .all.yaml
/// schemas as NuGet package content files, and consumers reference them via
/// the <c>package:</c> import prefix in their schema files.
/// </remarks>
public sealed class SchemaPackageResolver
{
    /// <summary>The prefix that identifies a package import path.</summary>
    internal const string PackageImportPrefix = "package:";

    private readonly IReadOnlyList<string> _schemaDirectories;

    /// <summary>
    /// Creates a resolver that searches the given directories for .all.yaml files.
    /// Directories typically point to NuGet package content schema folders.
    /// </summary>
    /// <param name="schemaDirectories">Directories to search for .all.yaml files.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="schemaDirectories"/> is null.</exception>
    public SchemaPackageResolver(IReadOnlyList<string> schemaDirectories)
    {
        _schemaDirectories = schemaDirectories ?? throw new ArgumentNullException(nameof(schemaDirectories));
    }

    /// <summary>
    /// Determines whether the given import path uses the <c>package:</c> prefix.
    /// </summary>
    /// <param name="importPath">The import path to check.</param>
    /// <returns>True if the import uses package resolution.</returns>
    public static bool IsPackageImport(string importPath)
    {
        if (string.IsNullOrEmpty(importPath))
            return false;

        // Must have content after "package:" — at least "package:X/Y"
        return importPath.StartsWith(PackageImportPrefix, StringComparison.Ordinal)
               && importPath.Length > PackageImportPrefix.Length
               && importPath.IndexOf('/', PackageImportPrefix.Length) > PackageImportPrefix.Length;
    }

    /// <summary>
    /// Resolves a <c>package:PackageName/file.all.yaml</c> import to an absolute file path.
    /// Returns null if the package or schema file is not found, or if the path is invalid.
    /// </summary>
    /// <param name="packageImport">The package import path (e.g., "package:MyPkg/common.all.yaml").</param>
    /// <returns>The absolute file path, or null if not found.</returns>
    public string? Resolve(string packageImport)
    {
        if (!IsPackageImport(packageImport))
            return null;

        var withoutPrefix = packageImport[PackageImportPrefix.Length..];
        var separatorIndex = withoutPrefix.IndexOf('/');
        if (separatorIndex <= 0)
            return null;

        var schemaFileName = withoutPrefix[(separatorIndex + 1)..];
        if (string.IsNullOrEmpty(schemaFileName))
            return null;

        // Guard against path traversal in the schema file name
        if (schemaFileName.Contains("..") || Path.IsPathRooted(schemaFileName))
            return null;

        // Search all configured directories for the schema file
        foreach (var dir in _schemaDirectories)
        {
            if (!Directory.Exists(dir))
                continue;

            var candidatePath = Path.GetFullPath(Path.Combine(dir, schemaFileName));

            // Path traversal guard: resolved path must stay within the directory
            var dirNormalized = Path.GetFullPath(dir);
            if (!dirNormalized.EndsWith(Path.DirectorySeparatorChar))
                dirNormalized += Path.DirectorySeparatorChar;

            if (!candidatePath.StartsWith(dirNormalized, StringComparison.Ordinal))
                continue;

            if (File.Exists(candidatePath))
                return candidatePath;
        }

        return null;
    }

    /// <summary>
    /// Discovers all .all.yaml schema files across all configured directories.
    /// </summary>
    /// <returns>A list of discovered package schema sources.</returns>
    public IReadOnlyList<PackageSchemaSource> DiscoverSchemas()
    {
        var results = new List<PackageSchemaSource>();

        foreach (var dir in _schemaDirectories)
        {
            if (!Directory.Exists(dir))
                continue;

            var yamlFiles = Directory.GetFiles(dir, "*.all.yaml", SearchOption.TopDirectoryOnly);
            foreach (var file in yamlFiles)
            {
                results.Add(new PackageSchemaSource
                {
                    SchemaFileName = Path.GetFileName(file),
                    FullPath = Path.GetFullPath(file),
                    SourceDirectory = dir
                });
            }
        }

        return results;
    }
}
