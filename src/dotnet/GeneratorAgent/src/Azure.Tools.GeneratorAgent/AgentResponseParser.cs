using System.Text.Json;
using Azure.Tools.GeneratorAgent.Models;
using Microsoft.Extensions.Logging;

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

                // Parse as JSON
                var response = JsonSerializer.Deserialize<AgentTypeSpecResponse>(rawResponse.Trim(), new JsonSerializerOptions
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
    }
}
