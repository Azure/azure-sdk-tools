using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Agent;
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
        
        private static readonly List<Fix> EmptyFixList = new(0);
        
        private readonly ToolBasedAgent ToolBasedAgent;
        private readonly ILogger<ErrorAnalysisService> Logger;

        public ErrorAnalysisService(ToolBasedAgent toolBasedAgent, ILogger<ErrorAnalysisService> logger)
        {
            ArgumentNullException.ThrowIfNull(toolBasedAgent);
            ArgumentNullException.ThrowIfNull(logger);
            ToolBasedAgent = toolBasedAgent;
            Logger = logger;
        }

        /// <summary>
        /// Analyzes both compile and build results to generate a unified list of fixes.
        /// Since compilation failure prevents build execution, only one result will contain errors.
        /// </summary>
        public async Task<Result<List<Fix>>> GenerateFixesFromResultsAsync(
            Result<object>? compileResult, 
            Result<object>? buildResult, 
            CancellationToken cancellationToken)
        {
            // Process compile result first (if compilation fails, build won't run)
            if (compileResult?.IsFailure == true && compileResult?.ProcessException?.Output != null)
            {
                var compileFixResult = await GenerateFixesFromResultAsync(compileResult, cancellationToken);
                return compileFixResult.IsSuccess 
                    ? Result<List<Fix>>.Success(compileFixResult.Value!.ToList())
                    : Result<List<Fix>>.Failure(compileFixResult.Exception!);
            }

            // Process build result (only if compile succeeded)
            if (buildResult?.IsFailure == true && buildResult?.ProcessException?.Output != null)
            {
                var buildFixResult = await GenerateFixesFromResultAsync(buildResult, cancellationToken);
                return buildFixResult.IsSuccess 
                    ? Result<List<Fix>>.Success(buildFixResult.Value!.ToList())
                    : Result<List<Fix>>.Failure(buildFixResult.Exception!);
            }

            // No errors found in either result
            return Result<List<Fix>>.Success(EmptyFixList);
        }

        /// <summary>
        /// Analyzes errors from a single result (compile or build) using regex parsing with AI fallback for unparsed content.
        /// </summary>
        private async Task<Result<IEnumerable<Fix>>> GenerateFixesFromResultAsync(Result<object> result, CancellationToken cancellationToken)
        {
            var originalOutput = result.ProcessException?.Output ?? string.Empty;
            var allErrors = new List<RuleError>();

            // Step 1: Parse with regex first and get unparsed content
            var (regexErrors, unparsedContent) = ParseWithRegex(originalOutput);
            var regexErrorsList = regexErrors.ToList();
            allErrors.AddRange(regexErrorsList);
            
            // Step 2: If we have unparsed content, analyze with AI
            if (!string.IsNullOrWhiteSpace(unparsedContent))
            {
                try
                {
                    var aiAnalysisResult = await ToolBasedAgent.AnalyzeErrorsAsync(unparsedContent, cancellationToken).ConfigureAwait(false);
                    if (aiAnalysisResult.IsSuccess && aiAnalysisResult.Value != null)
                    {
                        var aiErrors = aiAnalysisResult.Value.ToList();
                        allErrors.AddRange(aiErrors);
                    }
                    else
                    {
                        Logger.LogWarning("AI analysis failed: {Error}", aiAnalysisResult.Exception?.Message);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Exception during AI error analysis");
                }
            }

            Logger.LogDebug("Total errors found: {TotalErrorCount} (Regex: {RegexCount}, AI: {AICount})", 
                allErrors.Count, regexErrorsList.Count, allErrors.Count - regexErrorsList.Count);

            return TransformErrorsToFixes(allErrors);
        }

        /// <summary>
        /// Parses error output using regex patterns to extract structured error information.
        /// Returns both the parsed errors and the remaining unparsed content in a single pass.
        /// </summary>
        private (IEnumerable<RuleError> errors, string unparsedContent) ParseWithRegex(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                Logger.LogWarning("No output provided for regex parsing.");
                return (new List<RuleError>(), string.Empty);
            }

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
        private Result<IEnumerable<Fix>> TransformErrorsToFixes(IEnumerable<RuleError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var errorList = errors.ToList();
            var fixes = ErrorAnalyzerService.GetFixes(errorList);
            var materializedFixes = fixes.ToList();
            
            return Result<IEnumerable<Fix>>.Success(materializedFixes);
        }
    }
}