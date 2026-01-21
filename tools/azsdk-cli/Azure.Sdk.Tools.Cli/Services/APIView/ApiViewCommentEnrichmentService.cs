// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services.APIView;

/// <summary>
/// Represents an APIView comment enriched with the corresponding line text.
/// </summary>
public class EnrichedApiViewComment
{
    [JsonPropertyName("line_text")]
    public string LineText { get; set; } = "";

    [JsonPropertyName("comment_text")]
    public string CommentText { get; set; } = "";
}

/// <summary>
/// Service for fetching APIView comments and enriching them with line text.
/// </summary>
public interface IApiViewCommentEnrichmentService
{
    /// <summary>
    /// Fetches actionable comments from an APIView URL and enriches them with line text.
    /// Excludes resolved comments and those labeled as "Question".
    /// </summary>
    /// <param name="apiViewUrl">The full APIView URL (e.g., https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId})</param>
    /// <returns>List of enriched comments, or null if retrieval fails</returns>
    Task<List<EnrichedApiViewComment>?> GetEnrichedCommentsAsync(string apiViewUrl);

    /// <summary>
    /// Formats enriched comments into a string suitable for LLM consumption.
    /// </summary>
    string FormatForPrompt(List<EnrichedApiViewComment> comments);
}

public class ApiViewCommentEnrichmentService : IApiViewCommentEnrichmentService
{
    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<ApiViewCommentEnrichmentService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiViewCommentEnrichmentService(
        IAPIViewService apiViewService,
        ILogger<ApiViewCommentEnrichmentService> logger)
    {
        _apiViewService = apiViewService;
        _logger = logger;
    }

    public async Task<List<EnrichedApiViewComment>?> GetEnrichedCommentsAsync(string apiViewUrl)
    {
        try
        {
            _logger.LogDebug("Fetching APIView data from {Url}", apiViewUrl);

            // Parse the URL to extract IDs using shared parser
            var (revisionId, reviewId) = ApiViewUrlParser.ExtractIds(apiViewUrl);
            string environment = IAPIViewHttpService.DetectEnvironmentFromUrl(apiViewUrl);

            // Step 1: Get content first to build line number map
            var textContent = await _apiViewService.GetRevisionContent(revisionId, reviewId, "text", environment);

            if (string.IsNullOrWhiteSpace(textContent))
            {
                _logger.LogWarning("No text content retrieved from APIView");
                return null;
            }

            // The API returns content as a JSON-encoded string (with escaped newlines).
            // Deserialize it to get the actual content with real newlines.
            if (textContent.StartsWith("\"") && textContent.EndsWith("\""))
            {
                try
                {
                    textContent = JsonSerializer.Deserialize<string>(textContent) ?? textContent;
                }
                catch (JsonException)
                {
                    // If deserialization fails, use the content as-is
                    _logger.LogWarning("Content appears JSON-encoded but deserialization failed; using raw content");
                }
            }

            // Step 2: Build line number to text map (1-indexed)
            var lineMap = BuildLineMap(textContent);
            _logger.LogDebug("Built line map with {Count} lines. Content length: {Length}", 
                lineMap.Count, textContent.Length);

            // Step 3: Get comments
            var commentsJson = await _apiViewService.GetCommentsByRevisionAsync(revisionId, environment);

            if (string.IsNullOrWhiteSpace(commentsJson))
            {
                _logger.LogInformation("No comments found for APIView");
                return new List<EnrichedApiViewComment>();
            }

            // Step 4: Parse comments
            var comments = JsonSerializer.Deserialize<List<RawApiViewComment>>(commentsJson, JsonOptions);
            if (comments == null)
            {
                _logger.LogWarning("Failed to parse comments JSON");
                return null;
            }

            // Step 5: Filter and enrich comments
            // Keep only: unresolved AND not labeled as "Question"
            var enriched = comments
                .Where(c => !c.IsResolved)
                .Where(c => !string.Equals(c.CommentType, "Question", StringComparison.OrdinalIgnoreCase))
                .Select(c => EnrichComment(c, lineMap))
                .ToList();

            _logger.LogInformation("Retrieved {Total} comments, {Filtered} actionable (excluding resolved and questions)",
                comments.Count, enriched.Count);

            return enriched;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid APIView URL: {Url}", apiViewUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve enriched comments from {Url}", apiViewUrl);
            return null;
        }
    }

    public string FormatForPrompt(List<EnrichedApiViewComment> comments)
    {
        if (comments.Count == 0)
        {
            return "No unresolved APIView comments found.";
        }

        var lines = new List<string>
        {
            $"## APIView Feedback ({comments.Count} unresolved comments)",
            "",
            "| line_text | comment_text |",
            "|-----------|--------------|"
        };

        foreach (var comment in comments)
        {
            var lineText = string.IsNullOrEmpty(comment.LineText) ? "(general)" : $"`{comment.LineText}`";
            lines.Add($"| {lineText} | {comment.CommentText} |");
        }

        return string.Join("\n", lines);
    }

    private static Dictionary<int, string> BuildLineMap(string content)
    {
        // The raw API content doesn't have any header - it starts directly with the API surface.
        // Line numbers in APIView comments are 1-indexed.
        var lines = content.Split('\n');
        var lineMap = new Dictionary<int, string>();
        
        for (int i = 0; i < lines.Length; i++)
        {
            int lineNumber = i + 1; // 1-indexed
            lineMap[lineNumber] = lines[i].TrimEnd('\r');
        }
        
        return lineMap;
    }

    private static EnrichedApiViewComment EnrichComment(RawApiViewComment raw, Dictionary<int, string> lineMap)
    {
        string? lineText = null;
        if (raw.LineNo.HasValue && lineMap.TryGetValue(raw.LineNo.Value, out var text))
        {
            lineText = text;
        }

        return new EnrichedApiViewComment
        {
            LineText = lineText ?? "",
            CommentText = raw.CommentText ?? ""
        };
    }

    /// <summary>
    /// Raw comment structure from APIView API.
    /// </summary>
    private class RawApiViewComment
    {
        public int? LineNo { get; set; }
        public string? CommentText { get; set; }
        public string? CommentType { get; set; }
        public bool IsResolved { get; set; }
    }
}
