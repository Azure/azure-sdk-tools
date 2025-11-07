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
                var jsonContent = CleanJsonResponse(rawResponse);

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

                return response.Errors.Select(e => new RuleError(
                    string.IsNullOrWhiteSpace(e.Type) ? "UnspecifiedError" : e.Type, 
                    string.IsNullOrWhiteSpace(e.Message) ? "No message provided" : e.Message));
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Agent response is not in the expected JSON format", ex);
            }
        }


        public static PatchRequest ParsePatchRequest(string response)
        {
            var cleanPatchJson = CleanJsonResponse(response);
            return JsonSerializer.Deserialize<PatchRequest>(cleanPatchJson, JsonOptions)!;
        }

        /// <summary>
        /// Cleans JSON response by removing markdown code blocks if present or extracting JSON from mixed content
        /// </summary>
        private static string CleanJsonResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            var trimmedResponse = response.Trim();

            // Find first occurrence of ```json (case insensitive)
            var jsonStartMarker = "```json";
            var jsonStartIndex = trimmedResponse.IndexOf(jsonStartMarker, StringComparison.OrdinalIgnoreCase);

            if (jsonStartIndex >= 0)
            {
                // Move past the ```json marker and any newlines
                var jsonContentStart = jsonStartIndex + jsonStartMarker.Length;

                // Skip any whitespace/newlines after ```json
                while (jsonContentStart < trimmedResponse.Length &&
                    char.IsWhiteSpace(trimmedResponse[jsonContentStart]))
                {
                    jsonContentStart++;
                }

                // Find the closing ``` marker
                var jsonEndIndex = trimmedResponse.IndexOf("```", jsonContentStart);

                if (jsonEndIndex > jsonContentStart)
                {
                    // Extract just the JSON content
                    return trimmedResponse.Substring(jsonContentStart, jsonEndIndex - jsonContentStart).Trim();
                }
                else
                {
                    // No closing marker found, take everything after ```json
                    return trimmedResponse.Substring(jsonContentStart).Trim();
                }
            }

            // Fallback: handle generic code blocks that start with ```
            if (trimmedResponse.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                int startIndex = trimmedResponse.IndexOf('\n');
                if (startIndex != -1)
                {
                    startIndex++; // Skip the newline
                    int endIndex = trimmedResponse.LastIndexOf("```");
                    if (endIndex > startIndex)
                    {
                        return trimmedResponse.Substring(startIndex, endIndex - startIndex).Trim();
                    }
                }
            }

            // Look for JSON object in mixed content using progressive deserialization
            var openBraceIndex = trimmedResponse.IndexOf('{');
            if (openBraceIndex >= 0)
            {
                return ExtractJsonUsingProgressiveDeserialization(trimmedResponse, openBraceIndex);
            }

            // No JSON found, return original response
            return trimmedResponse;
        }

        /// <summary>
        /// Extracts JSON from mixed content by attempting deserialization on progressively larger substrings
        /// </summary>
        private static string ExtractJsonUsingProgressiveDeserialization(string content, int startIndex)
        {
            // Try progressively larger substrings starting from the first brace
            for (int endIndex = startIndex + 1; endIndex <= content.Length; endIndex++)
            {
                try
                {
                    var candidate = content.Substring(startIndex, endIndex - startIndex);
                    
                    // Quick check: must end with closing brace to be complete JSON object
                    if (!candidate.TrimEnd().EndsWith('}'))
                        continue;

                    // Attempt to parse as JsonDocument to validate structure
                    using var document = JsonDocument.Parse(candidate, new JsonDocumentOptions 
                    { 
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                    
                    // If we get here, it's valid JSON - return it
                    return candidate;
                }
                catch (JsonException)
                {
                    // Not valid JSON yet, continue trying larger substrings
                    continue;
                }
            }

            // Fallback: if no valid JSON found, return everything from first brace to end
            return content.Substring(startIndex);
        }
    }
}
