// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers;

internal static partial class ReleasePlanSpecHelper
{
    public static bool IsValidCommitSha(string? commitSha) =>
        commitSha is { Length: 40 } && commitSha.All(Uri.IsHexDigit);

    // An opaque, caller-carried precondition over both records, not an approval token.
    public static string GetTargetRevision(int? planId, int? planRevision, int? specId, int? specRevision) =>
        planId is > 0 && planRevision is > 0 && specId is > 0 && specRevision is > 0
            ? FormattableString.Invariant($"{planId}:{planRevision}:{specId}:{specRevision}")
            : string.Empty;

    public static (string Repository, int Number) ParsePullRequest(string pullRequestUrl)
    {
        var match = SpecPullRequestRegex().Match(pullRequestUrl ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            throw new ArgumentException("Provide a valid Azure/azure-rest-api-specs pull request to configure a release target.");
        }

        return (match.Groups["repository"].Value.ToLowerInvariant(), number);
    }

    public static async Task<ReleasePlanSpecTarget> ResolveTargetAsync(
        IGitHubService githubService, ITypeSpecHelper typeSpecHelper, INpxHelper npxHelper, ILogger logger,
        string projectPath, string pullRequestUrl, string apiVersion, string commitSha, string sdkReleaseType, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (repository, number) = ParsePullRequest(pullRequestUrl);
        var pullRequest = await githubService.GetPullRequestAsync("Azure", repository, number, ct).WaitAsync(ct)
            ?? throw new InvalidOperationException($"Spec pull request '{pullRequestUrl}' could not be found. No spec commit was pinned.");
        if (!pullRequest.Merged && pullRequest.State == Octokit.ItemState.Closed)
        {
            throw new InvalidOperationException("The selected spec PR was closed without merging. Select the intended active or merged spec PR.");
        }
        // Open PRs use the exact source commit, never GitHub's moving test-merge ref.
        var resolvedCommit = pullRequest.Merged ? pullRequest.MergeCommitSha : pullRequest.Head?.Sha;
        if (!IsValidCommitSha(resolvedCommit))
        {
            throw new InvalidOperationException("The spec PR has no valid source commit SHA.");
        }
        if (!string.IsNullOrEmpty(commitSha) && !string.Equals(commitSha, resolvedCommit, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Spec PR source changed or the supplied SHA does not match it. Review commit {resolvedCommit} and confirm the intended target again.");
        }
        var project = await typeSpecHelper.ValidateReleasePlanSnapshotAsync(projectPath, resolvedCommit!, npxHelper, logger, ct);
        var availableVersions = project.Packages.Select(p => p.ApiVersion)
            .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (availableVersions.Count == 0 || availableVersions.Any(v =>
            v.Equals("all", StringComparison.OrdinalIgnoreCase) || v.Equals("multiple-versions", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Spec metadata at commit {resolvedCommit} must report concrete API versions. Fix the metadata configuration before confirming a release target.");
        }
        var selectedVersion = apiVersion?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(selectedVersion))
        {
            selectedVersion = availableVersions.Count == 1 ? availableVersions[0] : string.Empty;
        }
        if (!string.IsNullOrEmpty(selectedVersion) && !availableVersions.Contains(selectedVersion, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"API version '{selectedVersion}' is not reported by metadata at commit {resolvedCommit}. Available versions: {string.Join(", ", availableVersions)}.");
        }
        if (string.Equals(sdkReleaseType, "stable", StringComparison.OrdinalIgnoreCase) && selectedVersion.Contains("preview", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A stable SDK release cannot target a preview API version. Confirm a beta release or choose a stable API version.");
        }
        ct.ThrowIfCancellationRequested();
        return new ReleasePlanSpecTarget
        {
            TypeSpecProjectPath = typeSpecHelper.GetTypeSpecProjectRelativePath(projectPath).TrimEnd('/'),
            ApiVersion = selectedVersion,
            SpecCommitSHA = resolvedCommit!,
            SpecPullRequestUrl = pullRequestUrl,
            CommitUrl = $"https://github.com/Azure/{repository}/commit/{resolvedCommit}",
            SDKReleaseType = sdkReleaseType,
            AvailableApiVersions = availableVersions,
            Packages = project.Packages
        };
    }

    public static bool NeedsConfirmation(string resolvedApiVersion, string commitSha, bool confirm) =>
        !confirm || string.IsNullOrWhiteSpace(resolvedApiVersion) || !IsValidCommitSha(commitSha);

    [GeneratedRegex(@"^https://github\.com/Azure/(?<repository>azure-rest-api-specs(?:-pr)?)/pull/(?<number>\d+)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex SpecPullRequestRegex();
}
