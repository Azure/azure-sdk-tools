// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.ApiReview;

namespace Azure.Sdk.Tools.Cli.Services.ApiReview;

public interface IApiReviewGitService
{
    Task<string> GetDefaultBranchAsync(string repoRoot, CancellationToken ct);
    Task FetchTargetAsync(string repoRoot, string repoName, ApiReviewTarget target, CancellationToken ct);
    Task AddWorktreeAsync(string repoRoot, string worktreePath, string gitRef, CancellationToken ct);
    Task RemoveWorktreeAsync(string repoRoot, string worktreePath, CancellationToken ct);
    Task MaterializeReviewBranchAsync(string repoRoot, string branchName, string startPoint, IEnumerable<ApiReviewArtifact> artifacts, string commitMessage, CancellationToken ct);
    Task PushBranchAsync(string repoRoot, string branchName, CancellationToken ct);
}

public class ApiReviewGitService(IGitCommandHelper gitCommandHelper, ILogger<ApiReviewGitService> logger) : IApiReviewGitService
{
    public async Task<string> GetDefaultBranchAsync(string repoRoot, CancellationToken ct)
    {
        var result = await RunGitAsync(repoRoot, ["symbolic-ref", "refs/remotes/origin/HEAD", "--short"], ct, throwOnFailure: false);
        if (result.ExitCode == 0)
        {
            var value = result.Stdout.Trim();
            const string originPrefix = "origin/";
            if (value.StartsWith(originPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return value[originPrefix.Length..];
            }
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "main";
    }

    public async Task FetchTargetAsync(string repoRoot, string repoName, ApiReviewTarget target, CancellationToken ct)
    {
        switch (target.Kind)
        {
            case ApiReviewTargetKind.RemoteBranch:
                await RunGitAsync(repoRoot, ["fetch", target.Remote!, target.Branch!], ct);
                break;
            case ApiReviewTargetKind.ForkBranch:
                var remoteUrl = $"https://github.com/{target.Owner}/{repoName}.git";
                await RunGitAsync(repoRoot, ["fetch", remoteUrl, $"{target.Branch}:refs/remotes/{target.Owner}/{target.Branch}"], ct);
                break;
            default:
                await RunGitAsync(repoRoot, ["fetch", "--tags"], ct);
                break;
        }
    }

    public async Task AddWorktreeAsync(string repoRoot, string worktreePath, string gitRef, CancellationToken ct)
    {
        await RunGitAsync(repoRoot, ["worktree", "add", "--detach", worktreePath, gitRef], ct);
    }

    public async Task RemoveWorktreeAsync(string repoRoot, string worktreePath, CancellationToken ct)
    {
        if (Directory.Exists(worktreePath))
        {
            await RunGitAsync(repoRoot, ["worktree", "remove", "--force", worktreePath], ct, throwOnFailure: false);
        }
    }

    public async Task MaterializeReviewBranchAsync(string repoRoot, string branchName, string startPoint, IEnumerable<ApiReviewArtifact> artifacts, string commitMessage, CancellationToken ct)
    {
        var worktreePath = Path.Combine(Path.GetTempPath(), "azsdk-api-review-branches", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(worktreePath)!);

        try
        {
            await RunGitAsync(repoRoot, ["worktree", "add", "-B", branchName, worktreePath, startPoint], ct);
            foreach (var artifact in artifacts)
            {
                var targetPath = Path.Combine(worktreePath, artifact.ReviewPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(artifact.SourcePath, targetPath, overwrite: true);
            }

            await RunGitAsync(worktreePath, ["add", "--", "."], ct);
            var diffResult = await RunGitAsync(worktreePath, ["diff", "--cached", "--quiet"], ct, throwOnFailure: false);
            if (diffResult.ExitCode != 0)
            {
                await RunGitAsync(worktreePath, ["commit", "-m", commitMessage], ct);
            }
            else
            {
                logger.LogInformation("No artifact changes to commit on {BranchName}", branchName);
            }
        }
        finally
        {
            await RemoveWorktreeAsync(repoRoot, worktreePath, ct);
        }
    }

    public async Task PushBranchAsync(string repoRoot, string branchName, CancellationToken ct)
    {
        await RunGitAsync(repoRoot, ["push", "-u", "origin", branchName, "--force-with-lease"], ct);
    }

    private async Task<ProcessResult> RunGitAsync(string workingDirectory, string[] args, CancellationToken ct, bool throwOnFailure = true)
    {
        var result = await gitCommandHelper.Run(new GitOptions(args, workingDirectory, logOutputStream: true, timeout: TimeSpan.FromMinutes(10)), ct);
        if (throwOnFailure && result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git command failed: git {string.Join(' ', args)}{Environment.NewLine}{result.Output}".Trim());
        }

        return result;
    }
}
