using System.ComponentModel;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

public record WriteFileInput(
    [property: Description("Relative path of the file to write")] string FilePath,
    [property: Description("Content to write to the file")] string Content);

public record WriteFileOutput([property: Description("Success message")] string Message);

public class WriteFileTool(string baseDir) : AgentTool<WriteFileInput, WriteFileOutput>
{
    public override string Name { get; init; } = "WriteFile";
    public override string Description { get; init; } = "Write content to a file";

    public override async Task<WriteFileOutput> Invoke(WriteFileInput input, CancellationToken ct)
    {
        // Ensure the input is not null or empty
        if (string.IsNullOrEmpty(input.FilePath))
        {
            throw new ArgumentException("Input path cannot be null or empty.", nameof(input));
        }

        if (!ToolHelpers.TryGetSafeFullPath(baseDir, input.FilePath, out var path))
        {
            throw new ArgumentException("The provided path is invalid or outside the allowed base directory.");
        }

        // Ensure the directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write the file content
        await File.WriteAllTextAsync(path, input.Content, ct);
        return new WriteFileOutput($"Successfully wrote to {input.FilePath}");
    }
}