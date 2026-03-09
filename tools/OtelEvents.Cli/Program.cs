using System.CommandLine;
using OtelEvents.Cli.Commands;

namespace OtelEvents.Cli;

/// <summary>
/// Entry point for the otel-events Schema CLI tool.
/// Provides validate, generate, and diff commands for .all.yaml schema files.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point — parses arguments and dispatches to the appropriate command.
    /// </summary>
    public static int Main(string[] args)
    {
        var rootCommand = BuildRootCommand();
        return rootCommand.Parse(args).Invoke();
    }

    /// <summary>
    /// Builds the root command with all subcommands. Exposed for integration testing.
    /// </summary>
    internal static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("otel-events Schema CLI — validate, generate, and diff .all.yaml schema files");

        rootCommand.Subcommands.Add(ValidateCommand.Create());
        rootCommand.Subcommands.Add(GenerateCommand.Create());
        rootCommand.Subcommands.Add(DiffCommand.Create());

        return rootCommand;
    }
}
