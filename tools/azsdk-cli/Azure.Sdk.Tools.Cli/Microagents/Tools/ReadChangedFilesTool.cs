using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

// Forward declaration to use types from LanguageChecks.cs
public record ChangelogContents(string Contents);
public record GitChangedFilesResult(IEnumerable<string> ChangedFiles, IEnumerable<string> StagedFiles, IEnumerable<string> UntrackedFiles);

public class ReadChangedFilesTool(string baseDir, IProcessHelper processHelper, IGitHelper gitHelper) : AgentTool<ChangelogContents, GitChangedFilesResult>
{
    public override string Name { get; init; } = "ReadChangedFiles";
    public override string Description { get; init; } = "Read the list of changed, staged, and untracked files in the git repository";

    public override async Task<GitChangedFilesResult> Invoke(ChangelogContents input, CancellationToken ct)
    {
        var repoRoot = gitHelper.DiscoverRepoRoot(baseDir);
        if (string.IsNullOrEmpty(repoRoot))
        {
            throw new InvalidOperationException($"No git repository found at or above the path: {baseDir}");
        }

        // Get unstaged changes (modified files)
        var diffOptions = new ProcessOptions("git", ["diff", "--name-only"], workingDirectory: repoRoot);
        var diffResult = await processHelper.Run(diffOptions, ct);
        var changedFiles = string.IsNullOrWhiteSpace(diffResult.Output) 
            ? new List<string>() 
            : diffResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Get staged changes
        var cachedOptions = new ProcessOptions("git", ["diff", "--cached", "--name-only"], workingDirectory: repoRoot);
        var cachedResult = await processHelper.Run(cachedOptions, ct);
        var stagedFiles = string.IsNullOrWhiteSpace(cachedResult.Output) 
            ? new List<string>() 
            : cachedResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Get untracked files
        var untrackedOptions = new ProcessOptions("git", ["ls-files", "--others", "--exclude-standard"], workingDirectory: repoRoot);
        var untrackedResult = await processHelper.Run(untrackedOptions, ct);
        var untrackedFiles = string.IsNullOrWhiteSpace(untrackedResult.Output) 
            ? new List<string>() 
            : untrackedResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

        return new GitChangedFilesResult(changedFiles, stagedFiles, untrackedFiles);
    }
}
