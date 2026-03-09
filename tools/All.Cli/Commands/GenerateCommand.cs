using System.CommandLine;
using All.Schema.CodeGen;
using All.Schema.Parsing;
using All.Schema.Validation;

namespace All.Cli.Commands;

/// <summary>
/// Generates C# source files from .all.yaml schema files.
/// Exit code 0 = success, 1 = errors.
/// </summary>
public static class GenerateCommand
{
    /// <summary>
    /// Creates the System.CommandLine <see cref="System.CommandLine.Command"/> for "generate".
    /// </summary>
    public static Command Create()
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Path to .all.yaml schema file"
        };

        var outputOption = new Option<string>("--output", "-o")
        {
            Description = "Output directory for generated C# files",
            Required = true
        };

        var cmd = new Command("generate", "Generate C# source files from an .all.yaml schema");
        cmd.Arguments.Add(pathArg);
        cmd.Options.Add(outputOption);

        cmd.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArg)!;
            var output = parseResult.GetValue(outputOption)!;
            return Execute(path, output, Console.Out, Console.Error);
        });

        return cmd;
    }

    /// <summary>
    /// Executes C# code generation from the specified schema file.
    /// </summary>
    /// <param name="path">Path to the .all.yaml schema file.</param>
    /// <param name="outputDir">Output directory for generated files.</param>
    /// <param name="stdout">Writer for standard output messages.</param>
    /// <param name="stderr">Writer for error messages.</param>
    /// <returns>0 if generation succeeded, 1 if errors occurred.</returns>
    internal static int Execute(string path, string outputDir, TextWriter stdout, TextWriter stderr)
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

        var generator = new CodeGenerator();
        var generatedFiles = generator.GenerateFromSchema(parseResult.Document!);

        Directory.CreateDirectory(outputDir);

        foreach (var file in generatedFiles)
        {
            var filePath = Path.Combine(outputDir, file.FileName);
            File.WriteAllText(filePath, file.Content);
            stdout.WriteLine($"  Generated: {file.FileName}");
        }

        stdout.WriteLine($"✓ Generated {generatedFiles.Count} file(s) to {outputDir}");
        return 0;
    }
}
