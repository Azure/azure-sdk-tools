
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

    public override Task<ListFilesOutput> Invoke(ListFilesInput input, CancellationToken ct)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (!ToolHelpers.TryGetSafeFullPath(this.baseDirectory, input.Path, out var path))
        {
            throw new ArgumentException("The provided path is invalid or outside the allowed base directory.");
        }

        if (!Path.Exists(path))
        {
            throw new ArgumentException($"Path {input.Path} does not exist", nameof(input.Path));
        }

        if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory))
        {
            throw new ArgumentException($"Path {input.Path} is not a directory", nameof(input.Path));
        }

        var filter = string.IsNullOrWhiteSpace(input.Filter) ? "*" : input.Filter;
        var searchOption = input.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var result = Directory.GetFileSystemEntries(path, filter, searchOption);

        return Task.FromResult(new ListFilesOutput(result.Select(p => Path.GetRelativePath(baseDirectory, p)).ToList()));
    }
}
