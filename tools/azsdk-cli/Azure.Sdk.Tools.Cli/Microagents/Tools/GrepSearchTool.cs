using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

public record GrepSearchInput(
    [property: Description("The search pattern (can be text or regex depending on IsRegex parameter)")] string Pattern,
    [property: Description("The relative file path or directory to search in")] string Path,
    [property: Description("Whether the pattern is a regular expression (default: false)")] bool IsRegex = false,
    [property: Description("Maximum number of results to return (default: 50)")] int MaxResults = 50
);

public record GrepSearchOutput(
    [property: Description("List of matching results with file path, line number, and content")] IList<GrepMatch> Matches,
    [property: Description("Total number of matches found (may be more than returned)")] int TotalMatches
);

public record GrepMatch(
    [property: Description("Relative file path")] string FilePath,
    [property: Description("Line number where match was found")] int LineNumber,
    [property: Description("Content of the matching line")] string Content
);

public class GrepSearchTool(string baseDirectory) : AgentTool<GrepSearchInput, GrepSearchOutput>
{
    public override string Name { get; init; } = "GrepSearch";
    public override string Description { get; init; } = "Search for patterns in files within the project";

    public override Task<GrepSearchOutput> Invoke(GrepSearchInput input, CancellationToken ct)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Pattern))
        {
            throw new ArgumentException("Search pattern cannot be empty", nameof(input.Pattern));
        }

        if (!ToolHelpers.TryGetSafeFullPath(baseDirectory, input.Path, out var searchPath))
        {
            throw new ArgumentException("The provided path is invalid or outside the allowed base directory.");
        }

        if (!Path.Exists(searchPath))
        {
            throw new ArgumentException($"Path {input.Path} does not exist", nameof(input.Path));
        }

        var matches = new List<GrepMatch>();
        var files = new List<string>();

        // Determine if searching a single file or directory
        if (File.Exists(searchPath))
        {
            files.Add(searchPath);
        }
        else if (Directory.Exists(searchPath))
        {
            files.AddRange(Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories));
        }

        // Compile regex if needed
        Regex? regex = null;
        if (input.IsRegex)
        {
            try
            {
                regex = new Regex(input.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regular expression: {ex.Message}", nameof(input.Pattern));
            }
        }

        // Search through files
        foreach (var file in files)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    bool isMatch = input.IsRegex
                        ? regex!.IsMatch(lines[i])
                        : lines[i].Contains(input.Pattern, StringComparison.OrdinalIgnoreCase);

                    if (isMatch)
                    {
                        matches.Add(new GrepMatch(
                            FilePath: Path.GetRelativePath(baseDirectory, file),
                            LineNumber: i + 1,
                            Content: lines[i].Trim()
                        ));

                        if (matches.Count >= input.MaxResults)
                        {
                            return Task.FromResult(new GrepSearchOutput(matches, matches.Count));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Skip files that can't be read (binary, permission issues, etc.)
                continue;
            }
        }

        return Task.FromResult(new GrepSearchOutput(matches, matches.Count));
    }
}
