namespace All.Schema.Packaging;

/// <summary>
/// Represents a discovered .all.yaml schema from a NuGet package content directory.
/// Immutable data class — carries the metadata about where a package schema was found.
/// </summary>
public sealed class PackageSchemaSource
{
    /// <summary>The file name of the schema (e.g., "common.all.yaml").</summary>
    public required string SchemaFileName { get; init; }

    /// <summary>The full absolute path to the schema file on disk.</summary>
    public required string FullPath { get; init; }

    /// <summary>The source directory where the schema was discovered.</summary>
    public required string SourceDirectory { get; init; }

    public override string ToString() => $"{SchemaFileName} ({SourceDirectory})";
}
