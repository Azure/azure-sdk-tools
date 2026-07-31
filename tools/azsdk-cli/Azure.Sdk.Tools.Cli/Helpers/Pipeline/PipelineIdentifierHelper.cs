// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Helpers.Pipeline;

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
    /// Resolves any identifier (build ID, Azure Pipeline link, GitHub PR link, or bare PR number)
    /// to a list of Azure Pipeline builds. For PR identifiers, returns the failed AZP builds from check runs.
    /// </summary>
    Task<List<AzurePipelineBuild>> ResolveBuildsAsync(string identifier, string? project = null, CancellationToken ct = default);

    /// <summary>
    /// Resolves the GitHub repository and commit the given builds ran against. The commit always comes from
    /// the builds themselves, so an identifier naming an old run correlates to the commit that run tested
    /// rather than to whatever the branch or pull request points at now. Returns null when none of the builds
    /// are backed by a GitHub repository.
    /// </summary>
    Task<GitHubCommitRef?> ResolveCommitRefFromBuildsAsync(IEnumerable<AzurePipelineBuild> builds, CancellationToken ct);

    /// <summary>
    /// Resolves the GitHub repository and commit directly from an identifier that names a pull request, for
    /// the case where no build correlates it: a pull request can fail on GitHub Actions alone. The commit is
    /// the pull request's current head. Returns null when the identifier is not a pull request or its head
    /// cannot be read.
    /// </summary>
    Task<GitHubCommitRef?> ResolveCommitRefFromPrAsync(string identifier, CancellationToken ct);
}

public record GitHubPrLink(string Owner, string Repo, int PrNumber);

public class PipelineIdentifierHelper(
    IDevOpsService devOpsService,
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
        var build = await devOpsService.GetBuildDetailsAsync(buildId, project, ct);
        var projectName = build.Project?.Name;
        if (string.IsNullOrEmpty(projectName))
        {
            throw new Exception($"Failed to parse project name from build details for build {buildId}");
        }
        return projectName;
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

    public async Task<List<AzurePipelineBuild>> ResolveBuildsAsync(string identifier, string? project = null, CancellationToken ct = default)
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

        var build = new AzurePipelineBuild(singleBuildId, resolvedProject, null, null, null);
        return new List<AzurePipelineBuild> { await GetBuildStatusesAsync(build, ct) };
    }

    private async Task<List<AzurePipelineBuild>> ResolveBuildsFromPrAsync(GitHubPrLink prLink, string? project = null, CancellationToken ct = default)
    {
        var checkRuns = await gitHubService.GetPrCheckRunsAsync(prLink.Owner, prLink.Repo, prLink.PrNumber, ct);

        // Filter to failed Azure Pipelines check runs (any non-passing conclusion, not just FAILURE), de-dup by buildId
        var builds = new Dictionary<int, AzurePipelineBuild>();
        var projectNameCache = new Dictionary<string, string>();

        foreach (var run in checkRuns.Where(r => r.AppName == "Azure Pipelines" && r.IsFailed))
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

                AzurePipelineBuild build = new AzurePipelineBuild(buildId, resolvedProject, pipelineUrl, null, null);
                builds[buildId] = await GetBuildStatusesAsync(build, ct);
            }
            catch (ArgumentException)
            {
                logger.LogDebug("Skipping non-DevOps URL: {url}", run.DetailsUrl);
            }
        }

        return [.. builds.Values];
    }

    public async Task<GitHubCommitRef?> ResolveCommitRefFromBuildsAsync(IEnumerable<AzurePipelineBuild> builds, CancellationToken ct)
    {
        foreach (var build in builds)
        {
            try
            {
                var commitRef = await devOpsService.ResolveBuildCommitRefAsync(build.BuildId, build.Project, ct);
                if (commitRef != null)
                {
                    logger.LogDebug(
                        "Correlated build {buildId} to {owner}/{repo} @ {sha}",
                        build.BuildId, commitRef.Owner, commitRef.Repo, commitRef.HeadSha);
                    return commitRef;
                }
            }
            catch (Exception ex)
            {
                // Resolution is best effort: a build that cannot be read just means the next one is tried.
                logger.LogDebug(ex, "Could not resolve a GitHub commit for build {buildId}", build.BuildId);
            }
        }

        logger.LogDebug("None of the resolved builds are backed by a GitHub repository and commit");
        return null;
    }

    public async Task<GitHubCommitRef?> ResolveCommitRefFromPrAsync(string identifier, CancellationToken ct)
    {
        var prLink = await TryResolveGitHubPrAsync(identifier, ct);
        if (prLink == null)
        {
            return null;
        }

        try
        {
            var pullRequest = await gitHubService.GetPullRequestAsync(prLink.Owner, prLink.Repo, prLink.PrNumber, ct);
            var headSha = pullRequest?.Head?.Sha;
            if (string.IsNullOrEmpty(headSha))
            {
                logger.LogDebug("No head commit reported for {owner}/{repo}#{pr}", prLink.Owner, prLink.Repo, prLink.PrNumber);
                return null;
            }

            logger.LogDebug("Resolved {owner}/{repo}#{pr} to commit {sha}", prLink.Owner, prLink.Repo, prLink.PrNumber, headSha);
            return new GitHubCommitRef(prLink.Owner, prLink.Repo, headSha, prLink.PrNumber);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read {owner}/{repo}#{pr} to resolve its head commit", prLink.Owner, prLink.Repo, prLink.PrNumber);
            return null;
        }
    }

    /// <summary>
    /// Validates and resolves a project name to a recognized DevOps project. Known names (public/internal)
    /// are trusted as-is; a project GUID is resolved to its real name via the build API, since a build id
    /// uniquely identifies its project. Any other value (the org name "azure-sdk", a typo, etc.) is
    /// unrecognized and rejected with an ArgumentException so callers fail early.
    /// </summary>
    private async Task<string> ResolveProjectNameAsync(int buildId, string? project = null, CancellationToken ct = default)
    {
        var normalized = NormalizeProjectName(project);

        if (normalized == Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT || normalized == Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT)
        {
            return normalized;
        }

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
                throw new ArgumentException(
                    $"Project GUID {project} is valid but could not be resolved for build {buildId} " +
                    "(network error, authentication issue, or the build does not exist under that project).", ex);
            }
        }

        logger.LogDebug("Unrecognized project name {project} for build {buildId}. Expected a known project name (public, internal) or a valid project GUID.", project, buildId);
        throw new ArgumentException($"Unrecognized project name: {project} for build {buildId}. Expected a known project name (public, internal) or a valid project GUID.");
    }

    private async Task<AzurePipelineBuild> GetBuildStatusesAsync(AzurePipelineBuild build, CancellationToken ct)
    {
        try
        {
            // One fetch resolves the owning project (public/internal probe + GUID → name) and the run
            // status/result.
            var details = await devOpsService.GetBuildDetailsAsync(build.BuildId, build.Project, ct);
            var buildProject = NormalizeProjectName(details.Project?.Name ?? build.Project ?? string.Empty);

            var status = details.Status != null ? JsonNamingPolicy.SnakeCaseLower.ConvertName(details.Status.Value.ToString()) : null;
            var result = details.Result != null ? JsonNamingPolicy.SnakeCaseLower.ConvertName(details.Result.Value.ToString()) : null;

            return new AzurePipelineBuild(
                build.BuildId,
                buildProject,
                build.PipelineUrl ?? GetPipelineUrl(buildProject, build.BuildId),
                status ?? AzurePipelineBuild.StatusUnavailable,
                result ?? AzurePipelineBuild.StatusUnavailable);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read status for build {buildId}; marking status unavailable", build.BuildId);

            var fallbackProject = build.Project ?? string.Empty;
            return new AzurePipelineBuild(
                build.BuildId,
                fallbackProject,
                build.PipelineUrl ?? GetPipelineUrl(fallbackProject, build.BuildId),
                AzurePipelineBuild.StatusUnavailable,
                AzurePipelineBuild.StatusUnavailable);
        }
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
