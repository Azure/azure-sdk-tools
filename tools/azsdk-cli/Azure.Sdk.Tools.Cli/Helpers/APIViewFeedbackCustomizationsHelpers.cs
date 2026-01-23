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
    /// Represents a consolidated comment from a discussion thread
    /// </summary>
    public class ConsolidatedComment
    {
        public string ThreadId { get; set; } = string.Empty;
        public int LineNo { get; set; }
        public string LineText { get; set; } = string.Empty;
        public string Conclusion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Helper interface for APIView feedback customizations operations
    /// </summary>
    public interface IAPIViewFeedbackCustomizationsHelpers
    {
        Task<List<ConsolidatedComment>> GetConsolidatedComments(string apiViewUrl);
    }

    /// <summary>
    /// Represents a raw comment from APIView API
    /// </summary>
    internal class ApiViewComment
    {
        [JsonPropertyName("lineNo")]
        public int LineNo { get; set; }
        
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
                _logger.LogInformation("No comments found for revision {RevisionId}", revisionId);
                return new List<ConsolidatedComment>();
            }

            // Deserialize comments
            var comments = JsonSerializer.Deserialize<List<ApiViewComment>>(commentsJson);
            
            if (comments == null || !comments.Any())
            {
                _logger.LogInformation("No comments to process for revision {RevisionId}", revisionId);
                return new List<ConsolidatedComment>();
            }

            // 1. Filter out resolved and Question severity comments
            var filteredComments = comments
                .Where(c => !c.IsResolved && c.Severity != "Question")
                .ToList();

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
                    "Consolidated discussion - ThreadId: {ThreadId}, LineNo: {LineNo}, Conclusion: {Conclusion}",
                    consolidated.ThreadId,
                    consolidated.LineNo,
                    consolidated.Conclusion);
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

            // Build discussion text from grouped comments
            var discussion = string.Join("\n\n", groupedComments.Select((c, idx) => 
                $"Comment {idx + 1} (by {c.CreatedBy} at {c.CreatedOn}):\n{c.CommentText}"));

            var prompt = $@"Read the following discussion thread from an API review and provide a summarized comment of the discussion with the conclusion.

Discussion:
{discussion}

Respond in JSON format:
{{
  ""conclusion"": ""a description of the final decision or action""
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
                Conclusion = jsonResponse?["conclusion"]?.ToString() ?? string.Empty
            };
        }
    }
}
