using OtelEvents.Schema.Documentation;
using OtelEvents.Schema.Parsing;

namespace OtelEvents.Cli;

/// <summary>
/// Handles the "docs" subcommand — generates Markdown documentation
/// from a YAML schema file.
/// Usage: all docs &lt;path&gt; [-o &lt;output.md&gt;]
/// </summary>
internal static class DocsCommand
{
    /// <summary>
    /// Executes the docs command: parses a YAML schema and generates Markdown documentation.
    /// </summary>
    /// <param name="schemaPath">Path to the .otel.yaml schema file.</param>
    /// <param name="outputPath">Optional output file path. If null, writes to stdout.</param>
    /// <returns>0 on success, 1 on error.</returns>
    public static int Execute(string schemaPath, string? outputPath)
    {
        if (!File.Exists(schemaPath))
        {
            Console.Error.WriteLine($"Error: Schema file not found: {schemaPath}");
            return 1;
        }

        var parser = new SchemaParser();
        var parseResult = parser.ParseFile(schemaPath);

        if (!parseResult.IsSuccess)
        {
            Console.Error.WriteLine("Error: Failed to parse schema file:");
            foreach (var error in parseResult.Errors)
            {
                Console.Error.WriteLine($"  [{error.Code}] {error.Message}");
            }

            return 1;
        }

        var generator = new SchemaDocumentationGenerator();
        var file = generator.GenerateDocumentation(parseResult.Document!);

        if (outputPath is not null)
        {
            File.WriteAllText(outputPath, file.Content);
            Console.WriteLine($"Documentation generated: {outputPath}");
        }
        else
        {
            Console.Write(file.Content);
        }

        return 0;
    }
}
