using System.Text.Json;
using Azure.Tools.GeneratorAgent.Models;
using Microsoft.Extensions.Logging;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for parsing and processing AI agent responses in plain text JSON format
    /// </summary>
    internal class AgentResponseParser
    {
        private readonly ILogger<AgentResponseParser> Logger;

        public AgentResponseParser(ILogger<AgentResponseParser> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        /// <summary>
        /// Parses the agent's response and extracts the updated client.tsp content from JSON
        /// </summary>
        public AgentTypeSpecResponse ParseResponse(string rawResponse)
        {
            Logger.LogInformation("Raw agent response: {Response}", rawResponse);
            
            try
            {
                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    throw new InvalidOperationException("Agent response is empty or null");
                }

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

                Logger.LogInformation("Successfully parsed JSON response with {Length} characters of client.tsp content", 
                    response.Content.Length);
                
                return response;
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Failed to parse agent response as JSON. Response: {Response}", rawResponse);
                throw new InvalidOperationException("Agent response is not in the expected JSON format", ex);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.LogError(ex, "Unexpected error while parsing agent response. Response length: {Length}", 
                    rawResponse?.Length ?? 0);
                throw;
            }
        }

        /// <summary>
        /// Parses the agent's error responses into a list of RuleError objects
        /// </summary>
        public List<RuleError> ParseErrors(string rawResponse)
        {
            Logger.LogInformation("Parsing errors from raw agent response.");

            try
            {
                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    throw new InvalidOperationException("Agent response is empty or null");
                }

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
                var ruleErrors = response.Errors.Select(e => new RuleError(e.Type, e.Message)).ToList();

                Logger.LogInformation("Successfully parsed {Count} errors from agent response.", ruleErrors.Count);
                return ruleErrors;
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "Failed to parse agent response as JSON. Response: {Response}", rawResponse);
                throw new InvalidOperationException("Agent response is not in the expected JSON format", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error while parsing agent response. Response length: {Length}", rawResponse?.Length ?? 0);
                throw;
            }
        }

        /// <summary>
        /// Extracts JSON content from agent response, handling markdown code blocks if present
        /// </summary>
        private string ExtractJsonFromResponse(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return rawResponse;
            }

            var trimmed = rawResponse.Trim();
            
            // Check if response is wrapped in markdown code blocks
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                // Find the start of JSON (after ```json)
                var startIndex = trimmed.IndexOf('\n');
                if (startIndex == -1)
                {
                    Logger.LogWarning("Malformed markdown code block: no newline after ```json");
                    return trimmed;
                }

                // Find the end of JSON (before closing ```)
                var endIndex = trimmed.LastIndexOf("```");
                if (endIndex == -1 || endIndex <= startIndex)
                {
                    Logger.LogWarning("Malformed markdown code block: no closing ```");
                    return trimmed;
                }

                var jsonContent = trimmed.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
                Logger.LogDebug("Extracted JSON from markdown code block. Length: {Length}", jsonContent.Length);
                return jsonContent;
            }
            
            // Check if response starts with ``` (without json specifier)
            if (trimmed.StartsWith("```"))
            {
                var startIndex = trimmed.IndexOf('\n');
                if (startIndex != -1)
                {
                    var endIndex = trimmed.LastIndexOf("```");
                    if (endIndex > startIndex)
                    {
                        var jsonContent = trimmed.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
                        Logger.LogDebug("Extracted content from generic markdown code block. Length: {Length}", jsonContent.Length);
                        return jsonContent;
                    }
                }
            }

            // Return as-is if no markdown code blocks detected
            return trimmed;
        }
    }
}
