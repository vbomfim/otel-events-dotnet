namespace All.Schema.CodeGen;

/// <summary>
/// Represents a single generated C# source file.
/// Contains the file name and the full source text.
/// </summary>
/// <param name="FileName">The suggested file name (e.g., "OrderEvents.g.cs").</param>
/// <param name="Content">The full C# source text.</param>
public sealed record GeneratedFile(string FileName, string Content);
