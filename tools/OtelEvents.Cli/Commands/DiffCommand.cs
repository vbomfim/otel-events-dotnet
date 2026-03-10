using System.CommandLine;
using OtelEvents.Schema.Comparison;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Cli.Commands;

/// <summary>
/// Compares two schema versions and reports structural differences.
/// Exit code 0 = no breaking changes, 1 = error, 2 = breaking changes detected.
/// </summary>
public static class DiffCommand
{
    /// <summary>
    /// Creates the System.CommandLine <see cref="System.CommandLine.Command"/> for "diff".
    /// </summary>
    public static Command Create()
    {
        var oldArg = new Argument<string>("old")
        {
            Description = "Path to the old (baseline) .otel.yaml schema file"
        };

        var newArg = new Argument<string>("new")
        {
            Description = "Path to the new .otel.yaml schema file"
        };

        var cmd = new Command("diff", "Compare two schema versions and detect breaking changes");
        cmd.Arguments.Add(oldArg);
        cmd.Arguments.Add(newArg);

        cmd.SetAction(parseResult =>
        {
            var oldPath = parseResult.GetValue(oldArg)!;
            var newPath = parseResult.GetValue(newArg)!;
            return Execute(oldPath, newPath, Console.Out, Console.Error);
        });

        return cmd;
    }

    /// <summary>
    /// Executes a diff between two schema files.
    /// </summary>
    /// <param name="oldPath">Path to the old schema file.</param>
    /// <param name="newPath">Path to the new schema file.</param>
    /// <param name="stdout">Writer for standard output messages.</param>
    /// <param name="stderr">Writer for error messages.</param>
    /// <returns>0 if no breaking changes, 1 if error, 2 if breaking changes detected.</returns>
    internal static int Execute(string oldPath, string newPath, TextWriter stdout, TextWriter stderr)
    {
        if (!File.Exists(oldPath))
        {
            stderr.WriteLine($"Error: File not found: {oldPath}");
            return 1;
        }

        if (!File.Exists(newPath))
        {
            stderr.WriteLine($"Error: File not found: {newPath}");
            return 1;
        }

        var parser = new SchemaParser();

        var oldResult = parser.ParseFile(oldPath);
        if (!oldResult.IsSuccess)
        {
            stderr.WriteLine($"Failed to parse old schema with {oldResult.Errors.Count} error(s):");
            foreach (var error in oldResult.Errors)
            {
                stderr.WriteLine($"  {error}");
            }
            return 1;
        }

        var newResult = parser.ParseFile(newPath);
        if (!newResult.IsSuccess)
        {
            stderr.WriteLine($"Failed to parse new schema with {newResult.Errors.Count} error(s):");
            foreach (var error in newResult.Errors)
            {
                stderr.WriteLine($"  {error}");
            }
            return 1;
        }

        var comparer = new SchemaComparer();
        var comparison = comparer.Compare(oldResult.Document!, newResult.Document!);

        if (comparison.Changes.Count == 0)
        {
            stdout.WriteLine("✓ No changes detected between schemas.");
            return 0;
        }

        stdout.WriteLine($"Found {comparison.Changes.Count} change(s):");
        stdout.WriteLine();

        foreach (var change in comparison.Changes)
        {
            var prefix = change.IsBreaking ? "  ✗ [BREAKING]" : "  ✓ [OK]";
            stdout.WriteLine($"{prefix} {change.Description}");
        }

        stdout.WriteLine();

        if (comparison.HasBreakingChanges)
        {
            stdout.WriteLine($"⚠ {comparison.BreakingChangeCount} breaking change(s) detected.");
            return 2;
        }

        stdout.WriteLine("✓ All changes are backward-compatible.");
        return 0;
    }
}
