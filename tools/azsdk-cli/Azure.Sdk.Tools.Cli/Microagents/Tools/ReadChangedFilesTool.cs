using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

// Forward declaration to use types from LanguageChecks.cs
public record ChangelogContents(string Contents);
public record GitCommitInfo(string Hash, string Author, string Date, string Subject, string Body);
public record GitChangedFilesResult(IEnumerable<string> ChangedFiles, IEnumerable<string> StagedFiles, IEnumerable<string> UntrackedFiles, IEnumerable<GitCommitInfo> RecentCommits);

public class ReadChangedFilesTool(string baseDir, IProcessHelper processHelper, IGitHelper gitHelper) : AgentTool<ChangelogContents, GitChangedFilesResult>
{
    public override string Name { get; init; } = "ReadChangedFiles";
    public override string Description { get; init; } = "Read the list of changed, staged, and untracked files in the git repository, along with recent commit information";

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

        // Get recent commits (last 10 commits with detailed format)
        var logOptions = new ProcessOptions("git", [
            "log", 
            "--oneline", 
            "-10",
            "--pretty=format:%H|%an|%ad|%s|%b",
            "--date=iso"
        ], workingDirectory: repoRoot);
        var logResult = await processHelper.Run(logOptions, ct);
        
        var recentCommits = new List<GitCommitInfo>();
        if (!string.IsNullOrWhiteSpace(logResult.Output))
        {
            var commitLines = logResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in commitLines)
            {
                var parts = line.Split('|', 5); // Split into max 5 parts
                if (parts.Length >= 4)
                {
                    var hash = parts[0];
                    var author = parts[1];
                    var date = parts[2];
                    var subject = parts[3];
                    var body = parts.Length > 4 ? parts[4] : "";
                    
                    recentCommits.Add(new GitCommitInfo(hash, author, date, subject, body));
                }
            }
        }

        return new GitChangedFilesResult(changedFiles, stagedFiles, untrackedFiles, recentCommits);
    }
}
