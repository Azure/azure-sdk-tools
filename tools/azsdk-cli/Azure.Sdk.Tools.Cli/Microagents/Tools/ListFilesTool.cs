
using System.ComponentModel;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

public record ListFilesInput(
    [property: Description("The path to list files for, which must be a relative path.")] string Path,
    [property: Description("Whether to list the files recursively")] bool Recursive,
    [property: Description("An optional glob pattern to filter the listed files")] string? Filter
    );

public record ListFilesOutput(IList<string> fileNames);

public class ListFilesTool(string baseDirectory) : AgentTool<ListFilesInput, ListFilesOutput>
{
    public override string Name { get; init; } = "ListFiles";
    public override string Description { get; init; } = "List files in the given directory";

    private readonly string baseDirectory = baseDirectory;

    public override Task<ListFilesOutput> InvokeAsync(ListFilesInput input, CancellationToken ct)
    {
        var path = Path.Join(this.baseDirectory, input.Path);

        if (!Directory.Exists(path))
        {
            throw new Exception($"{path} does not exist");
        }

        Console.WriteLine($"Listing files in: {path}");
        var result = Directory.GetFileSystemEntries(path, input.Filter, input.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        return Task.FromResult(new ListFilesOutput(result.Select(p => Path.GetRelativePath(baseDirectory, p)).ToList()));
    }
}
