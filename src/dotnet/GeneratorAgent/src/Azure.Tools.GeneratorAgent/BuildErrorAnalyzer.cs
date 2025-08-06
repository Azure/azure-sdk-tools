using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers;
using Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers;
using Azure.Tools.GeneratorAgent.Exceptions;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for analyzing build errors and generating AI prompts for fixes.
    /// </summary>
    internal class BuildErrorAnalyzer
    {
        // Regular expression to match error patterns.
        private static readonly Regex ErrorRegex = new(@"error\s+([A-Z]+\d+):\s*(.+?)(?=\s*\[|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        
        private readonly ILogger<BuildErrorAnalyzer> Logger;

        /// <summary>
        /// Static constructor to initialize ErrorAnalyzer providers.
        /// </summary>
        static BuildErrorAnalyzer()
        {
            ErrorAnalyzerService.RegisterProvider(new ClientAnalyzerProvider());
            ErrorAnalyzerService.RegisterProvider(new GeneralAnalyzerProvider());
            ErrorAnalyzerService.RegisterProvider(new ManagementAnalyzerProvider());
        }

        public BuildErrorAnalyzer(ILogger<BuildErrorAnalyzer> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        /// <summary>
        /// Analyzes both compile and build results to generate a unified list of fixes.
        /// </summary>
        public List<Fix> AnalyzeAndGetFixes(Result<object>? compileResult, Result<object>? buildResult)
        {
            List<Fix> allFixes = new List<Fix>();

            // Process TypeSpec compilation errors
            if (compileResult?.IsFailure == true && compileResult.ProcessException is TypeSpecCompilationException typeSpecEx)
            {
                Logger.LogInformation("Analyzing TypeSpec compilation errors for command: {Command}", typeSpecEx.Command);
                try
                {
                    List<RuleError> typeSpecErrors = ParseBuildOutput(typeSpecEx);
                    IEnumerable<Fix> typeSpecFixes = GetFixes(typeSpecErrors);
                    allFixes.AddRange(typeSpecFixes);
                    Logger.LogInformation("Generated fixes for TypeSpec compilation errors");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to analyze TypeSpec compilation errors");
                }
            }

            // Process .NET build errors  
            if (buildResult?.IsFailure == true && buildResult.ProcessException is DotNetBuildException dotNetEx)
            {
                Logger.LogInformation("Analyzing .NET build errors for command: {Command}", dotNetEx.Command);
                try
                {
                    List<RuleError> buildErrors = ParseBuildOutput(dotNetEx);
                    IEnumerable<Fix> buildFixes = GetFixes(buildErrors);
                    allFixes.AddRange(buildFixes);
                    Logger.LogInformation("Generated fixes for .NET build errors");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to analyze .NET build errors");
                }
            }

            Logger.LogInformation("Total fixes generated: {TotalFixCount}", allFixes.Count);
            return allFixes;
        }

        /// <summary>
        /// Takes ProcessExecutionException and generates List&lt;RuleError&gt;
        /// </summary>
        public List<RuleError> ParseBuildOutput(ProcessExecutionException processException)
        {
            ArgumentNullException.ThrowIfNull(processException);

            // Combine both output and error streams for comprehensive analysis
            string combinedOutput = $"{processException.Output}\n{processException.Error}".Trim();

            if (string.IsNullOrWhiteSpace(combinedOutput))
            {
                Logger.LogWarning("No output or error content available for analysis from command: {Command}", processException.Command);
                return new List<RuleError>();
            }

            List<RuleError> errors = new List<RuleError>();
            
            MatchCollection matches = ErrorRegex.Matches(combinedOutput);
            Logger.LogDebug("Found {MatchCount} potential error matches using pattern", matches.Count);
            
            // TODO: Implement LLM-based error message parsing as a fallback

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
            return errors
                .GroupBy(e => new { e.type, e.message })
                .Select(g => g.First())
                .ToList();
        }
        
        /// <summary>
        /// Takes List&lt;RuleError&gt; and calls ErrorAnalyzers.GetFixes method
        /// </summary>
        public IEnumerable<Fix> GetFixes(List<RuleError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            try
            {
                IEnumerable<Fix> fixes = ErrorAnalyzerService.GetFixes(errors);
                Logger.LogInformation("Generated fixes for {ErrorCount} errors", errors.Count);
                return fixes;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to generate fixes for errors");
                return Enumerable.Empty<Fix>();
            }
        }
    }
}
