// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Actions.Core.Services;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Octokit;
using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace GitHubClient;

public class GitHubApi
{
    private static ConcurrentDictionary<string, GraphQLHttpClient> _graphQLClients = new();
    private static ConcurrentDictionary<string, HttpClient> _restClients = new();
    private const int MaxLabelDelaySeconds = 30;

    /// <summary>
    /// Gets or creates a GraphQL client for the GitHub API using the provided token.
    /// </summary>
    /// <remarks>The timeout is set to 2 minutes and the client is cached for reuse.</remarks>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <returns>A GraphQLHttpClient instance configured with the provided token and necessary headers.</returns>
    private static GraphQLHttpClient GetGraphQLClient(string githubToken) =>
        _graphQLClients.GetOrAdd(githubToken, token =>
        {
            GraphQLHttpClient client = new("https://api.github.com/graphql", new SystemTextJsonSerializer());
            client.HttpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    scheme: "bearer",
                    parameter: token);

            client.HttpClient.Timeout = TimeSpan.FromMinutes(2);

            return client;
        });

    /// <summary>
    /// Gets or creates a REST client for the GitHub API using the provided token.
    /// </summary>
    /// <remarks>The client is cached for reuse.</remarks>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <returns>An HttpClient instance configured with the provided token and necessary headers.</returns>
    private static HttpClient GetRestClient(string githubToken) =>
        _restClients.GetOrAdd(githubToken, token =>
        {
            HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                scheme: "bearer",
                parameter: token);
            client.DefaultRequestHeaders.Accept.Add(new("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            client.DefaultRequestHeaders.Add("User-Agent", "Issue-Labeler");

            return client;
        });

    /// <summary>
    /// Downloads issues from a GitHub repository, filtering them by label and other criteria.
    /// </summary>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="labelPredicate">A predicate function to filter labels.</param>
    /// <param name="issuesLimit">The maximum number of issues to download.</param>
    /// <param name="pageSize">The number of items per page in GitHub API requests.</param>
    /// <param name="pageLimit">The maximum number of pages to retrieve.</param>
    /// <param name="retries">An array of retry delays in seconds.</param>
    /// <param name="excludedAuthors">An array of authors to exclude from the results.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <param name="verbose">Emit verbose output into the action log.</param>
    /// <returns>The downloaded issues as an async enumerable collection of tuples containing the issue and its predicate-matched label (when only one matcing label is found).</returns>
    public static async IAsyncEnumerable<(Issue Issue, string CategoryLabel, string ServiceLabel)> DownloadIssues(
        string githubToken,
        string org, string repo,
        int? issuesLimit,
        int? pageSize,
        int? pageLimit,
        int[] retries,
        string[]? excludedAuthors,
        ICoreService action,
        bool verbose = false)
    {
        await foreach (var item in DownloadItems<Issue>("issues", githubToken, org, repo, issuesLimit, pageSize ?? 100, pageLimit ?? 1000, retries, excludedAuthors, action, verbose))
        {
            yield return (item.Item, item.CategoryLabel, item.ServiceLabel);
        }
    }

    /// <summary>
    /// Downloads pull requests from a GitHub repository, filtering them by label and other criteria.
    /// </summary>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="labelPredicate">A predicate function to filter labels.</param>
    /// <param name="pullsLimit">The maximum number of pull requests to download.</param>
    /// <param name="pageSize">The number of items per page in GitHub API requests.</param>
    /// <param name="pageLimit">The maximum number of pages to retrieve.</param>
    /// <param name="retries">An array of retry delays in seconds.</param>
    /// <param name="excludedAuthors">An array of authors to exclude from the results.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <param name="verbose">Emit verbose output into the action log.</param>
    /// <returns>The downloaded pull requests as an async enumerable collection of tuples containing the pull request and its predicate-matched label (when only one matching label is found).</returns>
    public static async IAsyncEnumerable<(PullRequest PullRequest, string CategoryLabel, string ServiceLabel)> DownloadPullRequests(
        string githubToken,
        string org,
        string repo,
        int? pullsLimit,
        int? pageSize,
        int? pageLimit,
        int[] retries,
        string[]? excludedAuthors,
        ICoreService action,
        bool verbose = false)
    {
        var items = DownloadItems<PullRequest>("pullRequests", githubToken, org, repo, pullsLimit, pageSize ?? 25, pageLimit ?? 4000, retries, excludedAuthors, action, verbose);

        await foreach (var item in items)
        {
            yield return (item.Item, item.CategoryLabel, item.ServiceLabel);
        }
    }

    /// <summary>
    /// Downloads items from a GitHub repository, filtering them by label and other criteria.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="itemQueryName">The GraphQL query name for the item type (e.g., "issues" or "pullRequests").</param>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="labelPredicate">A predicate function to filter labels.</param>
    /// <param name="itemLimit">The maximum number of issues to download.</param>
    /// <param name="pageSize">The number of items per page in GitHub API requests.</param>
    /// <param name="pageLimit">The maximum number of pages to retrieve.</param>
    /// <param name="retries">An array of retry delays in seconds.</param>
    /// <param name="excludedAuthors">An array of authors to exclude from the results.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <param name="verbose">Emit verbose output into the action log.</param>
    /// <returns>The downloaded items as an async enumerable collection of tuples containing the item and its predicate-matched label (when only one matching label is found).</returns>
    /// <exception cref="ApplicationException"></exception>
    private static async IAsyncEnumerable<(T Item, string CategoryLabel, string ServiceLabel)> DownloadItems<T>(
        string itemQueryName,
        string githubToken,
        string org,
        string repo,
        int? itemLimit,
        int pageSize,
        int pageLimit,
        int[] retries,
        string[]? excludedAuthors,
        ICoreService action,
        bool verbose) where T : Issue
    {
        pageSize = Math.Min(pageSize, 100);

        string typeNames = typeof(T) == typeof(PullRequest) ? "Pull Requests" : "Issues";
        string typeName = typeof(T) == typeof(PullRequest) ? "Pull Request" : "Issue";

        int pageNumber = 0;
        string? after = null;
        bool hasNextPage = true;
        int loadedCount = 0;
        int includedCount = 0;
        int? totalCount = null;
        byte retry = 0;
        bool finished = false;

        do
        {
            action.WriteInfo($"Downloading {typeNames} page {pageNumber + 1} from {org}/{repo}...{(retry > 0 ? $" (retry {retry} of {retries.Length}) " : "")}{(after is not null ? $" (cursor: '{after}')" : "")}");

            Page<T> page;

            try
            {
                page = await GetItemsPage<T>(githubToken, org, repo, pageSize, after, itemQueryName);
            }
            catch (Exception ex) when (
                ex is HttpIOException ||
                ex is HttpRequestException ||
                ex is GraphQLHttpRequestException ||
                ex is TaskCanceledException
            )
            {
                action.WriteInfo($"Exception caught during query.\n  {ex.Message}");

                if (retry >= retries.Length - 1)
                {
                    await action.WriteStatusAsync($"Retry limit of {retries.Length} reached. Aborting.");

                    throw new ApplicationException($"""
                        Retry limit of {retries.Length} reached. Aborting.

                        {ex.Message}

                        Total Downloaded: {totalCount}
                        Applicable for Training: {loadedCount}
                        Page Number: {pageNumber}
                        """
                    );
                }
                else
                {
                    await action.WriteStatusAsync($"Waiting {retries[retry]} seconds before retry {retry + 1} of {retries.Length}...");
                    await Task.Delay(retries[retry] * 1000);
                    retry++;

                    continue;
                }
            }

            if (after == page.EndCursor)
            {
                action.WriteError($"Paging did not progress. Cursor: '{after}'. Aborting.");
                break;
            }

            pageNumber++;
            after = page.EndCursor;
            hasNextPage = page.HasNextPage;
            loadedCount += page.Nodes.Length;
            totalCount ??= page.TotalCount;
            retry = 0;


            foreach (T item in page.Nodes)
            {
                if (excludedAuthors is not null && item.Author?.Login is not null && excludedAuthors.Contains(item.Author.Login, StringComparer.InvariantCultureIgnoreCase))
                {
                    if (verbose) action.WriteInfo($"{typeName} {org}/{repo}#{item.Number} - Excluded from output. Author '{item.Author.Login}' is in excluded list.");
                    continue;
                }

                // If there are more labels, there might be other applicable
                // labels that were not loaded and the model is incomplete.
                if (item.Labels.HasNextPage)
                {
                    if (verbose) action.WriteInfo($"{typeName} {org}/{repo}#{item.Number} - Excluded from output. Not all labels were loaded.");
                    continue;
                }

                if (!item.LabelNames.Contains("issue-addressed") || !item.LabelNames.Contains("customer-reported"))
                {
                    if (verbose) action.WriteInfo($"{typeName} {org}/{repo}#{item.Number} - Excluded from output. Labels do not contain issue-addressed and customer-reported.");
                    continue;
                }
                string[] categoryLabels = item.CategoryLabelNames;
                string[] serviceLabels = item.ServiceLabelNames;

                if (categoryLabels.Length != 1 || serviceLabels.Length != 1)
                {
                    if (verbose) action.WriteInfo($"{typeName} {org}/{repo}#{item.Number} - Excluded from output. {categoryLabels.Length} applicable Category labels found, and {serviceLabels.Length} applicable Service labels found.");
                    continue;
                }

                // Exactly one applicable category and service label were found on the item. Include it in the model.
                if (verbose) action.WriteInfo($"{typeName} {org}/{repo}#{item.Number} - Included in output. Applicable labels: '{categoryLabels[0]}', '{serviceLabels[0]}'.");

                yield return (item, categoryLabels[0], serviceLabels[0]);

                includedCount++;

                if (itemLimit.HasValue && includedCount >= itemLimit)
                {
                    break;
                }
            }

            finished = (!hasNextPage || pageNumber >= pageLimit || (itemLimit.HasValue && includedCount >= itemLimit));

            await action.WriteStatusAsync(
                $"Items to Include: {includedCount} (limit: {(itemLimit.HasValue ? itemLimit : "none")}) | " +
                $"Items Downloaded: {loadedCount} (total: {totalCount}) | " +
                $"Pages Downloaded: {pageNumber} (limit: {pageLimit})");

            if (finished)
            {
                action.Summary.AddPersistent(summary => {
                    summary.AddMarkdownHeading($"Finished Downloading {typeNames} from {org}/{repo}", 2);
                    summary.AddMarkdownList([
                        $"Items to Include: {includedCount} (limit: {(itemLimit.HasValue ? itemLimit : "none")})",
                        $"Items Downloaded: {loadedCount} (total: {totalCount})",
                        $"Pages Downloaded: {pageNumber} (limit: {pageLimit})"
                    ]);
                });
            }
        }
        while (!finished);
    }

    /// <summary>
    /// Retrieves a page of items from a GitHub repository using GraphQL.
    /// </summary>
    /// <typeparam name="T">The type of items to retrieve (e.g., Issue or PullRequest).</typeparam>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="pageSize">The number of items per page in GitHub API requests.</param>
    /// <param name="after">The cursor for pagination (null for the first page).</param>
    /// <param name="itemQueryName">The GraphQL query name for the item type (e.g., "issues" or "pullRequests").</param>
    /// <returns>The page of items retrieved from the GitHub repository.</returns>
    /// <exception cref="ApplicationException">When the GraphQL request returns errors or the response does not include the expected data.</exception>
    private static async Task<Page<T>> GetItemsPage<T>(string githubToken, string org, string repo, int pageSize, string? after, string itemQueryName) where T : Issue
    {
        GraphQLHttpClient client = GetGraphQLClient(githubToken);

        string files = typeof(T) == typeof(PullRequest) ? "files (first: 100) { nodes { path } }" : "";

        GraphQLRequest query = new GraphQLRequest
        {
            Query = $$"""
                query ($owner: String!, $repo: String!, $after: String) {
                    repository (owner: $owner, name: $repo) {
                        result:{{itemQueryName}} (after: $after, first: {{pageSize}}, orderBy: {field: CREATED_AT, direction: DESC}) {
                            nodes {
                                number
                                title
                                author { login }
                                body: bodyText
                                labels (first: 25) {
                                    nodes { 
                                    name
                                    color
                                    }
                                    pageInfo { hasNextPage }
                                }
                                {{files}}
                            }
                            pageInfo {
                                hasNextPage
                                endCursor
                            }
                            totalCount
                        }
                    }
                }
                """,
            Variables = new
            {
                Owner = org,
                Repo = repo,
                After = after
            }
        };

        var response = await client.SendQueryAsync<RepositoryQuery<Page<T>>>(query);

        if (response.Errors?.Any() ?? false)
        {
            string errors = string.Join("\n\n", response.Errors.Select((e, i) => $"{i + 1}. {e.Message}").ToArray());
            throw new ApplicationException($"GraphQL request returned errors.\n\n{errors}");
        }
        else if (response.Data is null || response.Data.Repository is null || response.Data.Repository.Result is null)
        {
            throw new ApplicationException("GraphQL response did not include the repository result data");
        }

        return response.Data.Repository.Result;
    }

    /// <summary>
    /// Gets an issue from a GitHub repository using GraphQL.
    /// </summary>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="number">The issue number.</param>
    /// <param name="retries">An array of retry delays in seconds.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <param name="verbose">Emit verbose output into the action log.</param>
    /// <returns>The issue retrieved from the GitHub repository, or <c>null</c> if not found.</returns>
    public static async Task<Issue?> GetIssue(string githubToken, string org, string repo, ulong number, int[] retries, ICoreService action, bool verbose) =>
        await GetItem<Issue>(githubToken, org, repo, number, retries, verbose, "issue", action);

    /// <summary>
    /// Gets a pull request from a GitHub repository using GraphQL.
    /// </summary>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="retries">An array of retry delays in seconds.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <param name="verbose">Emit verbose output into the action log.</param>
    /// <returns>The pull request retrieved from the GitHub repository, or <c>null</c> if not found.</returns>
    public static async Task<PullRequest?> GetPullRequest(string githubToken, string org, string repo, ulong number, int[] retries, ICoreService action, bool verbose) =>
        await GetItem<PullRequest>(githubToken, org, repo, number, retries, verbose, "pullRequest", action);

    private static async Task<T?> GetItem<T>(string githubToken, string org, string repo, ulong number, int[] retries, bool verbose, string itemQueryName, ICoreService action) where T : Issue
    {
        GraphQLHttpClient client = GetGraphQLClient(githubToken);
        string files = typeof(T) == typeof(PullRequest) ? "files (first: 100) { nodes { path } }" : "";

        GraphQLRequest query = new GraphQLRequest
        {
            Query = $$"""
                query ($owner: String!, $repo: String!, $number: Int!) {
                    repository (owner: $owner, name: $repo) {
                        result:{{itemQueryName}} (number: $number) {
                            number
                            title
                            author { login }
                            body: bodyText
                            labels (first: 25) {
                                nodes { 
                                name
                                color
                                }
                                pageInfo { hasNextPage }
                            }
                            {{files}}
                        }
                    }
                }
                """,
            Variables = new
            {
                Owner = org,
                Repo = repo,
                Number = number
            }
        };

        byte retry = 0;
        string typeName = typeof(T) == typeof(PullRequest) ? "Pull Request" : "Issue";

        while (retry < retries.Length)
        {
            try
            {
                var response = await client.SendQueryAsync<RepositoryQuery<T>>(query);

                if (!(response.Errors?.Any() ?? false) && response.Data?.Repository?.Result is not null)
                {
                    return response.Data.Repository.Result;
                }

                if (response.Errors?.Any() ?? false)
                {
                    // These errors occur when an issue/pull does not exist or when the API rate limit has been exceeded
                    if (response.Errors.Any(e => e.Message.StartsWith("API rate limit exceeded")))
                    {
                        action.WriteInfo($"""
                            [{typeName} {org}/{repo}#{number}] Failed to retrieve data.
                                Rate limit has been reached.
                                {(retry < retries.Length ? $"Will proceed with retry {retry + 1} of {retries.Length} after {retries[retry]} seconds..." : $"Retry limit of {retries.Length} reached.")}
                            """);
                    }
                    else
                    {
                        // Could not detect this as a rate limit issue. Do not retry.
                        string errors = string.Join("\n\n", response.Errors.Select((e, i) => $"{i + 1}. {e.Message}").ToArray());

                        action.WriteInfo($"""
                            [{typeName} {org}/{repo}#{number}] Failed to retrieve data.
                                GraphQL request returned errors:

                                {errors}
                            """);

                        return null;
                    }
                }
                else
                {
                    // Do not retry as these errors are not recoverable
                    // This is usually a bug during development when the query/response model is incorrect
                    action.WriteInfo($"""
                        [{typeName} {org}/{repo}#{number}] Failed to retrieve data.
                            GraphQL response did not include the repository result data.
                        """);

                    return null;
                }
            }
            catch (Exception ex) when (
                ex is HttpIOException ||
                ex is HttpRequestException ||
                ex is GraphQLHttpRequestException ||
                ex is TaskCanceledException
            )
            {
                // Retry on exceptions as they can be temporary network issues
                action.WriteInfo($"""
                    [{typeName} {org}/{repo}#{number}] Failed to retrieve data.
                        Exception caught during query.

                        {ex.Message}

                        {(retry < retries.Length ? $"Will proceed with retry {retry + 1} of {retries.Length} after {retries[retry]} seconds..." : $"Retry limit of {retries.Length} reached.")}
                    """);
            }

            await Task.Delay(retries[retry++] * 1000);
        }

        return null;
    }

    /// <summary>
    /// Adds a label to an issue or pull request in a GitHub repository.
    /// </summary>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="type">The type of item (e.g., "issue" or "pull request").</param>
    /// <param name="number">The issue or pull request number.</param>
    /// <param name="label">The label to add.</param>
    /// <param name="retries">An array of retry delays in seconds. A maximum delay of 30 seconds is enforced.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <returns>A string describing a failure, or <c>null</c> if successful.</returns>
    public static async Task<string?> AddLabels(string githubToken, string org, string repo, string type, ulong number, string[] labels, int[] retries, ICoreService action)
    {
        var client = GetRestClient(githubToken);
        byte retry = 0;

        while (retry < retries.Length)
        {
            var response = await client.PostAsJsonAsync(
                $"https://api.github.com/repos/{org}/{repo}/issues/{number}/labels",
                labels,
                CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            action.WriteInfo($"""
                [{type} {org}/{repo}#{number}] Failed to add labels '{labels}'. {response.ReasonPhrase} ({response.StatusCode})
                    {(retry < retries.Length ? $"Will proceed with retry {retry + 1} of {retries.Length} after {retries[retry]} seconds..." : $"Retry limit of {retries.Length} reached.")}
                """);

            int delay = Math.Min(retries[retry++], MaxLabelDelaySeconds);
            await Task.Delay(delay * 1000);
        }

        return $"Failed to add labels '{labels}' after {retries.Length} retries.";
    }

    /// <summary>
    /// Removes a label from an issue or pull request in a GitHub repository.
    /// </summary>
    /// <param name="githubToken">The GitHub token to use for authentication.</param>
    /// <param name="org">The GitHub organization name.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="type">The type of item (e.g., "issue" or "pull request").</param>
    /// <param name="number">The issue or pull request number.</param>
    /// <param name="label">The label to add.</param>
    /// <param name="retries">An array of retry delays in seconds. A maximum delay of 30 seconds is enforced.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <returns>A string describing a failure, or <c>null</c> if successful.</returns>
    public static async Task<string?> RemoveLabel(string githubToken, string org, string repo, string type, ulong number, string label, int[] retries, ICoreService action)
    {
        var client = GetRestClient(githubToken);
        byte retry = 0;

        while (retry < retries.Length)
        {
            var response = await client.DeleteAsync(
                $"https://api.github.com/repos/{org}/{repo}/issues/{number}/labels/{label}",
                CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            action.WriteInfo($"""
                [{type} {org}/{repo}#{number}] Failed to remove label '{label}'. {response.ReasonPhrase} ({response.StatusCode})
                    {(retry < retries.Length ? $"Will proceed with retry {retry + 1} of {retries.Length} after {retries[retry]} seconds..." : $"Retry limit of {retries.Length} reached.")}
                """);

            int delay = Math.Min(retries[retry++], MaxLabelDelaySeconds);
            await Task.Delay(delay * 1000);
        }

        return $"Failed to remove label '{label}' after {retries.Length} retries.";
    }
}
