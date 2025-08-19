using System.Text.RegularExpressions;
using Azure.Tools.GeneratorAgent.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for parsing and processing AI agent responses from TypeSpec code blocks
    /// </summary>
    internal class AgentResponseParser
    {
        private readonly ILogger<AgentResponseParser> Logger;
        private static readonly Regex TypeSpecCodeBlockRegex = new(@"```typespec\s*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TspCodeBlockRegex = new(@"```tsp\s*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TypeScriptCodeBlockRegex = new(@"```typescript\s*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GenericCodeBlockRegex = new(@"```\s*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public AgentResponseParser(ILogger<AgentResponseParser> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        /// <summary>
        /// Parses the agent's response and extracts the updated client.tsp content from TypeSpec code blocks
        /// </summary>
        public AgentResponse ParseResponse(string rawResponse)
        {
            try
            {
                string updatedContent = ExtractTypeSpecCodeBlock(rawResponse);
                
                Logger.LogInformation("Successfully parsed agent response with {Length} characters of client.tsp content", 
                    updatedContent.Length);
                
                return new AgentResponse
                {
                    UpdatedFileContent = updatedContent.Trim(),
                    HasValidContent = true
                };
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.LogError(ex, "Unexpected error while parsing agent response. Response length: {Length}", 
                    rawResponse?.Length ?? 0);
                throw;
            }
        }

        /// <summary>
        /// Extracts TypeSpec content from code blocks in the response
        /// </summary>
        private string ExtractTypeSpecCodeBlock(string response)
        {
            try
            {
                // Try pre-compiled regex patterns in order of preference
                (Regex, string)[] regexPatterns = new[]
                {
                    (TypeSpecCodeBlockRegex, "typespec"),        // ```typespec ... ``` (primary - correct format)
                    (TspCodeBlockRegex, "tsp"),                  // ```tsp ... ``` (file extension)
                    (TypeScriptCodeBlockRegex, "typescript"),    // ```typescript ... ``` (legacy fallback)
                    (GenericCodeBlockRegex, "generic")           // ``` ... ``` (no language)
                };

                foreach ((Regex regex, string patternName) in regexPatterns)
                {
                    MatchCollection matches = regex.Matches(response);
                    
                    if (matches.Count > 0)
                    {
                        Match lastMatch = matches[matches.Count - 1];
                        string extractedContent = lastMatch.Groups[1].Value.Trim();
                        
                        if (!string.IsNullOrWhiteSpace(extractedContent))
                        {
                            Logger.LogDebug("Successfully extracted content using {PatternName} pattern", patternName);
                            return extractedContent.Trim();
                        }
                    }
                }

                Logger.LogWarning("No code blocks found in response using any pattern");
                throw new InvalidOperationException("No valid TypeSpec code blocks found in agent response");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.LogError(ex, "Error extracting TypeSpec code block from response");
                throw;
            }
        }
    }
}
