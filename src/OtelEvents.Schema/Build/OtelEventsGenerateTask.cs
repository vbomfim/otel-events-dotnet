using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace OtelEvents.Schema.Build;

/// <summary>
/// MSBuild task that generates C# source files from .otel.yaml schema files.
/// Wraps <see cref="SchemaCodeGenRunner"/> to integrate with the MSBuild pipeline.
/// </summary>
/// <remarks>
/// Usage in a .targets file:
/// <code>
/// &lt;OtelEventsGenerate SchemaFiles="@(OtelEventsSchema)" OutputDirectory="$(IntermediateOutputPath)OtelEventsGenerated" /&gt;
/// </code>
/// </remarks>
public sealed class OtelEventsGenerateTask : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// The .otel.yaml schema files to process.
    /// </summary>
    [Required]
    public ITaskItem[] SchemaFiles { get; set; } = [];

    /// <summary>
    /// The directory where generated .g.cs files will be written.
    /// </summary>
    [Required]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Output parameter: the list of generated .g.cs file paths.
    /// MSBuild targets can use this to add files to the Compile item group.
    /// </summary>
    [Output]
    public ITaskItem[] GeneratedFiles { get; set; } = [];

    /// <inheritdoc />
    public override bool Execute()
    {
        if (SchemaFiles.Length == 0)
        {
            Log.LogMessage(MessageImportance.Low, "OtelEvents: No schema files found, skipping code generation.");
            return true;
        }

        var runner = new SchemaCodeGenRunner();
        var allGeneratedFiles = new List<ITaskItem>();
        var hasErrors = false;

        foreach (var schemaItem in SchemaFiles)
        {
            var schemaPath = schemaItem.GetMetadata("FullPath");
            if (string.IsNullOrEmpty(schemaPath))
            {
                schemaPath = schemaItem.ItemSpec;
            }

            Log.LogMessage(MessageImportance.Normal, "OtelEvents: Generating code from '{0}'", schemaPath);

            var result = runner.Generate(schemaPath, OutputDirectory);

            if (!result.IsSuccess)
            {
                foreach (var error in result.Errors)
                {
                    Log.LogError(
                        subcategory: "OtelEvents",
                        errorCode: null,
                        helpKeyword: null,
                        file: schemaPath,
                        lineNumber: 0,
                        columnNumber: 0,
                        endLineNumber: 0,
                        endColumnNumber: 0,
                        message: error);
                }

                hasErrors = true;
                continue;
            }

            foreach (var generatedPath in result.GeneratedFiles)
            {
                allGeneratedFiles.Add(new TaskItem(generatedPath));
                Log.LogMessage(MessageImportance.Low, "OtelEvents: Generated '{0}'", generatedPath);
            }
        }

        GeneratedFiles = [.. allGeneratedFiles];

        if (!hasErrors)
        {
            Log.LogMessage(MessageImportance.Normal,
                "OtelEvents: Generated {0} file(s) from {1} schema(s).",
                allGeneratedFiles.Count, SchemaFiles.Length);
        }

        return !hasErrors;
    }
}
