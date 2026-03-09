using System.Reflection;
using OtelEvents.Schema.Parsing;

namespace OtelEvents.Schema;

/// <summary>
/// Provides access to built-in schema definitions embedded in the OtelEvents.Schema package.
/// Consumers can use these schemas directly or import them into their own schemas.
/// </summary>
public static class BuiltInSchemas
{
    private static readonly Assembly s_assembly = typeof(BuiltInSchemas).Assembly;

    /// <summary>
    /// The embedded resource name for the lifecycle schema.
    /// </summary>
    internal const string LifecycleResourceName = "OtelEvents.Schema.Schemas.lifecycle.all.yaml";

    /// <summary>
    /// Loads and parses the built-in lifecycle schema (application health and lifecycle events).
    /// </summary>
    /// <returns>A parse result containing the lifecycle schema document.</returns>
    public static ParseResult LoadLifecycleSchema()
    {
        return LoadEmbeddedSchema(LifecycleResourceName);
    }

    /// <summary>
    /// Returns the raw YAML content of the built-in lifecycle schema.
    /// Useful for consumers who want to import or merge it with their own schemas.
    /// </summary>
    /// <returns>The lifecycle schema YAML content as a string.</returns>
    public static string GetLifecycleSchemaYaml()
    {
        return ReadEmbeddedResource(LifecycleResourceName);
    }

    private static ParseResult LoadEmbeddedSchema(string resourceName)
    {
        var yaml = ReadEmbeddedResource(resourceName);
        var parser = new SchemaParser();
        return parser.Parse(yaml, yaml.Length);
    }

    private static string ReadEmbeddedResource(string resourceName)
    {
        using var stream = s_assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Built-in schema resource '{resourceName}' not found in assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
