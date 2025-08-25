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
        private readonly ILogger<BuildErrorAnalyzer> Logger;
        private readonly ErrorParser ErrorParser;

        /// <summary>
        /// Static constructor to initialize ErrorAnalyzer providers.
        /// </summary>
        static BuildErrorAnalyzer()
        {
            ErrorAnalyzerService.RegisterProvider(new ClientAnalyzerProvider());
            ErrorAnalyzerService.RegisterProvider(new GeneralAnalyzerProvider());
            ErrorAnalyzerService.RegisterProvider(new ManagementAnalyzerProvider());
        }

        public BuildErrorAnalyzer(ILogger<BuildErrorAnalyzer> logger, ErrorParser errorParser)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ErrorParser = errorParser ?? throw new ArgumentNullException(nameof(errorParser));
        }

        /// <summary>
        /// Analyzes both compile and build results to generate a unified list of fixes.
        /// </summary>
        public async Task<List<Fix>> AnalyzeAndGetFixesAsync(Result<object>? compileResult, Result<object>? buildResult, CancellationToken cancellationToken)
        {
            List<Fix> allFixes = new List<Fix>();

            if (compileResult?.IsFailure == true)
            {
                allFixes.AddRange(await ErrorParser.AnalyzeErrorsAsync(compileResult, cancellationToken));
            }

            if (buildResult?.IsFailure == true)
            {
                allFixes.AddRange(await ErrorParser.AnalyzeErrorsAsync(buildResult, cancellationToken));
            }

            Logger.LogInformation("Total fixes generated: {TotalFixCount}", allFixes.Count);
            return allFixes;
        }
    }
}
