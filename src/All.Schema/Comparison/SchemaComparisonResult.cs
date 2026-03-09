namespace All.Schema.Comparison;

/// <summary>
/// The result of comparing two schema versions.
/// Contains all detected structural changes and their breaking classification.
/// </summary>
public sealed class SchemaComparisonResult
{
    /// <summary>All detected changes between old and new schema.</summary>
    public IReadOnlyList<SchemaChange> Changes { get; }

    /// <summary>Whether any change is classified as breaking.</summary>
    public bool HasBreakingChanges => Changes.Any(c => c.IsBreaking);

    /// <summary>The number of breaking changes.</summary>
    public int BreakingChangeCount => Changes.Count(c => c.IsBreaking);

    internal SchemaComparisonResult(IReadOnlyList<SchemaChange> changes)
    {
        Changes = changes;
    }
}
