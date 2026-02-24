using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

public record ReadFileInput([property: Description("Path of the file to read, relative to the package directory (e.g. src/main/java/com/azure/.../File.java) or an absolute path")] string FilePath);
public record ReadFileOutput([property: Description("The content of the file with line numbers")] string FileContent);

public class ReadFileTool(string baseDir, ILogger logger = null) : AgentTool<ReadFileInput, ReadFileOutput>
{
    public override string Name { get; init; } = "ReadFile";
    public override string Description { get; init; } = "Read the contents of a file with line numbers prefixed to each line";

    public override async Task<ReadFileOutput> Invoke(ReadFileInput input, CancellationToken ct)
    {
        // Ensure the input is not null or empty
        if (string.IsNullOrEmpty(input.FilePath))
        {
            throw new ArgumentException("Input path cannot be null or empty.", nameof(input));
        }

        if (!ToolHelpers.TryGetSafeFullPath(baseDir, input.FilePath, out var path))
        {
            logger?.LogWarning("ReadFile rejected path '{InputPath}' (base: '{BaseDir}')", input.FilePath, baseDir);
            throw new ArgumentException("The provided path is invalid or outside the allowed base directory.");
        }
        if (!File.Exists(path))
        {
            throw new ArgumentException($"{path} does not exist");
        }

        // Read the file and prefix each line with its 1-based line number
        var lines = await File.ReadAllLinesAsync(path, ct);
        var numbered = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            numbered.Append(i + 1).Append(": ").AppendLine(lines[i]);
        }
        return new ReadFileOutput(numbered.ToString());
    }
}
