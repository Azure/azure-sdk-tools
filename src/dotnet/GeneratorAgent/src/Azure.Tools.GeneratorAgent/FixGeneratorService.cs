using Azure.Tools.ErrorAnalyzers;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for analyzing build errors and generating AI prompts for fixes.
    /// </summary>
    internal class FixGeneratorService
    {
        private readonly ILogger<FixGeneratorService> Logger;
        private readonly ErrorParsingService ErrorParsingService;

        public FixGeneratorService(ILogger<FixGeneratorService> logger, ErrorParsingService errorParsingService)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ErrorParsingService = errorParsingService ?? throw new ArgumentNullException(nameof(errorParsingService));
        }

        /// <summary>
        /// Analyzes both compile and build results to generate a unified list of fixes.
        /// </summary>
        public async Task<List<Fix>> AnalyzeAndGetFixesAsync(Result<object>? compileResult, Result<object>? buildResult, CancellationToken cancellationToken)
        {
            List<Fix> allFixes = new List<Fix>();

            if (compileResult?.IsFailure == true)
            {
                allFixes.AddRange(await ErrorParsingService.AnalyzeErrorsAsync(compileResult, cancellationToken));
            }

            if (buildResult?.IsFailure == true)
            {
                allFixes.AddRange(await ErrorParsingService.AnalyzeErrorsAsync(buildResult, cancellationToken));
            }

            Logger.LogInformation("Total fixes generated: {TotalFixCount}", allFixes.Count);
            return allFixes;
        }
    }
}
