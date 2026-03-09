namespace All.Exporter.Json;

/// <summary>Output target for the ALL JSON exporter.</summary>
public enum AllJsonOutput
{
    /// <summary>Write JSONL to standard output (default — recommended for containers).</summary>
    Stdout,

    /// <summary>Write JSONL to standard error.</summary>
    Stderr,

    /// <summary>Write JSONL to a file (path specified in options).</summary>
    File,
}
