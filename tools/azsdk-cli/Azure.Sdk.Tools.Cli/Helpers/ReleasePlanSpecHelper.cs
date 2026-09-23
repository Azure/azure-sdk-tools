// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers;

internal static partial class ReleasePlanSpecHelper
{
    public static bool IsValidCommitSha(string? commitSha) =>
        commitSha is { Length: 40 } && commitSha.All(Uri.IsHexDigit);

    public static (string Repository, int Number) ParsePullRequest(string pullRequestUrl)
    {
        var match = SpecPullRequestRegex().Match(pullRequestUrl ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            throw new ArgumentException("Link a valid Azure/azure-rest-api-specs pull request to the release plan before generating SDKs.");
        }

        return (match.Groups["repository"].Value.ToLowerInvariant(), number);
    }

    public static async Task<string> GetMergedCommitShaAsync(IGitHubService githubService, string pullRequestUrl, CancellationToken ct)
    {
        var pullRequest = await GetPullRequestAsync(githubService, pullRequestUrl, ct);
        return pullRequest.Merged ? pullRequest.MergeCommitSha : string.Empty;
    }

    public static async Task<Octokit.PullRequest> GetPullRequestAsync(IGitHubService githubService, string pullRequestUrl, CancellationToken ct)
    {
        var (repository, number) = ParsePullRequest(pullRequestUrl);
        var pullRequest = await githubService.GetPullRequestAsync("Azure", repository, number, ct).WaitAsync(ct);
        if (pullRequest == null)
        {
            throw new InvalidOperationException($"Spec pull request '{pullRequestUrl}' could not be found. No spec commit was pinned.");
        }

        // GitHub also returns a synthetic merge_commit_sha for an open PR. Never pin that value.
        if (!pullRequest.Merged)
        {
            return pullRequest;
        }

        if (!IsValidCommitSha(pullRequest.MergeCommitSha))
        {
            throw new InvalidOperationException($"Merged spec pull request '{pullRequestUrl}' does not have a valid merge commit SHA.");
        }

        return pullRequest;
    }

    [GeneratedRegex(@"^https://github\.com/Azure/(?<repository>azure-rest-api-specs(?:-pr)?)/pull/(?<number>\d+)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex SpecPullRequestRegex();
}
