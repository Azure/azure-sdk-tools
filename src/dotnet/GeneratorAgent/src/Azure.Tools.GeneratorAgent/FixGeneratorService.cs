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
            // Build enumerable chain without intermediate collections - optimized approach
            var fixTasks = new List<Task<IEnumerable<Fix>>>();

            if (compileResult?.IsFailure == true)
            {
                fixTasks.Add(ErrorParsingService.AnalyzeErrorsAsync(compileResult, cancellationToken));
            }

            if (buildResult?.IsFailure == true)
            {
                fixTasks.Add(ErrorParsingService.AnalyzeErrorsAsync(buildResult, cancellationToken));
            }

            // Process all tasks and flatten results efficiently
            var allFixResults = await Task.WhenAll(fixTasks).ConfigureAwait(false);
            var finalFixes = allFixResults
                .SelectMany(fixes => fixes)  // Flatten enumerable chain
                .ToList();
            
            Logger.LogInformation("Total fixes generated: {TotalFixCount}", finalFixes.Count);
            return finalFixes;
        }
    }
}
