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
        public async Task<Result<List<Fix>>> AnalyzeAndGetFixesAsync(Result<object>? compileResult, Result<object>? buildResult, CancellationToken cancellationToken)
        {
            var fixTasks = new List<Task<Result<IEnumerable<Fix>>>>();

            if (compileResult?.IsFailure == true && compileResult?.ProcessException?.Output != null)
            {
                fixTasks.Add(ErrorParsingService.AnalyzeErrorsAsync(compileResult, cancellationToken));
            }

            if (buildResult?.IsFailure == true && buildResult?.ProcessException?.Output != null)
            {
                fixTasks.Add(ErrorParsingService.AnalyzeErrorsAsync(buildResult, cancellationToken));
            }

            if (fixTasks.Count == 0)
            {
                Logger.LogDebug("No errors to analyze");

                return Result<List<Fix>>.Success(new List<Fix>());
            }

            // Process all tasks and handle failures
            var allFixResults = await Task.WhenAll(fixTasks).ConfigureAwait(false);

            // Check for any failures
            var failures = allFixResults.Where(r => r.IsFailure);
            var firstFailure = failures.FirstOrDefault();
            if (firstFailure != null)
            {
                return Result<List<Fix>>.Failure(firstFailure.Exception ?? new InvalidOperationException("Error analysis failed"));
            }
            
            var finalFixes = allFixResults
                .Where(r => r.IsSuccess)
                .SelectMany(r => r.Value!)
                .ToList();
            
            return Result<List<Fix>>.Success(finalFixes);
        }
    }
}
