// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Helpers;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    /// <summary>
    /// Represents metadata from APIView resolve endpoint
    /// </summary>
    public class ApiViewMetadata
    {
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;
        
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;
        
        [JsonPropertyName("reviewId")]
        public string ReviewId { get; set; } = string.Empty;
        
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
        
        [JsonPropertyName("revisionId")]
        public string RevisionId { get; set; } = string.Empty;
        
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
        private readonly OpenAIClient _openAIClient;
        private readonly ILogger<APIViewFeedbackCustomizationsHelpers> _logger;

        public APIViewFeedbackCustomizationsHelpers(
            IAPIViewService apiViewService,
            OpenAIClient openAIClient,
            ILogger<APIViewFeedbackCustomizationsHelpers> logger)
        {
            _apiViewService = apiViewService;
            _openAIClient = openAIClient;
            _logger = logger;
        }

        /// <summary>
        /// Gets consolidated comments from an APIView URL by filtering, grouping, and consolidating comments
        /// </summary>
        public async Task<List<ConsolidatedComment>> GetConsolidatedComments(string apiViewUrl)
        {
            // Parse the URL to get revisionId
            var (revisionId, reviewId) = ApiViewUrlParser.ExtractIds(apiViewUrl);
            var environment = IAPIViewHttpService.DetectEnvironmentFromUrl(apiViewUrl);
            
            _logger.LogInformation("Getting comments for revision {RevisionId} in review {ReviewId}", revisionId, reviewId);
            
            // Get comments from APIViewService - returns JSON string
            var commentsJson = await _apiViewService.GetCommentsByRevisionAsync(revisionId, environment);
            
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

            var prompt = $@"Read the following discussion thread from an API review and provide a summarized comment of the discussion.

Discussion:
{discussion}

Respond in JSON format:
{{
  ""comment"": ""a description of the final decision or action""
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
            var jsonResponse = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText);
            
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
        /// Gets metadata (language, packageName, etc.) for an APIView URL
        /// </summary>
        public async Task<ApiViewMetadata> GetMetadata(string apiViewUrl)
        {
            var environment = IAPIViewHttpService.DetectEnvironmentFromUrl(apiViewUrl);
            
            _logger.LogInformation("Getting metadata for APIView URL: {ApiViewUrl}", apiViewUrl);
            
            // Call the resolve endpoint to get metadata
            var metadataJson = await _apiViewService.ResolveApiViewUrlAsync(apiViewUrl, environment);
            
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
}
