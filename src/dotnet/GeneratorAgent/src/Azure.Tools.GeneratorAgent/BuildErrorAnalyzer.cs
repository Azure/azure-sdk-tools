using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for analyzing build errors and generating AI prompts for fixes.
    /// </summary>
    internal class BuildErrorAnalyzer
    {
        private readonly ILogger<BuildErrorAnalyzer> Logger;
        private static readonly object InitializationLock = new object();
        private static bool IsInitialized = false;

        public BuildErrorAnalyzer(ILogger<BuildErrorAnalyzer> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
            
            InitializeErrorAnalyzers();
        }

        /// <summary>
        /// Initializes the ErrorAnalyzer providers. Thread-safe singleton initialization.
        /// </summary>
        private static void InitializeErrorAnalyzers()
        {
            if (IsInitialized) return;

            lock (InitializationLock)
            {
                if (IsInitialized) return;

                try
                {
                    ErrorAnalyzerService.RegisterProvider(new ClientAnalyzerProvider());
                    ErrorAnalyzerService.RegisterProvider(new GeneralAnalyzerProvider());
                    ErrorAnalyzerService.RegisterProvider(new ManagementAnalyzerProvider());
                    IsInitialized = true;
                }
                catch (Exception ex)
                {
                    // Log the error but don't throw - we can still continue without error analysis
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize ErrorAnalyzers: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Takes raw build output string and generates List&lt;RuleError&gt;
        /// </summary>
        public List<RuleError> ParseBuildOutput(string buildOutput)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(buildOutput);

            List<RuleError> errors = new List<RuleError>();
            
            // Regular expression to match error patterns like:
            // error AZC0012: Single word class names are too generic...
            string errorPattern = @"error\s+([A-Z]+\d+):\s*(.+?)(?=\s*\[|$)";
            Regex regex = new Regex(errorPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            MatchCollection matches = regex.Matches(buildOutput);
            Logger.LogDebug("Found {MatchCount} potential error matches", matches.Count);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string errorType = match.Groups[1].Value.Trim();
                    string errorMessage = match.Groups[2].Value.Trim();

                    // Skip if we couldn't extract meaningful data
                    if (string.IsNullOrWhiteSpace(errorType) || string.IsNullOrWhiteSpace(errorMessage))
                    {
                        continue;
                    }

                    try
                    {
                        RuleError ruleError = new RuleError(errorType, errorMessage);
                        errors.Add(ruleError);
                        Logger.LogDebug("Extracted error: {ErrorType} - {ErrorMessage}", errorType, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to create RuleError for type='{ErrorType}', message='{ErrorMessage}'", errorType, errorMessage);
                    }
                }
            }

            // Remove duplicates based on both type and message
            List<RuleError> uniqueErrors = errors
                .GroupBy(e => new { e.type, e.message })
                .Select(g => g.First())
                .ToList();

            Logger.LogDebug("Extracted {UniqueErrorCount} unique errors from {TotalErrorCount} total matches", uniqueErrors.Count, errors.Count);

            return uniqueErrors;
        }

        /// <summary>
        /// Takes List&lt;RuleError&gt; and calls ErrorAnalyzers.GetFixes method
        /// </summary>
        public IEnumerable<Fix> GetFixes(List<RuleError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            Logger.LogInformation("Generating fixes for {ErrorCount} errors", errors.Count);

            try
            {
                IEnumerable<Fix> fixes = ErrorAnalyzerService.GetFixes(errors);
                List<Fix> fixList = fixes.ToList();
                
                Logger.LogInformation("Generated {FixCount} fixes for {ErrorCount} errors", fixList.Count, errors.Count);
                
                return fixList;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate fixes for errors");
                return Enumerable.Empty<Fix>();
            }
        }
    }
}
