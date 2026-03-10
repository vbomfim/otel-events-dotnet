namespace OtelEvents.Schema.Packaging;

/// <summary>
/// Metadata for a schema file to be included in a NuGet package.
/// Used by MSBuild integration to configure content packaging.
/// </summary>
public sealed class SchemaPackageMetadata
{
    /// <summary>The absolute path to the source .otel.yaml file.</summary>
    public required string SourcePath { get; init; }

    /// <summary>The NuGet package path (e.g., "contentFiles/any/any/schemas/events.otel.yaml").</summary>
    public required string PackagePath { get; init; }

    /// <summary>The MSBuild build action for this file.</summary>
    public required string BuildAction { get; init; }

    /// <summary>Whether this file should be copied to the consuming project's output directory.</summary>
    public required bool CopyToOutput { get; init; }
}

/// <summary>
/// Provides helpers for discovering and packaging .otel.yaml schema files
/// as NuGet package content. Used by MSBuild targets integration.
/// </summary>
public static class SchemaPackageTargets
{
    /// <summary>NuGet content files path prefix for schema files.</summary>
    internal const string ContentFilesPrefix = "contentFiles/any/any/schemas/";

    /// <summary>
    /// Finds all .otel.yaml schema files in the given directory, excluding bin/ and obj/.
    /// </summary>
    /// <param name="projectDirectory">The project root directory to search.</param>
    /// <returns>List of absolute file paths to discovered schema files.</returns>
    public static IReadOnlyList<string> FindSchemaFiles(string projectDirectory)
    {
        if (!Directory.Exists(projectDirectory))
            return [];

        return Directory
            .GetFiles(projectDirectory, "*.otel.yaml", SearchOption.AllDirectories)
            .Where(f => !IsExcludedDirectory(f, projectDirectory))
            .ToList();
    }

    /// <summary>
    /// Returns the NuGet package path for a schema file.
    /// Schemas are placed in <c>contentFiles/any/any/schemas/</c> for broad compatibility.
    /// </summary>
    /// <param name="relativeOrFileName">The file name or relative path of the schema file.</param>
    /// <returns>The NuGet content path.</returns>
    public static string GetPackagePath(string relativeOrFileName)
    {
        var fileName = Path.GetFileName(relativeOrFileName);
        return $"{ContentFilesPrefix}{fileName}";
    }

    /// <summary>
    /// Generates NuGet package metadata for all .otel.yaml files in a project directory.
    /// </summary>
    /// <param name="projectDirectory">The project root directory.</param>
    /// <returns>Package metadata entries for each discovered schema file.</returns>
    public static IReadOnlyList<SchemaPackageMetadata> GeneratePackageMetadata(string projectDirectory)
    {
        var files = FindSchemaFiles(projectDirectory);

        return files.Select(f => new SchemaPackageMetadata
        {
            SourcePath = f,
            PackagePath = GetPackagePath(f),
            BuildAction = "Content",
            CopyToOutput = true
        }).ToList();
    }

    private static bool IsExcludedDirectory(string filePath, string projectRoot)
    {
        var relativePath = Path.GetRelativePath(projectRoot, filePath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase)
                           || p.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }
}
