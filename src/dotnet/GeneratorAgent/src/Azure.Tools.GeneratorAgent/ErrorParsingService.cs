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

        public virtual async Task<IEnumerable<Fix>> AnalyzeErrorsAsync(Result<object> result, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Analyzing errors...");

            if (result.ProcessException?.Output == null)
            {
                Logger.LogWarning("No fixable error, skipping error analysis.");
                return Enumerable.Empty<Fix>();
            }

            var errors = ParseWithRegex(result.ProcessException.Output);

            if (!errors.Any())
            {
                Logger.LogInformation("No errors found with regex. Falling back to AI-based parsing.");
                if (ErrorFixerAgent != null)
                {
                    errors = await ErrorFixerAgent.AnalyzeErrorsAsync(result.ProcessException.Output, cancellationToken).ConfigureAwait(false);
                }
            }

            return GenerateFixes(errors);
        }

        private IEnumerable<RuleError> ParseWithRegex(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                Logger.LogWarning("No output provided for regex parsing.");
                return new List<RuleError>();
            }

            MatchCollection matches = ErrorRegex.Matches(output);
            Logger.LogDebug("Found {MatchCount} potential error matches using regex.", matches.Count);

            HashSet<(string type, string message)> seenErrors = new();
            List<RuleError> errors = new(matches.Count);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string errorType = match.Groups[1].Value.Trim();
                    string errorMessage = match.Groups[2].Value.Trim();

                    if (string.IsNullOrWhiteSpace(errorType) || string.IsNullOrWhiteSpace(errorMessage))
                    {
                        continue;
                    }

                    if (!seenErrors.Add((errorType, errorMessage)))
                    {
                        continue;
                    }

                    errors.Add(new RuleError(errorType, errorMessage));
                    Logger.LogDebug("Extracted error: {ErrorType} - {ErrorMessage}", errorType, errorMessage);
                }
            }

            return errors;
        }

        private IEnumerable<Fix> GenerateFixes(IEnumerable<RuleError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            try
            {
                var fixes = ErrorAnalyzerService.GetFixes(errors);
                var materializedFixes = fixes.ToList();
                Logger.LogInformation("Generated {FixCount} fixes for errors.", materializedFixes.Count);
                return materializedFixes;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate fixes for errors.");
                return Enumerable.Empty<Fix>();
            }
        }
    }
}
