using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent;

/// <summary>
/// Orchestrates the iterative TypeSpec generation and error-fixing workflow
/// </summary>
internal class GenerationOrchestrator
{
    private readonly AppSettings AppSettings;
    private readonly LocalLibraryGenerationService LibraryGenerationService;
    private readonly TypeSpecFileService TypeSpecFileService;
    private readonly OpenAIService AIService;
    private readonly BuildWorkflow BuildWorkflow;
    private readonly ErrorAnalysisService ErrorAnalysisService;
    private readonly TypeSpecPatchApplicator PatchApplicator;
    private readonly ILogger<GenerationOrchestrator> Logger;

    public GenerationOrchestrator(
        AppSettings appSettings,
        LocalLibraryGenerationService libraryGenerationService,
        TypeSpecFileService typeSpecFileService,
        OpenAIService aiService,
        BuildWorkflow buildWorkflow,
        ErrorAnalysisService errorAnalysisService,
        TypeSpecPatchApplicator patchApplicator,
        ILogger<GenerationOrchestrator> logger)
    {
        AppSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        LibraryGenerationService = libraryGenerationService ?? throw new ArgumentNullException(nameof(libraryGenerationService));
        TypeSpecFileService = typeSpecFileService ?? throw new ArgumentNullException(nameof(typeSpecFileService));
        AIService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        BuildWorkflow = buildWorkflow ?? throw new ArgumentNullException(nameof(buildWorkflow));
        ErrorAnalysisService = errorAnalysisService ?? throw new ArgumentNullException(nameof(errorAnalysisService));
        PatchApplicator = patchApplicator ?? throw new ArgumentNullException(nameof(patchApplicator));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the complete generation workflow with iterative error fixing
    /// </summary>
    public async Task ExecuteAsync(ValidationContext validationContext, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Starting library generation process");

        // Download TypeSpec files if from GitHub
        if (!string.IsNullOrWhiteSpace(validationContext.ValidatedCommitId))
        {
            await TypeSpecFileService.DownloadGitHubTypeSpecFilesAsync(validationContext, cancellationToken);
        }

        // Setup local environment
        await LibraryGenerationService.InstallTypeSpecDependencies(cancellationToken);

        // Execute iterative fixing workflow
        await ExecuteIterativeFixingAsync(validationContext, cancellationToken);

        Logger.LogInformation("Library generation completed successfully");
    }

    /// <summary>
    /// Executes the iterative compile -> analyze -> fix -> apply cycle
    /// </summary>
    private async Task ExecuteIterativeFixingAsync(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        var currentIteration = 0;
        var maxIterations = AppSettings.MaxIterations;

        while (currentIteration < maxIterations)
        {
            Logger.LogInformation("\n=== Iteration {Current}/{Max} ===\n", currentIteration + 1, maxIterations);

            // Execute build workflow (TypeSpec compilation + SDK build)
            var workflowResult = await BuildWorkflow.ExecuteAsync(validationContext, cancellationToken);

            if (workflowResult.IsSuccess)
            {
                Logger.LogInformation("No compile or build errors found");
                return;
            }

            var errorOutput = workflowResult?.ProcessException?.Output ?? string.Empty;

            // Generate fixes from error output
            var fixes = await ErrorAnalysisService.GenerateFixesFromFailureLogsAsync(errorOutput, cancellationToken);

            if (fixes.Count == 0)
            {
                Logger.LogWarning("No fixes generated for compilation/build errors - errors may not be addressable");
                throw new InvalidOperationException("No addressable errors found in compilation/build output");
            }

            Logger.LogInformation("Generated {FixCount} fixes for iteration {Iteration}", fixes.Count, currentIteration + 1);

            // Apply fixes and apply the changes
            var patchResponse = await AIService.GenerateFixesAsync(fixes, validationContext, cancellationToken);
        
            // Apply patch
            await PatchApplicator.ApplyPatchAsync(
                patchResponse, validationContext.ValidatedTypeSpecDir, cancellationToken);

            Logger.LogInformation("Iteration {Current} completed - fixes applied", currentIteration + 1);
            currentIteration++;
        }

        Logger.LogWarning(" Maximum iterations ({Max}) reached without resolution", maxIterations);
        throw new InvalidOperationException($"Maximum iterations ({maxIterations}) reached");
    }
}
