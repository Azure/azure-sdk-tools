using Azure.Tools.ErrorAnalyzers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class ErrorParser
    {
        private static readonly Regex ErrorRegex = new(@"error\s+([A-Z]+\d+):\s*(.+?)(?=\s*\[|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        
        private readonly AgentOrchestrator? AgentOrchestrator;
        private readonly ILogger<ErrorParser> Logger;

        public ErrorParser(AgentOrchestrator? agentOrchestrator, ILogger<ErrorParser> logger)
        {
            AgentOrchestrator = agentOrchestrator;
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        public virtual async Task<List<Fix>> AnalyzeErrorsAsync(Result<object> result, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Analyzing errors...");

            if (result.ProcessException?.Output == null)
            {
                Logger.LogWarning("No fixable error, skipping error analysis.");
                return new List<Fix>();
            }

            List<RuleError> errors = ParseWithRegex(result.ProcessException.Output);

            if (errors.Count == 0)
            {
                Logger.LogInformation("No errors found with regex. Falling back to AI-based parsing.");
                if (AgentOrchestrator != null)
                {
                    errors = await AgentOrchestrator.AnalyzeErrorsAsync(result.ProcessException.Output, cancellationToken);
                }
            }

            return GenerateFixes(errors).ToList();
        }

        private List<RuleError> ParseWithRegex(string output)
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
                IEnumerable<Fix> fixes = ErrorAnalyzerService.GetFixes(errors);
                Logger.LogInformation("Generated {FixCount} fixes for errors.", fixes.Count());
                return fixes;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate fixes for errors.");
                return Array.Empty<Fix>();
            }
        }
    }
}
