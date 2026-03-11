using OtelEvents.Schema.CodeGen;
using OtelEvents.Schema.Parsing;
using OtelEvents.Schema.Validation;

namespace OtelEvents.Schema.Build;

/// <summary>
/// Orchestrates the schema code generation pipeline: parse → validate → generate → write.
/// This is a pure C# class with no MSBuild dependencies, making it independently testable.
/// </summary>
public sealed class SchemaCodeGenRunner
{
    private readonly SchemaParser _parser = new();
    private readonly SchemaValidator _validator = new();
    private readonly CodeGenerator _generator = new();

    /// <summary>
    /// Runs code generation for a single schema file, writing generated C# files to the output directory.
    /// </summary>
    /// <param name="schemaFilePath">Absolute path to the .otel.yaml schema file.</param>
    /// <param name="outputDirectory">Directory where generated .g.cs files will be written.</param>
    /// <returns>The result of the code generation operation.</returns>
    public CodeGenResult Generate(string schemaFilePath, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        // Step 1: Parse the schema file
        var parseResult = _parser.ParseFile(schemaFilePath);
        if (!parseResult.IsSuccess)
        {
            return CodeGenResult.Failed(
                schemaFilePath,
                parseResult.Errors.Select(e => $"{e.Code}: {e.Message}").ToList());
        }

        var document = parseResult.Document!;

        // Step 2: Validate the parsed document
        var validationResult = _validator.Validate(document);
        if (!validationResult.IsValid)
        {
            return CodeGenResult.Failed(
                schemaFilePath,
                validationResult.Errors.Select(e => $"{e.Code}: {e.Message}").ToList());
        }

        // Step 3: Generate C# source files
        var generatedFiles = _generator.GenerateFromSchema(document);

        // Step 4: Write files to the output directory
        Directory.CreateDirectory(outputDirectory);

        var writtenFiles = new List<string>();
        foreach (var file in generatedFiles)
        {
            var outputPath = Path.Combine(outputDirectory, file.FileName);
            File.WriteAllText(outputPath, file.Content);
            writtenFiles.Add(outputPath);
        }

        return CodeGenResult.Succeeded(schemaFilePath, writtenFiles);
    }
}

/// <summary>
/// Result of a code generation operation for a single schema file.
/// </summary>
public sealed class CodeGenResult
{
    /// <summary>Whether the code generation completed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>The schema file that was processed.</summary>
    public string SchemaFilePath { get; }

    /// <summary>Paths of generated files (populated on success).</summary>
    public IReadOnlyList<string> GeneratedFiles { get; }

    /// <summary>Error messages (populated on failure).</summary>
    public IReadOnlyList<string> Errors { get; }

    private CodeGenResult(bool isSuccess, string schemaFilePath,
        IReadOnlyList<string> generatedFiles, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        SchemaFilePath = schemaFilePath;
        GeneratedFiles = generatedFiles;
        Errors = errors;
    }

    internal static CodeGenResult Succeeded(string schemaFilePath, IReadOnlyList<string> generatedFiles)
        => new(true, schemaFilePath, generatedFiles, []);

    internal static CodeGenResult Failed(string schemaFilePath, IReadOnlyList<string> errors)
        => new(false, schemaFilePath, [], errors);
}
