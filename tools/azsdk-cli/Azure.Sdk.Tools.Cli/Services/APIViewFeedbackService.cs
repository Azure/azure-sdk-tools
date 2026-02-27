// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tools.APIView;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Helper interface for APIView feedback customizations operations
/// </summary>
public interface IAPIViewFeedbackService
{
    Task<List<ConsolidatedComment>> GetConsolidatedComments(string revisionId);
    Task<ReviewMetadata> ParseReviewMetadata(string revisionId);
    Task<List<FeedbackItem>> GetFeedbackItemsAsync(string apiViewUrl, CancellationToken ct = default);
    Task<string?> GetLanguageAsync(string apiViewUrl, CancellationToken ct = default);
    Task<(string? commitSha, string? tspProjectPath, string? targetRepo)> DetectShaAndTspPath(ReviewMetadata metadata);
}

/// <summary>
/// Helper class for APIView feedback customizations operations
/// </summary>
public class APIViewFeedbackService : IAPIViewFeedbackService
{
    private readonly IAPIViewService _apiViewService;
    private readonly OpenAIClient _openAIClient;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<APIViewFeedbackService> _logger;

    public APIViewFeedbackService(
        IAPIViewService apiViewService,
        OpenAIClient openAIClient,
        IGitHubService gitHubService,
        ILogger<APIViewFeedbackService> logger)
    {
        _apiViewService = apiViewService;
        _openAIClient = openAIClient;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    /// <summary>
    /// Gets consolidated comments from an APIView revision by filtering, grouping, and consolidating comments
    /// </summary>
    public async Task<List<ConsolidatedComment>> GetConsolidatedComments(string revisionId)
    {
        _logger.LogInformation("Getting comments for revision {RevisionId}", revisionId);

        // Get comments from APIViewService - returns JSON string
        var commentsJson = await _apiViewService.GetCommentsByRevisionAsync(revisionId);

        if (string.IsNullOrWhiteSpace(commentsJson))
        {
            _logger.LogError("Failed to retrieve comments from APIView for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"APIView returned empty response for revision {revisionId}");
        }

        // Deserialize comments
        List<APIViewComment>? comments;
        try
        {
            comments = JsonSerializer.Deserialize<List<APIViewComment>>(commentsJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize comments JSON for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"Failed to parse APIView response for revision {revisionId}", ex);
        }

        if (comments?.Count == 0)
        {
            _logger.LogInformation("No comments found for revision {RevisionId}", revisionId);
            return new List<ConsolidatedComment>();
        }

        // 1. Filter out resolved, Question severity comments, and comments with no text
        var filteredComments = comments
            .Where(c => !c.IsResolved && c.Severity != "Question" && !string.IsNullOrWhiteSpace(c.CommentText))
            .ToList();

        if (filteredComments.Count == 0)
        {
            _logger.LogInformation("No actionable comments for revision {RevisionId} after filtering (all resolved or questions)", revisionId);
            return new List<ConsolidatedComment>();
        }

        _logger.LogInformation("Found {Count} actionable comment(s) after filtering", filteredComments.Count);

        // 2. Group comments by threadId and order by createdOn
        var groupedComments = filteredComments
            .GroupBy(c => c.ThreadId ?? string.Empty)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.CreatedOn).ToList()
            );

        // 3. Consolidate grouped comments using OpenAI
        var consolidatedComments = new List<ConsolidatedComment>();

        foreach (var group in groupedComments)
        {
            var consolidated = await ConsolidateComments(group.Value);
            consolidatedComments.Add(consolidated);

            _logger.LogInformation(
                "Consolidated discussion - ThreadId: {ThreadId}, LineNo: {LineNo}, Comment: {Comment}",
                consolidated.ThreadId,
                consolidated.LineNo,
                consolidated.Comment);
        }

        return consolidatedComments;
    }

    /// <summary>
    /// Consolidates grouped comments using OpenAI to determine conclusion
    /// </summary>
    private async Task<ConsolidatedComment> ConsolidateComments(List<APIViewComment> groupedComments)
    {
        var firstComment = groupedComments.First();
        var threadId = firstComment.ThreadId ?? string.Empty;
        var lineNo = firstComment.LineNo;
        var lineText = firstComment.LineText ?? string.Empty;
        var lineId = firstComment.LineId ?? string.Empty;

        // If only one comment, use it directly as the comment
        if (groupedComments.Count == 1)
        {
            return new ConsolidatedComment
            {
                ThreadId = threadId,
                LineNo = lineNo,
                LineId = lineId,
                LineText = lineText,
                Comment = firstComment.CommentText!,
            };
        }

        // Build discussion text from grouped comments
        var discussion = string.Join("\n\n", groupedComments.Select((c, idx) =>
            $"Comment {idx + 1} (by {c.CreatedBy} at {c.CreatedOn}):\n{c.CommentText}"));

        var prompt = $@"Read the following API review discussion thread and extract the final decision or action in a clear, direct statement.

Discussion:
{discussion}

Requirements:
- Use imperative/active voice (e.g., ""Remove X"", ""Keep Y as is"", ""Change Z to..."")
- Be concise and actionable (1-2 sentences max)
- State WHAT to do and WHY briefly
- Avoid passive constructions like ""The discussion concluded..."" or ""It was decided...""
- If no clear decision, state the open question directly

Examples:
- Good: ""Remove 'widget_' prefix since it's redundant.""
- Bad: ""The discussion concluded with the decision to remove the 'widget_' prefix due to redundancy.""
- Good: ""Keep this as is since cancelled/canceled are both used in the TypeSpec.""
- Bad: ""The discussion highlights the inconsistency but concluded to keep the current mixed usage for alignment purposes.""
- Good: ""No changes needed - delete operations should return the status object per REST API design.""
- Bad: ""After discussion, it was determined that the current behavior aligns with the REST API which returns an object, so no action is required.""

Respond in JSON format:
{{
  ""comment"": ""direct actionable statement""
}}";

        var modelName = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL_DEPLOYMENT")
            ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? "gpt-4o";

        try
        {
            var chatClient = _openAIClient.GetChatClient(modelName);
            var messages = new[] { new UserChatMessage(prompt) };
            var response = await chatClient.CompleteChatAsync(messages);

            // Validate response structure
            if (response?.Value?.Content == null || response.Value.Content.Count == 0)
            {
                _logger.LogWarning("OpenAI returned empty or null response for ThreadId {ThreadId}, LineNo {LineNo}. Using fallback.", threadId, lineNo);
                return CreateFallbackComment(groupedComments, threadId, lineNo, lineId, lineText);
            }

            var result = response.Value.Content[0].Text;
            if (string.IsNullOrWhiteSpace(result))
            {
                _logger.LogWarning("OpenAI returned empty text for ThreadId {ThreadId}, LineNo {LineNo}. Using fallback.", threadId, lineNo);
                return CreateFallbackComment(groupedComments, threadId, lineNo, lineId, lineText);
            }

            // Strip markdown code blocks if present (OpenAI sometimes wraps JSON in ```json ... ```)
            var jsonText = result.Trim();
            var match = Regex.Match(jsonText, @"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline);
            if (match.Success)
            {
                jsonText = match.Groups[1].Value.Trim();
            }

            // Parse OpenAI response
            Dictionary<string, object>? jsonResponse;
            try
            {
                jsonResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse OpenAI JSON response for ThreadId {ThreadId}, LineNo {LineNo}. Raw response: {Response}. Using fallback.", threadId, lineNo, result);
                return CreateFallbackComment(groupedComments, threadId, lineNo, lineId, lineText);
            }

            // Extract comment from JSON response
            var comment = jsonResponse?.TryGetValue("comment", out var commentValue) == true
                ? commentValue?.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(comment))
            {
                _logger.LogWarning("OpenAI response missing or empty 'comment' field for ThreadId {ThreadId}, LineNo {LineNo}. Using fallback.", threadId, lineNo);
                return CreateFallbackComment(groupedComments, threadId, lineNo, lineId, lineText);
            }

            return new ConsolidatedComment
            {
                ThreadId = threadId,
                LineNo = lineNo,
                LineId = lineId,
                LineText = lineText,
                Comment = comment
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI consolidation failed for ThreadId {ThreadId}, LineNo {LineNo}. Using fallback.", threadId, lineNo);
            return CreateFallbackComment(groupedComments, threadId, lineNo, lineId, lineText);
        }
    }

    /// <summary>
    /// Creates a fallback consolidated comment by concatenating all comments in the thread
    /// </summary>
    private static ConsolidatedComment CreateFallbackComment(
        List<APIViewComment> groupedComments,
        string threadId,
        int lineNo,
        string lineId,
        string lineText)
    {
        // Format: [user: comment, user: comment, ...]
        var commentChain = string.Join(", ", groupedComments.Select(c =>
            $"{c.CreatedBy}: {c.CommentText}"));

        return new ConsolidatedComment
        {
            ThreadId = threadId,
            LineNo = lineNo,
            LineId = lineId,
            LineText = lineText,
            Comment = $"[{commentChain}]"
        };
    }

    /// <summary>
    /// Gets complete metadata (language, packageName, PR info, etc.) for an APIView revision
    /// </summary>
    public async Task<ReviewMetadata> ParseReviewMetadata(string revisionId)
    {
        _logger.LogInformation("Getting metadata for revision {RevisionId}", revisionId);

        // Get metadata from APIViewService
        var metadataJson = await _apiViewService.GetMetadata(revisionId);

        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            _logger.LogError("Failed to get metadata from APIView for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"Failed to get metadata for revision: {revisionId}");
        }

        // Deserialize metadata
        ReviewMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<ReviewMetadata>(metadataJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize metadata JSON for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"Failed to parse metadata for revision: {revisionId}", ex);
        }

        if (metadata == null)
        {
            _logger.LogError("Deserialized metadata is null for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"Failed to deserialize metadata for revision: {revisionId}");
        }

        // Get additional metadata from resolve endpoint (includes revisionLabel)
        await ParseRevisionLabel(metadata, revisionId);

        _logger.LogInformation(
            "Retrieved metadata - Package: {PackageName}, Language: {Language}, PR: {PullRequestNo}, Repo: {PullRequestRepository}, Label: {RevisionLabel}",
            metadata.PackageName,
            metadata.Language,
            metadata.Revision?.PullRequestNo,
            metadata.Revision?.PullRequestRepository,
            metadata.Revision?.RevisionLabel);

        return metadata;
    }

    /// <summary>
    /// Retrieves and sets the RevisionLabel from the resolve endpoint
    /// </summary>
    // TODO: Resolve endpoint needs to be called to get RevisionLabel. This can be removed once SourceBranch field is available in metadata: https://github.com/Azure/azure-sdk-tools/issues/13661
    private async Task ParseRevisionLabel(ReviewMetadata metadata, string revisionId)
    {
        _logger.LogInformation("Attempting to get revisionLabel from resolve endpoint - ReviewId: {ReviewId}, HasRevision: {HasRevision}",
            metadata.ReviewId, metadata.Revision != null);

        if (!string.IsNullOrEmpty(metadata.ReviewId) && metadata.Revision != null)
        {
            string apiViewUrl = $"https://apiview.dev/review/{metadata.ReviewId}?activeApiRevisionId={revisionId}";
            _logger.LogInformation("Calling Resolve endpoint for URL: {Url}", apiViewUrl);
            var resolveJson = await _apiViewService.Resolve(apiViewUrl);
            if (!string.IsNullOrWhiteSpace(resolveJson))
            {
                _logger.LogInformation("Resolve response received: {Response}", resolveJson);
                try
                {
                    var resolveData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resolveJson);
                    if (resolveData != null && resolveData.TryGetValue("revisionLabel", out var labelElement))
                    {
                        metadata.Revision.RevisionLabel = labelElement.GetString();
                        _logger.LogInformation("Retrieved revisionLabel from resolve endpoint: {RevisionLabel}", metadata.Revision.RevisionLabel);
                    }
                    else
                    {
                        _logger.LogWarning("Resolve response does not contain revisionLabel field");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse resolve response for revision {RevisionId}", revisionId);
                }
            }
            else
            {
                _logger.LogWarning("Resolve endpoint returned empty response");
            }
        }
        else
        {
            _logger.LogWarning("Cannot call Resolve endpoint - ReviewId is null or empty, or Revision is null");
        }
    }

    /// <summary>
    /// Returns feedback items derived from consolidated APIView review comments for the given APIView URL.
    /// </summary>
    public async Task<List<FeedbackItem>> GetFeedbackItemsAsync(string apiViewUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Preprocessing APIView feedback from: {Url}", apiViewUrl);
        var (revisionId, _) = APIViewReviewTool.ExtractIdsFromUrl(apiViewUrl);
        var comments = await GetConsolidatedComments(revisionId);
        var feedbackItems = comments.Select(c =>
        {
            var text = $"API Line {c.LineNo}: {c.LineId}, Code: {c.LineText.Trim()}, ReviewComment: {c.Comment}";
            return new FeedbackItem { Text = text, Context = string.Empty };
        }).ToList();
        _logger.LogInformation("Converted {Count} comments to feedback items", feedbackItems.Count);
        return feedbackItems;
    }

    /// <summary>
    /// Returns the SDK language detected from APIView review metadata for the given APIView URL.
    /// </summary>
    public async Task<string?> GetLanguageAsync(string apiViewUrl, CancellationToken ct = default)
    {
        var (revisionId, _) = APIViewReviewTool.ExtractIdsFromUrl(apiViewUrl);
        var metadata = await ParseReviewMetadata(revisionId);
        return metadata.Language;
    }

    /// <summary>
    /// Detects commit SHA, TypeSpec project path, and TypeSpec repository that an APIView was generated from given review metadata, if available
    /// </summary>
    public async Task<(string? commitSha, string? tspProjectPath, string? targetRepo)> DetectShaAndTspPath(ReviewMetadata metadata)
    {
        _logger.LogInformation("Detecting commit SHA and TypeSpec project path for {Package}", metadata.PackageName);

        var revision = metadata.Revision;
        if (revision == null)
        {
            _logger.LogWarning("No revision metadata available");
            return (null, null, null);
        }

        if (revision.PullRequestNo.HasValue && !string.IsNullOrEmpty(revision.PullRequestRepository))
        {
            var prRepo = revision.PullRequestRepository;
            var prNumber = revision.PullRequestNo.Value;

            _logger.LogInformation("Detected PR #{PrNumber} in {PrRepo}", prNumber, prRepo);

            // Validate repository format
            if (prRepo.Split('/').Length != 2)
            {
                _logger.LogWarning("Invalid PR repository format: {PrRepo}. Expected format: 'owner/repo'", prRepo);
                return (null, null, null);
            }

            // Parse owner/repo from PullRequestRepository (format: "Azure/azure-rest-api-specs")
            var (prOwner, prRepoName) = (prRepo.Split('/')[0], prRepo.Split('/')[1]);

            // Scenario 1: If PR info is provided and PR is in specs (public or private) repo, get SHA directly from PR head
            if (prRepoName.Equals("azure-rest-api-specs", StringComparison.OrdinalIgnoreCase) ||
                prRepoName.Equals("azure-rest-api-specs-pr", StringComparison.OrdinalIgnoreCase))
            {
                return await GetTspShaFromSpecsRepoPR(prOwner, prRepoName, prNumber, prRepo);
            }

            // Scenario 2: If PR info is provided and PR is in language repo, tsp-location.yaml should be parsed to get specs repo and SHA
            return await GetTspShaFromLanguageRepoPR(prOwner, prRepoName, prNumber, prRepo, metadata.PackageName, metadata.Language);
        }

        // Scenario 3: If PR info is not provided, then APIView was generated from source branch in the language SDK repo and tsp-location.yaml should be parsed from there
        // TODO: RevisionLabel currently provides SourceBranch info. Should be updated to use SourceBranch field once it's available: https://github.com/Azure/azure-sdk-tools/issues/13661
        if (!string.IsNullOrEmpty(revision.RevisionLabel))
        {
            return await GetTspShaFromLanguageRepoBranch(revision.RevisionLabel, metadata.PackageName, metadata.Language);
        }

        _logger.LogInformation("No commit SHA or TypeSpec path detected");
        return (null, null, null);
    }

    /// <summary>
    /// Gets the SDK repository name from the language
    /// </summary>
    private static string? GetSdkRepoFromLanguage(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "python" => "azure-sdk-for-python",
            "javascript" or "typescript" or "js" or "ts" => "azure-sdk-for-js",
            "java" => "azure-sdk-for-java",
            "csharp" or "c#" or ".net" or "dotnet" => "azure-sdk-for-net",
            "go" or "golang" => "azure-sdk-for-go",
            _ => null
        };
    }

    /// <summary>
    /// Gets the TypeSpec SHA from an azure-rest-api-specs repository PR (public or private)
    /// </summary>
    private async Task<(string? commitSha, string? tspProjectPath, string? targetRepo)> GetTspShaFromSpecsRepoPR(
        string prOwner,
        string prRepoName,
        int prNumber,
        string prRepo)
    {
        _logger.LogInformation("PR is in {RepoName} - getting head SHA", prRepoName);
        var sha = await GetPrHeadSha(prOwner, prRepoName, prNumber);
        if (sha != null)
        {
            _logger.LogInformation("Retrieved commit SHA from specs PR: {Sha}", sha);
            return (sha, null, prRepo);
        }
        return (null, null, prRepo);
    }

    /// <summary>
    /// Gets the TypeSpec SHA from a language SDK repository PR by parsing tsp-location.yaml
    /// </summary>
    private async Task<(string? commitSha, string? tspProjectPath, string? targetRepo)> GetTspShaFromLanguageRepoPR(
        string prOwner,
        string prRepoName,
        int prNumber,
        string prRepo,
        string packageName,
        string? language)
    {
        _logger.LogInformation("PR is in SDK repo {PrRepo} - parsing tsp-location.yaml", prRepo);
        var (sha, tspPath, specsRepo) = await ResolveTspSourceInfo(prOwner, prRepoName, packageName, language, prNumber: prNumber);
        if (sha != null || tspPath != null || specsRepo != null)
        {
            _logger.LogInformation("Retrieved from tsp-location.yaml - SHA: {Sha}, TSP Path: {TspPath}, Repo: {Repo}", sha, tspPath, specsRepo);
            return (sha, tspPath, specsRepo);
        }
        return (null, null, null);
    }

    /// <summary>
    /// Gets the TypeSpec SHA from a language SDK repository branch by parsing tsp-location.yaml
    /// </summary>
    private async Task<(string? commitSha, string? tspProjectPath, string? targetRepo)> GetTspShaFromLanguageRepoBranch(
        string revisionLabel,
        string packageName,
        string? language)
    {
        // Parse branch from RevisionLabel (format: "Source Branch:main")
        var branch = revisionLabel;
        if (revisionLabel.StartsWith("Source Branch:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Detected branch in RevisionLabel: {RevisionLabel}", revisionLabel);
            branch = revisionLabel.Substring("Source Branch:".Length).Trim();
        }

        // Determine the SDK repo from language (assume Azure org)
        var branchOwner = "Azure";
        var sdkRepo = GetSdkRepoFromLanguage(language);
        if (sdkRepo != null)
        {
            _logger.LogInformation("Parsing tsp-location.yaml from branch {Branch} in {Owner}/{Repo}", branch, branchOwner, sdkRepo);
            var (sha, tspPath, specsRepo) = await ResolveTspSourceInfo(branchOwner, sdkRepo, packageName, language, branch: branch);
            if (sha != null || tspPath != null || specsRepo != null)
            {
                _logger.LogInformation("Retrieved from tsp-location.yaml in branch - SHA: {Sha}, TSP Path: {TspPath}, Repo: {Repo}", sha, tspPath, specsRepo);
                return (sha, tspPath, specsRepo);
            }
        }
        return (null, null, null);
    }

    /// <summary>
    /// Gets the head SHA of a pull request
    /// </summary>
    private async Task<string?> GetPrHeadSha(string owner, string repo, int prNumber)
    {
        try
        {
            _logger.LogInformation("Getting head SHA for PR #{PrNumber} in {Owner}/{Repo}", prNumber, owner, repo);
            var sha = await _gitHubService.GetPullRequestHeadSha(owner, repo, prNumber);
            _logger.LogInformation("Retrieved head SHA: {Sha}", sha);
            return sha;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get PR head SHA for #{PrNumber} in {Owner}/{Repo}", prNumber, owner, repo);
            return null;
        }
    }

    /// <summary>
    /// Resolves TypeSpec source location from tsp-location.yaml file, including the target specs repo
    /// Uses GitHub Code Search to find the file at pattern: sdk/*/packageName/tsp-location.yaml
    /// </summary>
    private async Task<(string? commitSha, string? tspProjectPath, string? targetRepo)> ResolveTspSourceInfo(string owner, string repo, string packageName, string? language, int? prNumber = null, string? branch = null)
    {
        try
        {
            // Find tsp-location.yaml using GitHub Code Search with pattern: sdk/*/packageName/tsp-location.yaml
            var tspLocationPath = await FindTspLocationPath(owner, repo, packageName);

            if (string.IsNullOrEmpty(tspLocationPath))
            {
                _logger.LogWarning("Could not find tsp-location.yaml for package {PackageName} in {Owner}/{Repo}", packageName, owner, repo);
                return (null, null, null);
            }

            _logger.LogInformation("Found tsp-location.yaml at path: {TspLocationPath}", tspLocationPath);

            string? fileContent;

            if (prNumber.HasValue)
            {
                _logger.LogInformation("Getting tsp-location.yaml from PR #{PrNumber} in {Owner}/{Repo}", prNumber.Value, owner, repo);
                fileContent = await _gitHubService.GetFileFromPullRequest(owner, repo, prNumber.Value, tspLocationPath);
            }
            else if (!string.IsNullOrEmpty(branch))
            {
                _logger.LogInformation("Getting tsp-location.yaml from branch {Branch} in {Owner}/{Repo}", branch, owner, repo);
                fileContent = await _gitHubService.GetFileFromBranch(owner, repo, branch, tspLocationPath);
            }
            else
            {
                _logger.LogWarning("Neither PR number nor branch specified for tsp-location.yaml lookup");
                return (null, null, null);
            }

            if (string.IsNullOrEmpty(fileContent))
            {
                _logger.LogInformation("tsp-location.yaml not found");
                return (null, null, null);
            }

            return ParseTspLocationYamlWithRepo(fileContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve TypeSpec source location from tsp-location.yaml");
            return (null, null, null);
        }
    }

    /// <summary>
    /// Finds tsp-location.yaml path using GitHub Code Search with pattern: sdk/*/packageName/tsp-location.yaml
    /// The * matches exactly one directory level (the service name)
    /// </summary>
    private async Task<string?> FindTspLocationPath(string owner, string repo, string packageName)
    {
        try
        {
            // Use GitHub Code Search API to find the file
            // Search query: filename:tsp-location.yaml repo:owner/repo path:sdk
            // We can't use wildcards in path, so search all tsp-location.yaml files and filter by package name
            var searchQuery = $"filename:tsp-location.yaml repo:{owner}/{repo} path:sdk";

            _logger.LogInformation("Searching for tsp-location.yaml with query: {SearchQuery}", searchQuery);

            var results = await _gitHubService.SearchFilesAsync(searchQuery);

            if (results.Items.Count == 0)
            {
                _logger.LogWarning("No tsp-location.yaml found in sdk/ directory");
                return null;
            }

            // Filter results to match pattern sdk/*/packageName/tsp-location.yaml (exactly one level deep)
            var matchingPath = results.Items
                .Select(item => item.Path)
                .FirstOrDefault(path =>
                {
                    // Check if path matches sdk/{service}/{package}/tsp-location.yaml pattern
                    var parts = path.Split('/');
                    return parts.Length == 4 &&
                           parts[0] == "sdk" &&
                           parts[2] == packageName &&
                           parts[3] == "tsp-location.yaml";
                });

            if (matchingPath == null)
            {
                _logger.LogWarning("Found {Count} tsp-location.yaml files but none match pattern sdk/*/{{packageName}}/tsp-location.yaml for package {PackageName}",
                    results.Items.Count, packageName);
                return null;
            }

            if (results.Items.Count > 1)
            {
                _logger.LogInformation("Multiple tsp-location.yaml files found, using: {Path}", matchingPath);
            }

            return matchingPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for tsp-location.yaml for package {PackageName}", packageName);
            return null;
        }
    }

    /// <summary>
    /// Parses tsp-location.yaml content to extract commit, directory, and repo
    /// </summary>
    private (string? commitSha, string? tspProjectPath, string? targetRepo) ParseTspLocationYamlWithRepo(string yamlContent)
    {
        try
        {
            string? commit = null;
            string? directory = null;
            string? targetRepo = null;

            // Simple line-by-line parsing for commit, directory, and repo fields
            var lines = yamlContent.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("commit:", StringComparison.OrdinalIgnoreCase))
                {
                    commit = trimmedLine.Substring(7).Trim().Trim('\'', '\"');
                }
                else if (trimmedLine.StartsWith("directory:", StringComparison.OrdinalIgnoreCase))
                {
                    directory = trimmedLine.Substring(10).Trim().Trim('\'', '\"');
                }
                else if (trimmedLine.StartsWith("repo:", StringComparison.OrdinalIgnoreCase))
                {
                    // repo field is typically a URL like "Azure/azure-rest-api-specs"
                    targetRepo = trimmedLine.Substring(5).Trim().Trim('\'', '\"');
                }
            }

            _logger.LogInformation("Parsed tsp-location.yaml - Commit: {Commit}, Directory: {Directory}, Repo: {Repo}", commit, directory, targetRepo);
            return (commit, directory, targetRepo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse tsp-location.yaml content");
            return (null, null, null);
        }
    }
}
