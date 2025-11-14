using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for analyzing build errors and generating fixes by combining regex parsing with AI fallback.
    /// Merges the functionality of the former ErrorParsingService and FixGeneratorService classes.
    /// </summary>
    internal class ErrorAnalysisService
    {
        private static readonly Regex ErrorRegex = new(@"error\s+([A-Z]+\d+):\s*([^\[]+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private readonly OpenAIService OpenAIService;
        private readonly ILogger<ErrorAnalysisService> Logger;

        public ErrorAnalysisService(OpenAIService aiService, ILogger<ErrorAnalysisService> logger)
        {
            ArgumentNullException.ThrowIfNull(aiService);
            ArgumentNullException.ThrowIfNull(logger);
            OpenAIService = aiService;
            Logger = logger;
        }

        /// <summary>
        /// Analyzes errors from a compilation/build result using regex parsing with AI fallback for unparsed content.
        /// Note: This method assumes the result contains a ProcessException (recoverable build/compilation errors).
        /// </summary>
        public async Task<List<Fix>> GenerateFixesFromFailureLogsAsync(string errorOutput, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(errorOutput))
            {
               return new List<Fix>();
            }

            Logger.LogDebug("Generating fixes from failure logs");
            
            var allErrors = new List<RuleError>();

            // Step 1: Parse with regex first and get unparsed content
            var (regexErrors, unparsedContent) = ParseWithRegex(errorOutput);

            var regexErrorCount = 0;
            foreach (var error in regexErrors)
            {
                allErrors.Add(error);
                regexErrorCount++;
            }

            // Step 2: Use AI service to analyze unparsed content
            if (!string.IsNullOrWhiteSpace(unparsedContent))
            {
                var aiErrors = await OpenAIService.AnalyzeErrorsAsync(unparsedContent, cancellationToken);
                allErrors.AddRange(aiErrors);
            }

            Logger.LogDebug("Total errors found: {TotalErrorCount} (Regex: {RegexCount}, AI: {AICount})",
                allErrors.Count, regexErrorCount, allErrors.Count - regexErrorCount);

            return TransformErrorsToFixes(allErrors).ToList();
        }

        /// <summary>
        /// Parses error output using regex patterns to extract structured error information.
        /// Returns both the parsed errors and the remaining unparsed content in a single pass.
        /// </summary>
        private (IEnumerable<RuleError> errors, string unparsedContent) ParseWithRegex(string output)
        {
            var lines = output.Split('\n', StringSplitOptions.None);

            HashSet<(string type, string message)> seenErrors = new();
            List<RuleError> errors = new();
            List<string> unparsedLines = new();

            // Process each line for regex matches and collect unmatched lines
            foreach (var line in lines)
            {
                var match = ErrorRegex.Match(line);
                bool lineMatched = false;

                if (match.Success && match.Groups.Count >= 3)
                {
                    string errorType = match.Groups[1].Value.Trim();
                    string errorMessage = match.Groups[2].Value.Trim();

                    if (!string.IsNullOrWhiteSpace(errorType) && !string.IsNullOrWhiteSpace(errorMessage))
                    {
                        if (seenErrors.Add((errorType, errorMessage)))
                        {
                            errors.Add(new RuleError(errorType, errorMessage));

                            if (Logger.IsEnabled(LogLevel.Debug))
                            {
                                Logger.LogDebug("Raw regex match: Type='{ErrorType}', Message='{ErrorMessage}'", errorType, errorMessage);
                            }
                        }
                        lineMatched = true; // Mark as matched regardless of whether it's a duplicate
                    }
                }

                // If the line didn't match regex, add it to unparsed content
                if (!lineMatched)
                {
                    unparsedLines.Add(line);
                }
            }

            var unparsedContent = string.Join('\n', unparsedLines).Trim();

            return (errors, unparsedContent);
        }

        /// <summary>
        /// Generates fixes from parsed error information using the ErrorAnalyzerService.
        /// </summary>
        private IEnumerable<Fix> TransformErrorsToFixes(IEnumerable<RuleError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            return  ErrorAnalyzerService.GetFixes(errors) ?? Enumerable.Empty<Fix>();
        }
    }
}
