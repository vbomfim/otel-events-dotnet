namespace OtelEvents.Cli;

/// <summary>
/// Entry point for the otel-events CLI tool.
/// Usage: otel-events docs &lt;path&gt; [-o &lt;output.md&gt;]
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry point. Dispatches subcommands.
    /// </summary>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0];

        return command switch
        {
            "docs" => HandleDocsCommand(args),
            "--help" or "-h" => PrintUsageAndReturn(),
            _ => PrintUnknownCommand(command)
        };
    }

    private static int HandleDocsCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: Missing schema file path.");
            Console.Error.WriteLine("Usage: otel-events docs <path> [-o <output.md>]");
            return 1;
        }

        var schemaPath = args[1];
        string? outputPath = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] is "-o" or "--output" && i + 1 < args.Length)
            {
                outputPath = args[i + 1];
                i++;
            }
        }

        return DocsCommand.Execute(schemaPath, outputPath);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("otel-events CLI — Schema tools for otel-events");
        Console.WriteLine();
        Console.WriteLine("Usage: otel-events <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  docs <path> [-o <output.md>]   Generate Markdown documentation from a YAML schema");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help                     Show this help message");
    }

    private static int PrintUsageAndReturn()
    {
        PrintUsage();
        return 0;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Error: Unknown command '{command}'");
        Console.Error.WriteLine("Run 'otel-events --help' for usage information.");
        return 1;
    }
}
