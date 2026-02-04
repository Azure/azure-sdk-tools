// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.APIView;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Represents complete metadata for an APIView review
/// </summary>
public class ReviewMetadata
{
    [JsonPropertyName("reviewId")]
    public string ReviewId { get; set; } = string.Empty;
    
    [JsonPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;
    
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
    
    [JsonPropertyName("revision")]
    public RevisionMetadata? Revision { get; set; }
}

/// <summary>
/// Represents revision-specific metadata from APIView
/// </summary>
public class RevisionMetadata
{
    [JsonPropertyName("revisionId")]
    public string RevisionId { get; set; } = string.Empty;
    
    [JsonPropertyName("pullRequestNo")]
    public int? PullRequestNo { get; set; }
    
    [JsonPropertyName("pullRequestRepository")]
    public string? PullRequestRepository { get; set; }
    
    [JsonPropertyName("revisionLabel")]
    public string? RevisionLabel { get; set; }
}

/// <summary>
/// Represents a consolidated comment from a discussion thread
/// </summary>
// TODO: Add ThreadUrl property for direct links to APIView discussion threads
public class ConsolidatedComment
{
    public string ThreadId { get; set; } = string.Empty;
    public int LineNo { get; set; }
    
    /// <summary>
    /// Line identifier from APIView. Note: _lineId is Python-specific and serves as 
    /// placeholder for fullObjectPath until fullObjectPath is added to APIView API.
    /// </summary>
    public string? LineId { get; set; } = string.Empty;
    
    public string LineText { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// Helper interface for APIView feedback customizations operations
/// </summary>
public interface IAPIViewFeedbackCustomizationsHelpers
{
    Task<List<ConsolidatedComment>> GetConsolidatedComments(string apiViewUrl);
    Task<ReviewMetadata> GetMetadata(string apiViewUrl);
    Task<(string? commitSha, string? tspProjectPath)> DetectShaAndTspPath(ReviewMetadata metadata, string owner, string repo);
}

/// <summary>
/// Represents a raw comment from APIView API
/// </summary>
internal class APIViewComment
{
    [JsonPropertyName("lineNo")]
    public int LineNo { get; set; }
    
    [JsonPropertyName("_lineId")]
    public string? LineId { get; set; }
    
    [JsonPropertyName("_lineText")]
    public string? LineText { get; set; }
    
    [JsonPropertyName("createdOn")]
    public string? CreatedOn { get; set; }
    
    [JsonPropertyName("upvotes")]
    public int Upvotes { get; set; }
    
    [JsonPropertyName("downvotes")]
    public int Downvotes { get; set; }
    
    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }
    
    [JsonPropertyName("commentText")]
    public string? CommentText { get; set; }
    
    [JsonPropertyName("isResolved")]
    public bool IsResolved { get; set; }
    
    [JsonPropertyName("severity")]
    public string? Severity { get; set; }
    
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; set; }
}

/// <summary>
/// Helper class for APIView feedback customizations operations
/// </summary>
public class APIViewFeedbackCustomizationsHelpers : IAPIViewFeedbackCustomizationsHelpers
{
    private readonly IAPIViewService _apiViewService;
    private readonly IAPIViewHttpService _apiViewHttpService;
    private readonly OpenAIClient _openAIClient;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<APIViewFeedbackCustomizationsHelpers> _logger;

    public APIViewFeedbackCustomizationsHelpers(
        IAPIViewService apiViewService,
        IAPIViewHttpService apiViewHttpService,
        OpenAIClient openAIClient,
        IGitHubService gitHubService,
        ILogger<APIViewFeedbackCustomizationsHelpers> logger)
    {
        _apiViewService = apiViewService;
        _apiViewHttpService = apiViewHttpService;
        _openAIClient = openAIClient;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    /// <summary>
    /// Gets consolidated comments from an APIView URL by filtering, grouping, and consolidating comments
    /// </summary>
    public async Task<List<ConsolidatedComment>> GetConsolidatedComments(string apiViewUrl)
    {
        // Parse the URL to get revisionId and reviewId
        var (revisionId, reviewId) = ExtractIdsFromUrl(apiViewUrl);
        var environment = DetectEnvironmentFromUrl(apiViewUrl);
        
        _logger.LogInformation("Getting comments for revision {RevisionId} in review {ReviewId} (environment: {Environment})", revisionId, reviewId, environment);
        
        // Get comments from APIViewService - returns JSON string
        var commentsJson = await _apiViewService.GetCommentsByRevisionAsync(revisionId, environment);
        
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
        
        if (comments == null)
        {
            _logger.LogError("Deserialized comments are null for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"APIView response deserialized to null for revision {revisionId}");
        }
        
        if (!comments.Any())
        {
            _logger.LogInformation("No comments found for revision {RevisionId}", revisionId);
            return new List<ConsolidatedComment>();
        }

        // 1. Filter out resolved, Question severity comments, and comments with no text
        var filteredComments = comments
            .Where(c => !c.IsResolved && c.Severity != "Question" && !string.IsNullOrWhiteSpace(c.CommentText))
            .ToList();
        
        if (!filteredComments.Any())
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

        var chatClient = _openAIClient.GetChatClient(modelName);
        var messages = new[] { new UserChatMessage(prompt) };
        var response = await chatClient.CompleteChatAsync(messages);
        var result = response.Value.Content[0].Text;

        // Strip markdown code blocks if present (OpenAI sometimes wraps JSON in ```json ... ```)
        var jsonText = result.Trim();
        if (jsonText.StartsWith("```"))
        {
            // Remove markdown code blocks
            var lines = jsonText.Split('\n');
            jsonText = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
        }

        // Parse OpenAI response
        var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText);
        
        return new ConsolidatedComment
        {
            ThreadId = threadId,
            LineNo = lineNo,
            LineId = lineId,
            LineText = lineText,
            Comment = jsonResponse?["comment"]?.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Gets complete metadata (language, packageName, PR info, etc.) for an APIView URL
    /// </summary>
    public async Task<ReviewMetadata> GetMetadata(string apiViewUrl)
    {
        var environment = DetectEnvironmentFromUrl(apiViewUrl);
        var (revisionId, reviewId) = ExtractIdsFromUrl(apiViewUrl);
        
        _logger.LogInformation("Getting metadata for revision {RevisionId} (environment: {Environment})", revisionId, environment);
        
        // Call the metadata endpoint with revisionId
        var endpoint = $"/api/reviews/metadata?revisionId={revisionId}";
        var metadataJson = await _apiViewHttpService.GetAsync(endpoint, environment);
        
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            _logger.LogError("Failed to get metadata from APIView for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"Failed to resolve APIView URL: {apiViewUrl}");
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
            throw new InvalidOperationException($"Failed to parse metadata for APIView URL: {apiViewUrl}", ex);
        }
        
        if (metadata == null)
        {
            _logger.LogError("Deserialized metadata is null for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"Failed to deserialize metadata for APIView URL: {apiViewUrl}");
        }

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
    /// Extracts revision ID and review ID from an APIView URL
    /// </summary>
    private static (string revisionId, string reviewId) ExtractIdsFromUrl(string apiViewUrl)
    {
        var uri = new Uri(apiViewUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        
        // Extract revisionId from query parameter
        var revisionId = query["activeApiRevisionId"] ?? string.Empty;
        
        // Extract reviewId from path (format: /review/{reviewId})
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var reviewId = segments.Length >= 2 && segments[0] == "review" ? segments[1] : string.Empty;
        
        if (string.IsNullOrEmpty(revisionId) || string.IsNullOrEmpty(reviewId))
        {
            throw new ArgumentException($"Could not extract IDs from URL: {apiViewUrl}");
        }
        
        return (revisionId, reviewId);
    }

    /// <summary>
    /// Detects commit SHA and TypeSpec project path from review metadata
    /// </summary>
    public async Task<(string? commitSha, string? tspProjectPath)> DetectShaAndTspPath(ReviewMetadata metadata, string owner, string repo)
    {
        _logger.LogInformation("Detecting commit SHA and TypeSpec project path for {Package}", metadata.PackageName);
        
        var revision = metadata.Revision;
        if (revision == null)
        {
            _logger.LogWarning("No revision metadata available");
            return (null, null);
        }

        // Scenario 1: PR in specs repo (azure-rest-api-specs)
        if (revision.PullRequestNo.HasValue && !string.IsNullOrEmpty(revision.PullRequestRepository))
        {
            var prRepo = revision.PullRequestRepository;
            var prNumber = revision.PullRequestNo.Value;
            
            _logger.LogInformation("Detected PR #{PrNumber} in {PrRepo}", prNumber, prRepo);
            
            // Parse owner/repo from PullRequestRepository (format: "Azure/azure-rest-api-specs")
            var prParts = prRepo.Split('/');
            if (prParts.Length != 2)
            {
                _logger.LogWarning("Invalid PR repository format: {PrRepo}", prRepo);
                return (null, null);
            }
            var prOwner = prParts[0];
            var prRepoName = prParts[1];
            
            // Check if this is the specs repo
            if (prRepoName.Equals("azure-rest-api-specs", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("PR is in azure-rest-api-specs - getting head SHA");
                var sha = await GetPrHeadSha(prOwner, prRepoName, prNumber);
                if (sha != null)
                {
                    _logger.LogInformation("Retrieved commit SHA from specs PR: {Sha}", sha);
                    return (sha, null);
                }
            }
            // Scenario 2: PR in SDK repo - parse tsp-location.yaml
            else
            {
                _logger.LogInformation("PR is in SDK repo {PrRepo} - parsing tsp-location.yaml", prRepo);
                var (sha, tspPath) = await ResolveTspSourceLocation(prOwner, prRepoName, prNumber: prNumber);
                if (sha != null || tspPath != null)
                {
                    _logger.LogInformation("Retrieved from tsp-location.yaml - SHA: {Sha}, TSP Path: {TspPath}", sha, tspPath);
                    return (sha, tspPath);
                }
            }
        }
        
        // Scenario 3: Branch in RevisionLabel - parse tsp-location.yaml from branch
        if (!string.IsNullOrEmpty(revision.RevisionLabel) && revision.RevisionLabel.Contains('/'))
        {
            _logger.LogInformation("Detected branch in RevisionLabel: {RevisionLabel}", revision.RevisionLabel);
            
            // Parse branch from RevisionLabel (format: "owner:branch" or "branch")
            var branchOwner = owner;
            var branch = revision.RevisionLabel;
            
            if (revision.RevisionLabel.Contains(':'))
            {
                var parts = revision.RevisionLabel.Split(':');
                branchOwner = parts[0];
                branch = parts[1];
            }
            
            _logger.LogInformation("Parsing tsp-location.yaml from branch {Branch} in {Owner}/{Repo}", branch, branchOwner, repo);
            var (sha, tspPath) = await ResolveTspSourceLocation(branchOwner, repo, branch: branch);
            if (sha != null || tspPath != null)
            {
                _logger.LogInformation("Retrieved from tsp-location.yaml in branch - SHA: {Sha}, TSP Path: {TspPath}", sha, tspPath);
                return (sha, tspPath);
            }
        }
        
        _logger.LogInformation("No commit SHA or TypeSpec path detected");
        return (null, null);
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
    /// Resolves TypeSpec source location from tsp-location.yaml file
    /// </summary>
    private async Task<(string? commitSha, string? tspProjectPath)> ResolveTspSourceLocation(string owner, string repo, int? prNumber = null, string? branch = null)
    {
        try
        {
            string? fileContent;
            
            if (prNumber.HasValue)
            {
                _logger.LogInformation("Getting tsp-location.yaml from PR #{PrNumber} in {Owner}/{Repo}", prNumber.Value, owner, repo);
                fileContent = await _gitHubService.GetFileFromPullRequest(owner, repo, prNumber.Value, "tsp-location.yaml");
            }
            else if (!string.IsNullOrEmpty(branch))
            {
                _logger.LogInformation("Getting tsp-location.yaml from branch {Branch} in {Owner}/{Repo}", branch, owner, repo);
                fileContent = await _gitHubService.GetFileFromBranch(owner, repo, branch, "tsp-location.yaml");
            }
            else
            {
                _logger.LogWarning("Neither PR number nor branch specified for tsp-location.yaml lookup");
                return (null, null);
            }
            
            if (string.IsNullOrEmpty(fileContent))
            {
                _logger.LogInformation("tsp-location.yaml not found");
                return (null, null);
            }
            
            return ParseTspLocationYaml(fileContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve TypeSpec source location from tsp-location.yaml");
            return (null, null);
        }
    }

    /// <summary>
    /// Parses tsp-location.yaml content to extract commit and directory
    /// </summary>
    private (string? commitSha, string? tspProjectPath) ParseTspLocationYaml(string yamlContent)
    {
        try
        {
            string? commit = null;
            string? directory = null;
            
            // Simple line-by-line parsing for commit and directory fields
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
            }
            
            _logger.LogInformation("Parsed tsp-location.yaml - Commit: {Commit}, Directory: {Directory}", commit, directory);
            return (commit, directory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse tsp-location.yaml content");
            return (null, null);
        }
    }

    /// <summary>
    /// Detects the APIView environment from the URL
    /// </summary>
    private static string DetectEnvironmentFromUrl(string apiViewUrl)
    {
        if (apiViewUrl.Contains("apiviewstagingtest.com", StringComparison.OrdinalIgnoreCase) ||
            apiViewUrl.Contains("spa.apiviewstagingtest.com", StringComparison.OrdinalIgnoreCase) ||
            apiViewUrl.Contains("apiview-staging.dev", StringComparison.OrdinalIgnoreCase))
        {
            return "staging";
        }
        
        if (apiViewUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return "local";
        }
        
        return "production";
    }
}
