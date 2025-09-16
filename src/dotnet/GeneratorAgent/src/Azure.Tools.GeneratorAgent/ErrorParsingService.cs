using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Agent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class ErrorParsingService
    {
        private static readonly Regex ErrorRegex = new(@"error\s+([A-Z]+\d+):\s*([^\[]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        
        private readonly ErrorFixerAgent? ErrorFixerAgent;
        private readonly ILogger<ErrorParsingService> Logger;

        public ErrorParsingService(ErrorFixerAgent? errorFixerAgent, ILogger<ErrorParsingService> logger)
        {
            ErrorFixerAgent = errorFixerAgent;
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        public virtual async Task<Result<IEnumerable<Fix>>> AnalyzeErrorsAsync(Result<object> result, CancellationToken cancellationToken)
        {
            if (result.ProcessException?.Output == null)
            {
                Logger.LogWarning("No fixable error, skipping error analysis.");
                return Result<IEnumerable<Fix>>.Success(Enumerable.Empty<Fix>());
            }

            Logger.LogDebug("Build error output:\n{ErrorOutput}", result.ProcessException.Output);

            var errors = ParseWithRegex(result.ProcessException.Output);

            if (!errors.Any())
            {
                Logger.LogDebug("No errors found with regex. Falling back to AI-based parsing.");
                if (ErrorFixerAgent != null)
                {
                    var aiAnalysisResult = await ErrorFixerAgent.AnalyzeErrorsAsync(result.ProcessException.Output, cancellationToken).ConfigureAwait(false);
                    if (aiAnalysisResult.IsFailure)
                    {
                        return Result<IEnumerable<Fix>>.Failure(aiAnalysisResult.Exception ?? new InvalidOperationException("AI analysis failed"));
                    }
                    errors = aiAnalysisResult.Value!;
                }
            }

            var fixesResult = GenerateFixes(errors);
            return fixesResult;
        }

        private IEnumerable<RuleError> ParseWithRegex(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                Logger.LogWarning("No output provided for regex parsing.");
                return new List<RuleError>();
            }

            MatchCollection matches = ErrorRegex.Matches(output);

            // Use HashSet to deduplicate exact (type, message) combinations
            HashSet<(string type, string message)> seenErrors = new();
            List<RuleError> errors = new();

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string errorType = match.Groups[1].Value.Trim();
                    string errorMessage = match.Groups[2].Value.Trim();

                    if (Logger.IsEnabled(LogLevel.Debug))
                    { 
                    Logger.LogDebug("Raw regex match: Type='{ErrorType}', Message='{ErrorMessage}'", errorType, errorMessage);
                    }

                    if (string.IsNullOrWhiteSpace(errorType) || string.IsNullOrWhiteSpace(errorMessage))
                    {
                        Logger.LogDebug("Skipping empty error type or message");
                        continue;
                    }

                    // Deduplicate exact (type, message) combinations
                    if (seenErrors.Add((errorType, errorMessage)))
                    {
                        errors.Add(new RuleError(errorType, errorMessage));
                    }
                }
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Parsed {UniqueErrorCount} unique errors from {TotalMatches} regex matches", errors.Count, matches.Count);
            }
            return errors;
        }

        private Result<IEnumerable<Fix>> GenerateFixes(IEnumerable<RuleError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var errorList = errors.ToList();

            var fixes = ErrorAnalyzerService.GetFixes(errorList);
            var materializedFixes = fixes.ToList();
            
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Generated {FixCount} fixes:", materializedFixes.Count);
                foreach (var fix in materializedFixes)
                {
                    Logger.LogDebug("  - {FixType}: {Action}", fix.GetType().Name, fix.Action);
                }
            }
            
            return Result<IEnumerable<Fix>>.Success(materializedFixes);
        }
    }
}
