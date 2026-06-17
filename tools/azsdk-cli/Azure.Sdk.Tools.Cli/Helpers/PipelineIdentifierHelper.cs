// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface IPipelineIdentifierHelper
{
    /// <summary>
    /// Parses a pipeline identifier that is either a numeric build ID or a DevOps pipeline URL.
    /// </summary>
    (int BuildId, string? Project) Parse(string pipelineIdentifier);

    /// <summary>
    /// Discovers the DevOps project for a build by trying the public project first, then the internal project.
    /// </summary>
    Task<string> GetPipelineProjectAsync(int buildId, string? project = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the DevOps pipeline URL for a given project and build ID.
    /// </summary>
    string GetPipelineUrl(string project, int buildId);

    /// <summary>
    /// Tries to parse a GitHub PR URL into its components. Returns null if not a valid PR link.
    /// </summary>
    GitHubPrLink? TryParseGitHubPrLink(string identifier);

    /// <summary>
    /// Resolves a GitHub PR identifier (URL or bare PR number) to its components.
    /// For bare PR numbers, uses IGitHelper to detect the current repo from the working directory.
    /// </summary>
    Task<GitHubPrLink?> TryResolveGitHubPrAsync(string identifier, CancellationToken ct);

    /// <summary>
    /// Queries GitHub's GraphQL API for check runs on a PR's latest commit.
    /// </summary>
    Task<List<PrCheckRun>> GetPrCheckRunsAsync(string owner, string repo, int prNumber, CancellationToken ct);

    /// <summary>
    /// Resolves any identifier (build ID, Azure Pipeline link, GitHub PR link, or bare PR number)
    /// to a list of Azure Pipeline builds. For PR identifiers, returns the failed AZP builds from check runs.
    /// </summary>
    Task<List<ResolvedBuild>> ResolveBuildsAsync(string identifier, string? project = null, CancellationToken ct = default);
}

public record GitHubPrLink(string Owner, string Repo, int PrNumber);

public record ResolvedBuild(int BuildId, string? Project, string? PipelineUrl);

public class PrCheckRun
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }

    [JsonPropertyName("details_url")]
    public string? DetailsUrl { get; set; }

    [JsonPropertyName("app_name")]
    public string? AppName { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class PipelineIdentifierHelper(
    IHttpClientFactory httpClientFactory,
    IGitHubService gitHubService,
    IGitHelper gitHelper,
    ILogger<PipelineIdentifierHelper> logger
) : IPipelineIdentifierHelper
{
    private static readonly Regex GitHubPrRegex = new(
        @"https?://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/pull/(?<number>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // AZP build IDs are 7+ digits; GitHub PR numbers are typically ≤ 6 digits
    private const int MaxGitHubPrNumber = 999_999;

    private const string CheckRunsGraphQLQuery = @"
query($owner: String!, $repo: String!, $pr: Int!) {
  repository(owner: $owner, name: $repo) {
    pullRequest(number: $pr) {
      commits(last: 1) {
        nodes {
          commit {
            statusCheckRollup {
              contexts(first: 100) {
                nodes {
                  __typename
                  ... on CheckRun {
                    name
                    conclusion
                    detailsUrl
                    checkSuite { app { name } }
                  }
                  ... on StatusContext {
                    context
                    state
                    targetUrl
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}";

    public (int BuildId, string? Project) Parse(string pipelineIdentifier)
    {
        if (int.TryParse(pipelineIdentifier, out int buildId))
        {
            return (buildId, null);
        }

        if (!Uri.TryCreate(pipelineIdentifier, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid pipeline identifier: {pipelineIdentifier}. Expected a valid absolute URI or an integer.");
        }

        string? project = null;
        var segments = uri.Segments.Select(s => s.Trim('/')).ToList();
        if (segments.Count >= 3)
        {
            project = segments[2];
        }

        var queryParams = HttpUtility.ParseQueryString(uri.Query);
        if (int.TryParse(queryParams.Get("buildId"), out buildId))
        {
            return (buildId, project);
        }

        throw new ArgumentException($"Could not extract buildId from pipeline identifier: {pipelineIdentifier}");
    }

    public async Task<string> GetPipelineProjectAsync(int buildId, string? project, CancellationToken ct)
    {
        var httpClient = httpClientFactory.CreateClient();

        if (project == Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT || string.IsNullOrEmpty(project))
        {
            var pipelineUrl = $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT}/_apis/build/builds/{buildId}?api-version=7.1";
            logger.LogDebug("Getting pipeline details from {url} via http", pipelineUrl);
            var response = await httpClient.GetAsync(pipelineUrl, ct);

            if (string.IsNullOrEmpty(project) && !response.IsSuccessStatusCode)
            {
                return await GetPipelineProjectAsync(buildId, Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT, ct);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
            {
                throw new Exception($"Not authorized to get pipeline details from {pipelineUrl}");
            }
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var projectName = doc.RootElement.GetProperty("project").GetProperty("name").GetString();
            if (string.IsNullOrEmpty(projectName))
            {
                throw new Exception($"Failed to parse project name from build details for build {buildId}");
            }
            return projectName;
        }

        var internalUrl = $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{project}/_apis/build/builds/{buildId}?api-version=7.1";
        logger.LogDebug("Getting pipeline details from {url} via http", internalUrl);
        var internalResponse = await httpClient.GetAsync(internalUrl, ct);
        if (internalResponse.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
        {
            throw new Exception($"Not authorized to get pipeline details from {internalUrl}");
        }
        internalResponse.EnsureSuccessStatusCode();
        var internalJson = await internalResponse.Content.ReadAsStringAsync(ct);
        using var internalDoc = JsonDocument.Parse(internalJson);
        var internalProjectName = internalDoc.RootElement.GetProperty("project").GetProperty("name").GetString();
        return internalProjectName ?? project;
    }

    public string GetPipelineUrl(string project, int buildId)
    {
        return $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{NormalizeProjectName(project)}/_build/results?buildId={buildId}";
    }

    public GitHubPrLink? TryParseGitHubPrLink(string identifier)
    {
        var match = GitHubPrRegex.Match(identifier);
        if (!match.Success)
        {
            return null;
        }

        return new GitHubPrLink(
            match.Groups["owner"].Value,
            match.Groups["repo"].Value,
            int.Parse(match.Groups["number"].Value));
    }

    public async Task<GitHubPrLink?> TryResolveGitHubPrAsync(string identifier, CancellationToken ct)
    {
        // Full PR URL
        var parsed = TryParseGitHubPrLink(identifier);
        if (parsed != null)
        {
            return parsed;
        }

        // Bare PR number (≤ 6 digits) — resolve owner/repo from current git working directory
        if (int.TryParse(identifier, out int prNumber) && prNumber <= MaxGitHubPrNumber)
        {
            try
            {
                var cwd = Directory.GetCurrentDirectory();
                var fullName = await gitHelper.GetRepoFullNameAsync(cwd, findUpstreamParent: true, ct: ct);
                var parts = fullName.Split('/');
                if (parts.Length == 2)
                {
                    logger.LogDebug("Resolved bare PR number {prNumber} to {owner}/{repo}", prNumber, parts[0], parts[1]);
                    return new GitHubPrLink(parts[0], parts[1], prNumber);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not resolve repo from working directory for bare PR number {prNumber}", prNumber);
            }
        }

        return null;
    }

    public async Task<List<PrCheckRun>> GetPrCheckRunsAsync(string owner, string repo, int prNumber, CancellationToken ct)
    {
        var httpClient = httpClientFactory.CreateClient();

        var token = gitHubService.GetAuthToken();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AzureSDKDevToolsMCP", "1.0"));

        var requestBody = JsonSerializer.Serialize(new
        {
            query = CheckRunsGraphQLQuery,
            variables = new { owner, repo, pr = prNumber }
        });

        logger.LogDebug("Querying GitHub GraphQL for PR check runs: {owner}/{repo}#{prNumber}", owner, repo, prNumber);
        var response = await httpClient.PostAsync(
            "https://api.github.com/graphql",
            new StringContent(requestBody, Encoding.UTF8, "application/json"),
            ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("errors", out var errors))
        {
            var errorMsg = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
            throw new Exception($"GitHub GraphQL error: {errorMsg}");
        }

        var checkRuns = new List<PrCheckRun>();
        var nodes = doc.RootElement
            .GetProperty("data")
            .GetProperty("repository")
            .GetProperty("pullRequest")
            .GetProperty("commits")
            .GetProperty("nodes");

        foreach (var commitNode in nodes.EnumerateArray())
        {
            var rollup = commitNode.GetProperty("commit").GetProperty("statusCheckRollup");
            if (rollup.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            foreach (var contextNode in rollup.GetProperty("contexts").GetProperty("nodes").EnumerateArray())
            {
                var typeName = contextNode.GetProperty("__typename").GetString();

                if (typeName == "CheckRun")
                {
                    checkRuns.Add(new PrCheckRun
                    {
                        Type = "CheckRun",
                        Name = contextNode.GetProperty("name").GetString() ?? "",
                        Conclusion = contextNode.GetProperty("conclusion").GetString(),
                        DetailsUrl = contextNode.GetProperty("detailsUrl").GetString(),
                        AppName = contextNode.GetProperty("checkSuite")
                            .GetProperty("app")
                            .GetProperty("name").GetString(),
                    });
                }
                else if (typeName == "StatusContext")
                {
                    checkRuns.Add(new PrCheckRun
                    {
                        Type = "StatusContext",
                        Name = contextNode.GetProperty("context").GetString() ?? "",
                        Conclusion = contextNode.GetProperty("state").GetString(),
                        DetailsUrl = contextNode.GetProperty("targetUrl").GetString(),
                        AppName = "StatusContext",
                    });
                }
            }
        }

        return checkRuns;
    }

    public async Task<List<ResolvedBuild>> ResolveBuildsAsync(string identifier, string? project = null, CancellationToken ct = default)
    {
        // Check if this is a GitHub PR identifier (URL or bare PR number)
        var prLink = await TryResolveGitHubPrAsync(identifier, ct);
        if (prLink != null)
        {
            return await ResolveBuildsFromPrAsync(prLink, project, ct);
        }

        // Single DevOps build ID or URL
        var (singleBuildId, parsedProj) = Parse(identifier);
        var resolvedProject = parsedProj ?? project;

        // Resolve GUID project names to human-readable names
        if (!string.IsNullOrEmpty(resolvedProject))
        {
            resolvedProject = await ResolveProjectNameAsync(singleBuildId, resolvedProject, ct);
        }

        return [new ResolvedBuild(singleBuildId, resolvedProject, null)];
    }

    private async Task<List<ResolvedBuild>> ResolveBuildsFromPrAsync(GitHubPrLink prLink, string? project = null, CancellationToken ct = default)
    {
        var checkRuns = await GetPrCheckRunsAsync(prLink.Owner, prLink.Repo, prLink.PrNumber, ct);

        // Filter to Azure Pipelines check runs with FAILURE conclusion, de-dup by buildId
        var builds = new Dictionary<int, ResolvedBuild>();
        var projectNameCache = new Dictionary<string, string>();

        foreach (var run in checkRuns.Where(r => r.AppName == "Azure Pipelines" && r.Conclusion == "FAILURE"))
        {
            if (string.IsNullOrEmpty(run.DetailsUrl))
            {
                continue;
            }

            try
            {
                var (buildId, parsedProject) = Parse(run.DetailsUrl);
                if (builds.ContainsKey(buildId))
                {
                    continue;
                }

                // Resolve GUID project names to human-readable names
                var resolvedProject = parsedProject ?? project;
                if (!string.IsNullOrEmpty(resolvedProject))
                {
                    resolvedProject = await ResolveCachedProjectNameAsync(buildId, resolvedProject, projectNameCache, ct);
                }

                var pipelineUrl = resolvedProject != null
                    ? GetPipelineUrl(resolvedProject, buildId)
                    : run.DetailsUrl;

                builds[buildId] = new ResolvedBuild(buildId, resolvedProject, pipelineUrl);
            }
            catch (ArgumentException)
            {
                logger.LogDebug("Skipping non-DevOps URL: {url}", run.DetailsUrl);
            }
        }

        return [.. builds.Values];
    }

    /// <summary>
    /// If the project looks like a GUID, resolve it to a human-readable name via the API.
    /// </summary>
    private async Task<string> ResolveProjectNameAsync(int buildId, string project = null, CancellationToken ct = default)
    {
        if (Guid.TryParse(project, out _))
        {
            try
            {
                var resolved = await GetPipelineProjectAsync(buildId, project, ct);
                return NormalizeProjectName(resolved);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not resolve project GUID {project} for build {buildId}", project, buildId);
            }
        }
        return NormalizeProjectName(project);
    }

    /// <summary>
    /// Cached version of ResolveProjectNameAsync to avoid redundant API calls for the same GUID.
    /// </summary>
    private async Task<string> ResolveCachedProjectNameAsync(int buildId, string project, Dictionary<string, string> cache, CancellationToken ct = default)
    {
        if (cache.TryGetValue(project, out var cached))
        {
            return cached;
        }

        var resolved = await ResolveProjectNameAsync(buildId, project, ct);
        cache[project] = resolved;
        return resolved;
    }

    /// <summary>
    /// Normalizes known DevOps project names to their human-readable form.
    /// </summary>
    private static string NormalizeProjectName(string project)
    {
        return project.ToLowerInvariant() switch
        {
            "azure-sdk-public" => Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT,
            "azure-sdk-internal" => Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT,
            _ => project,
        };
    }
}
