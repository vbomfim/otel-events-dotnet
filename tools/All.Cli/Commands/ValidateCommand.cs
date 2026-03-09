using System.CommandLine;
using All.Schema.Parsing;
using All.Schema.Validation;

namespace All.Cli.Commands;

/// <summary>
/// Validates .all.yaml schema files by parsing and running all validation rules.
/// Exit code 0 = valid, 1 = errors found.
/// </summary>
public static class ValidateCommand
{
    /// <summary>
    /// Creates the System.CommandLine <see cref="System.CommandLine.Command"/> for "validate".
    /// </summary>
    public static Command Create()
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Path to .all.yaml schema file to validate"
        };

        var cmd = new Command("validate", "Validate an .all.yaml schema file");
        cmd.Arguments.Add(pathArg);

        cmd.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArg)!;
            return Execute(path, Console.Out, Console.Error);
        });

        return cmd;
    }

    /// <summary>
    /// Executes schema validation against the specified file.
    /// </summary>
    /// <param name="path">Path to the .all.yaml schema file.</param>
    /// <param name="stdout">Writer for standard output messages.</param>
    /// <param name="stderr">Writer for error messages.</param>
    /// <returns>0 if valid, 1 if errors found.</returns>
    internal static int Execute(string path, TextWriter stdout, TextWriter stderr)
    {
        if (!File.Exists(path))
        {
            stderr.WriteLine($"Error: File not found: {path}");
            return 1;
        }

        var parser = new SchemaParser();
        var parseResult = parser.ParseFile(path);

        if (!parseResult.IsSuccess)
        {
            stderr.WriteLine($"Schema parsing failed with {parseResult.Errors.Count} error(s):");
            foreach (var error in parseResult.Errors)
            {
                stderr.WriteLine($"  {error}");
            }
            return 1;
        }

        var validator = new SchemaValidator();
        var validationResult = validator.Validate(parseResult.Document!);

        if (!validationResult.IsValid)
        {
            stderr.WriteLine($"Validation failed with {validationResult.Errors.Count} error(s):");
            foreach (var error in validationResult.Errors)
            {
                stderr.WriteLine($"  {error}");
            }
            return 1;
        }

        stdout.WriteLine($"✓ Schema '{parseResult.Document!.Schema.Name}' is valid.");
        return 0;
    }
}
