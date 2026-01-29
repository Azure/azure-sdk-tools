namespace Sdk.Tools.Cli.Models;

/// <summary>
/// Represents a source file loaded for context.
/// </summary>
public record SourceInput(
    string FilePath,
    string Content,
    int Priority = 10
);
