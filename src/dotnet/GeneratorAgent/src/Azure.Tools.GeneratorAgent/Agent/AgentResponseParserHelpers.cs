using System.Text.Json;
using Azure.Tools.GeneratorAgent.Models;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.GeneratorAgent.Agent
{
    /// <summary>
    /// Static helper methods for parsing AI agent responses in plain text JSON format
    /// </summary>
    internal static class AgentResponseParserHelpers
    {
        /// <summary>
        /// Parses the agent's response and extracts the updated client.tsp content from JSON
        /// </summary>
        public static AgentTypeSpecResponse ParseResponse(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                throw new ArgumentException("Agent response is empty or null", nameof(rawResponse));
            }

            try
            {
                // Extract JSON from markdown code blocks if present
                var jsonContent = ExtractJsonFromResponse(rawResponse);

                // Parse as JSON
                var response = JsonSerializer.Deserialize<AgentTypeSpecResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });

                if (response == null)
                {
                    throw new InvalidOperationException("Failed to deserialize agent response");
                }

                if (response.Path != "client.tsp")
                {
                    throw new InvalidOperationException($"Expected path 'client.tsp' but got '{response.Path}'");
                }

                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    throw new InvalidOperationException("Agent response contains empty content");
                }

                return response;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Agent response is not in the expected JSON format", ex);
            }
        }

        /// <summary>
        /// Parses the agent's error responses into a list of RuleError objects
        /// </summary>
        public static List<RuleError> ParseErrors(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                throw new ArgumentException("Agent response is empty or null", nameof(rawResponse));
            }

            try
            {
                // Extract JSON from markdown code blocks if present
                var jsonContent = ExtractJsonFromResponse(rawResponse);

                // Deserialize the response
                var response = JsonSerializer.Deserialize<AgentErrorResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });

                if (response == null || response.Errors == null || !response.Errors.Any())
                {
                    throw new InvalidOperationException("No errors found in the agent response");
                }

                // Map errors to RuleError objects
                return response.Errors.Select(e => new RuleError(e.Type, e.Message)).ToList();
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
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return rawResponse;
            }

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
            // Find the start of content (after start marker)
            var startIndex = trimmed.IndexOf('\n');
            if (startIndex == -1)
            {
                throw new InvalidOperationException($"Malformed markdown code block: no newline after {startMarker}");
            }

            // Find the end of content (before closing ```)
            var endIndex = trimmed.LastIndexOf("```");
            if (endIndex == -1 || endIndex <= startIndex)
            {
                throw new InvalidOperationException("Malformed markdown code block: no closing ```");
            }

            int length = endIndex - startIndex - 1;
            if (length <= 0)
            {
                throw new InvalidOperationException($"Malformed markdown code block: calculated content length is not positive (startIndex: {startIndex}, endIndex: {endIndex})");
            }

            return trimmed.Substring(startIndex + 1, length).Trim();
        }
    }
}
