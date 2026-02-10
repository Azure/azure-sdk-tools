// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Services.APIView;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Represents metadata from APIView resolve endpoint
/// </summary>
public class ApiViewMetadata
{
    [JsonPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;
    
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
    
    [JsonPropertyName("revisionLabel")]
    public string? RevisionLabel { get; set; }
}

/// <summary>
/// Represents a consolidated comment from a discussion thread
/// </summary>
public class ConsolidatedComment
{
    public string ThreadId { get; set; } = string.Empty;
    public int LineNo { get; set; }
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
    Task<ApiViewMetadata> GetMetadata(string apiViewUrl);
}

/// <summary>
/// Represents a raw comment from APIView API
/// </summary>
internal class ApiViewComment
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
    private readonly ICopilotClientWrapper _copilotClient;
    private readonly ILogger<APIViewFeedbackCustomizationsHelpers> _logger;

    public APIViewFeedbackCustomizationsHelpers(
        IAPIViewService apiViewService,
        IAPIViewHttpService apiViewHttpService,
        ICopilotClientWrapper copilotClient,
        ILogger<APIViewFeedbackCustomizationsHelpers> logger)
    {
        _apiViewService = apiViewService;
        _apiViewHttpService = apiViewHttpService;
        _copilotClient = copilotClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets consolidated comments from an APIView URL by filtering, grouping, and consolidating comments
    /// </summary>
    public async Task<List<ConsolidatedComment>> GetConsolidatedComments(string apiViewUrl)
    {
        // Parse the URL to get revisionId
        var (revisionId, reviewId) = ApiViewUrlParser.ExtractIds(apiViewUrl);
        
        _logger.LogInformation("Getting comments for revision {RevisionId} in review {ReviewId}", revisionId, reviewId);
        
        // Get comments from APIViewService - returns JSON string
        var commentsJson = await _apiViewService.GetCommentsByRevisionAsync(revisionId);
        
        if (string.IsNullOrWhiteSpace(commentsJson))
        {
            _logger.LogError("Failed to retrieve comments from APIView for revision {RevisionId}", revisionId);
            throw new InvalidOperationException($"APIView returned empty response for revision {revisionId}");
        }

        // Deserialize comments
        List<ApiViewComment>? comments;
        try
        {
            comments = JsonSerializer.Deserialize<List<ApiViewComment>>(commentsJson);
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
    private async Task<ConsolidatedComment> ConsolidateComments(List<ApiViewComment> groupedComments)
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

        var systemPrompt = @"You are an expert at summarizing API review discussions. Read the discussion thread provided and extract the final decision or action in a clear, direct statement.

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
{
  ""comment"": ""direct actionable statement""
}";

        var userPrompt = $"Discussion:\n{discussion}";

        var result = await SendCopilotPromptAsync(systemPrompt, userPrompt);

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
    /// Sends a prompt to the Copilot SDK and returns the assistant's text response
    /// </summary>
    private async Task<string> SendCopilotPromptAsync(string systemPrompt, string userPrompt)
    {
        var sessionConfig = new SessionConfig
        {
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = systemPrompt
            },
            AvailableTools = []
        };

        await using var session = await _copilotClient.CreateSessionAsync(sessionConfig);

        string? assistantResponse = null;
        TaskCompletionSource sessionIdleTcs = new();
        SessionErrorEvent? sessionError = null;

        using var eventSubscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    assistantResponse = msg.Data.Content;
                    break;
                case SessionErrorEvent error:
                    _logger.LogError("Copilot session error: {ErrorType} - {Message}", error.Data.ErrorType, error.Data.Message);
                    sessionError = error;
                    sessionIdleTcs.TrySetResult();
                    break;
                case SessionIdleEvent:
                    sessionIdleTcs.TrySetResult();
                    break;
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = userPrompt });

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await sessionIdleTcs.Task.WaitAsync(timeoutCts.Token);

        if (sessionError != null)
        {
            throw new InvalidOperationException(
                $"Copilot session error: [{sessionError.Data.ErrorType}] {sessionError.Data.Message}");
        }

        return assistantResponse ?? throw new InvalidOperationException("Copilot returned no response");
    }

    /// <summary>
    /// Gets metadata (language, packageName, etc.) for an APIView URL
    /// </summary>
    public async Task<ApiViewMetadata> GetMetadata(string apiViewUrl)
    {
        var environment = IAPIViewHttpService.DetectEnvironmentFromUrl(apiViewUrl);
        
        _logger.LogInformation("Getting metadata for APIView URL: {ApiViewUrl}", apiViewUrl);
        
        // Call the resolve endpoint to get metadata
        var endpoint = $"/api/reviews/resolve?link={Uri.EscapeDataString(apiViewUrl)}";
        var metadataJson = await _apiViewHttpService.GetAsync(endpoint, environment);
        
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new InvalidOperationException($"Failed to resolve APIView URL: {apiViewUrl}");
        }

        // Deserialize metadata
        var metadata = JsonSerializer.Deserialize<ApiViewMetadata>(metadataJson);
        
        if (metadata == null)
        {
            throw new InvalidOperationException($"Failed to deserialize metadata for APIView URL: {apiViewUrl}");
        }

        _logger.LogInformation(
            "Retrieved metadata - Package: {PackageName}, Language: {Language}, RevisionLabel: {RevisionLabel}",
            metadata.PackageName,
            metadata.Language,
            metadata.RevisionLabel);

        return metadata;
    }
}

/// <summary>
/// Feedback input from APIView review comments
/// </summary>
public class APIViewFeedbackItem : IFeedbackItem
{
    private readonly string _apiViewUrl;
    private readonly IAPIViewFeedbackCustomizationsHelpers _helper;
    private readonly ILogger<APIViewFeedbackItem> _logger;

    public APIViewFeedbackItem(
        string apiViewUrl,
        IAPIViewFeedbackCustomizationsHelpers helper,
        ILogger<APIViewFeedbackItem> logger)
    {
        _apiViewUrl = apiViewUrl;
        _helper = helper;
        _logger = logger;
    }

    public async Task<FeedbackContext> PreprocessAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Preprocessing APIView feedback from: {Url}", _apiViewUrl);

        // Get consolidated comments
        var comments = await _helper.GetConsolidatedComments(_apiViewUrl);
        
        // Get metadata
        var metadata = await _helper.GetMetadata(_apiViewUrl);

        // Convert to feedback items
        var feedbackItems = comments.Select(c =>
        {
            var text = $"API Line {c.LineNo}: {c.LineId}, Code: {c.LineText.Trim()}, ReviewComment: {c.Comment}";
            var item = new FeedbackItem
            {
                Text = text,
                Context = string.Empty
            };
            item.FormattedPrompt = $"Id: {item.Id}\nText: {text}\nContext: ";
            return item;
        }).ToList();

        _logger.LogInformation("Converted {Count} comments to feedback items", feedbackItems.Count);

        return new FeedbackContext
        {
            FormattedFeedback = string.Join("\n\n", feedbackItems.Select(f => f.FormattedPrompt)),
            FeedbackItems = feedbackItems,
            Language = metadata.Language,
            PackageName = metadata.PackageName,
            InputType = "apiview",
            Metadata = new Dictionary<string, string>
            {
                ["APIViewUrl"] = _apiViewUrl
            }
        };
    }

    private static string FormatCommentForPrompt(ConsolidatedComment comment, string itemId)
    {
        var text = $"API Line {comment.LineNo}: {comment.LineId}, Code: {comment.LineText.Trim()}, ReviewComment: {comment.Comment}";
        return $"Id: {itemId}\nText: {text}\nContext: ";
    }
}
