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
                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    throw new InvalidOperationException("Agent response is Null or empty");
                }
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
            catch (Exception ex)
            {
                throw new InvalidOperationException("Agent response is not in the expected JSON format", ex);
            }
        }


        public static PatchRequest ParsePatchRequest(string response)
        {
            try
            {
                var cleanPatchJson = CleanJsonResponse(response);

                var patchRequest = JsonSerializer.Deserialize<PatchRequest>(cleanPatchJson, JsonOptions);
                if (patchRequest == null)
                {
                    throw new InvalidOperationException("Failed to deserialize patch request - result was null");
                }
                return patchRequest;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse patch JSON: {ex.Message}", ex);
            }
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

            // Look for JSON object in mixed content (plain text + JSON)
            var openBraceIndex = trimmedResponse.IndexOf('{');
            if (openBraceIndex >= 0)
            {
                // Find the matching closing brace
                int braceCount = 0;
                int closeBraceIndex = -1;
                
                for (int i = openBraceIndex; i < trimmedResponse.Length; i++)
                {
                    if (trimmedResponse[i] == '{')
                        braceCount++;
                    else if (trimmedResponse[i] == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            closeBraceIndex = i;
                            break;
                        }
                    }
                }

                if (closeBraceIndex > openBraceIndex)
                {
                    return trimmedResponse.Substring(openBraceIndex, closeBraceIndex - openBraceIndex + 1);
                }
            }

            // No JSON found, return original response
            return trimmedResponse;
        }
    }
}
