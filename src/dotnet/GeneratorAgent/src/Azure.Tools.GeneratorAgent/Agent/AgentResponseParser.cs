using System.Text.Json;
using Azure.Tools.GeneratorAgent.Models;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.GeneratorAgent.Agent
{
    /// <summary>
    /// Static helper methods for parsing AI agent responses in plain text JSON format
    /// </summary>
    internal static class AgentResponseParser
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Parses the agent's error responses into a list of RuleError objects
        /// </summary>
        public static IEnumerable<RuleError> ParseErrors(string rawResponse)
        {
            try
            {
                // Extract JSON from markdown code blocks if present
                var jsonContent = ExtractJsonFromResponse(rawResponse);

                // Deserialize using shared static options (avoids allocation per call)
                var response = JsonSerializer.Deserialize<AgentErrorResponse>(jsonContent, JsonOptions);

                if (response == null)
                {
                    throw new InvalidOperationException("Failed to deserialize agent response");
                }

                // Return empty collection if no errors found - this is valid behavior
                if (response.Errors == null || !response.Errors.Any())
                {
                    return Enumerable.Empty<RuleError>();
                }

                return response.Errors.Select(e => new RuleError(e.Type, e.Message));
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Agent response is not in the expected JSON format", ex);
            }
        }

        /// <summary>
        /// Extracts JSON content from agent response, handling markdown code blocks if present
        /// </summary>
        private static string ExtractJsonFromResponse(string rawResponse)
        {
            var trimmed = rawResponse.Trim();
            
            // Check if response is wrapped in markdown code blocks
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractFromMarkdownBlock(trimmed, "```json");
            }
            
            // Check if response starts with ``` (without json specifier)
            if (trimmed.StartsWith("```"))
            {
                return ExtractFromMarkdownBlock(trimmed, "```");
            }

            // Return as-is if no markdown code blocks detected
            return trimmed;
        }

        private static string ExtractFromMarkdownBlock(string trimmed, string startMarker)
        {
            // Find where the actual JSON content starts after the marker
            var contentStart = startMarker.Length;
            
            // Skip any whitespace (spaces, newlines, tabs) after the start marker
            while (contentStart < trimmed.Length && char.IsWhiteSpace(trimmed[contentStart]))
            {
                contentStart++;
            }
            
            // If we've consumed the entire string, return empty
            if (contentStart >= trimmed.Length)
            {
                return string.Empty;
            }

            // Find the end of content (before closing ```)
            var endIndex = trimmed.LastIndexOf("```");
            
            // If no closing backticks found or they're at the start, use everything after contentStart
            if (endIndex == -1 || endIndex <= contentStart)
            {
                return trimmed.Substring(contentStart).Trim();
            }

            // Extract content between contentStart and closing backticks
            var length = endIndex - contentStart;
            if (length <= 0)
            {
                return trimmed.Substring(contentStart).Trim();
            }

            return trimmed.Substring(contentStart, length).Trim();
        }
    }
}
