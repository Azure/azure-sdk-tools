using System.ComponentModel;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

public record ReadFileInput([property: Description("Relative path of the file to read")] string FilePath);
public record ReadFileOutput([property: Description("The content of the file")] string FileContent);

public class ReadFileTool(string baseDir) : AgentTool<ReadFileInput, ReadFileOutput>
{
    public override string Name { get; init; } = "ReadFile";
    public override string Description { get; init; } = "Read the contents of a file";

    public override async Task<ReadFileOutput> Invoke(ReadFileInput input, CancellationToken ct)
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
        if (!File.Exists(path))
        {
            throw new ArgumentException($"{path} does not exist");
        }

        // Read the file content
        var content = await File.ReadAllTextAsync(path);
        return new ReadFileOutput(content);
    }
}
