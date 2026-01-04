// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using GitHubClient;
using static DataFileUtils;

/// <summary>
/// Provides methods for downloading GitHub issues and pull requests data to TSV files.
/// </summary>
public static class GitHubDataDownloader
{
    /// <summary>
    /// Downloads issues data from GitHub repositories and writes to a TSV file.
    /// </summary>
    /// <param name="githubToken">The GitHub token for API authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repos">The list of repository names to download from.</param>
    /// <param name="outputPath">The path to write the TSV file.</param>
    /// <param name="issuesLimit">Optional limit on number of issues to download.</param>
    /// <param name="pageSize">Optional page size for API requests.</param>
    /// <param name="pageLimit">Optional limit on number of pages to retrieve.</param>
    /// <param name="retries">Retry delays in seconds.</param>
    /// <param name="excludedAuthors">Authors to exclude from results.</param>
    /// <param name="verbose">Whether to output verbose logging.</param>
    /// <param name="requireBothLabels">If true, only include items with both category and service labels.</param>
    /// <returns>The number of issues downloaded.</returns>
    public static async Task<int> DownloadIssuesAsync(
        string? githubToken,
        string org,
        IEnumerable<string> repos,
        string outputPath,
        int? issuesLimit = null,
        int? pageSize = null,
        int? pageLimit = null,
        int[]? retries = null,
        string[]? excludedAuthors = null,
        bool verbose = false,
        bool requireBothLabels = false)
    {
        Console.WriteLine($"Downloading issues data to: {outputPath}");
        EnsureOutputDirectory(outputPath);

        int totalCount = 0;
        byte perFlushCount = 0;

        using var writer = new StreamWriter(outputPath);
        await writer.WriteLineAsync(FormatIssueRecord("CategoryLabel", "ServiceLabel", "Title", "Body"));

        foreach (var repo in repos)
        {
            Console.WriteLine($"Downloading issues from {org}/{repo}...");

            await foreach (var result in GitHubApi.DownloadIssues(
                githubToken,
                org,
                repo,
                issuesLimit,
                pageSize,
                pageLimit,
                retries ?? [30, 30, 300, 300, 3000, 3000],
                excludedAuthors,
                verbose))
            {
                // Skip if we require both labels and one is missing
                if (requireBothLabels && (result.CategoryLabel is null || result.ServiceLabel is null))
                {
                    continue;
                }

                await writer.WriteLineAsync(FormatIssueRecord(
                    result.CategoryLabel ?? string.Empty,
                    result.ServiceLabel ?? string.Empty,
                    result.Issue.Title,
                    result.Issue.Body));

                totalCount++;

                if (++perFlushCount == 100)
                {
                    await writer.FlushAsync();
                    perFlushCount = 0;
                }
            }
        }

        Console.WriteLine($"Downloaded {totalCount} issues{(requireBothLabels ? " with both category and service labels" : "")}.");
        return totalCount;
    }

    /// <summary>
    /// Downloads pull requests data from GitHub repositories and writes to a TSV file.
    /// </summary>
    /// <param name="githubToken">The GitHub token for API authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repos">The list of repository names to download from.</param>
    /// <param name="outputPath">The path to write the TSV file.</param>
    /// <param name="pullsLimit">Optional limit on number of pull requests to download.</param>
    /// <param name="pageSize">Optional page size for API requests.</param>
    /// <param name="pageLimit">Optional limit on number of pages to retrieve.</param>
    /// <param name="retries">Retry delays in seconds.</param>
    /// <param name="excludedAuthors">Authors to exclude from results.</param>
    /// <param name="verbose">Whether to output verbose logging.</param>
    /// <param name="requireBothLabels">If true, only include items with both category and service labels.</param>
    /// <returns>The number of pull requests downloaded.</returns>
    public static async Task<int> DownloadPullRequestsAsync(
        string? githubToken,
        string org,
        IEnumerable<string> repos,
        string outputPath,
        int? pullsLimit = null,
        int? pageSize = null,
        int? pageLimit = null,
        int[]? retries = null,
        string[]? excludedAuthors = null,
        bool verbose = false,
        bool requireBothLabels = false)
    {
        Console.WriteLine($"Downloading pull requests data to: {outputPath}");
        EnsureOutputDirectory(outputPath);

        int totalCount = 0;
        byte perFlushCount = 0;

        using var writer = new StreamWriter(outputPath);
        await writer.WriteLineAsync(FormatPullRequestRecord("CategoryLabel", "ServiceLabel", "Title", "Body", ["FileNames"], ["FolderNames"]));

        foreach (var repo in repos)
        {
            Console.WriteLine($"Downloading pull requests from {org}/{repo}...");

            await foreach (var result in GitHubApi.DownloadPullRequests(
                githubToken,
                org,
                repo,
                pullsLimit,
                pageSize,
                pageLimit,
                retries ?? [30, 30, 300, 300, 3000, 3000],
                excludedAuthors,
                verbose))
            {
                // Skip if we require both labels and one is missing
                if (requireBothLabels && (result.CategoryLabel is null || result.ServiceLabel is null))
                {
                    continue;
                }

                await writer.WriteLineAsync(FormatPullRequestRecord(
                    result.CategoryLabel ?? string.Empty,
                    result.ServiceLabel ?? string.Empty,
                    result.PullRequest.Title,
                    result.PullRequest.Body,
                    result.PullRequest.FileNames,
                    result.PullRequest.FolderNames));

                totalCount++;

                if (++perFlushCount == 100)
                {
                    await writer.FlushAsync();
                    perFlushCount = 0;
                }
            }
        }

        Console.WriteLine($"Downloaded {totalCount} pull requests{(requireBothLabels ? " with both category and service labels" : "")}.");
        return totalCount;
    }
}
