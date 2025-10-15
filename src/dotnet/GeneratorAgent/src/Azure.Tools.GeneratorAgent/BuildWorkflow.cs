using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent;

/// <summary>
/// Workflow for executing TypeSpec compilation and SDK build sequentially
/// </summary>
internal class BuildWorkflow
{
    private readonly LocalLibraryGenerationService CompileService;
    private readonly LibraryBuildService BuildService;
    private readonly ILogger<BuildWorkflow> Logger;

    public BuildWorkflow(
        LocalLibraryGenerationService compileService,
        LibraryBuildService buildService,
        ILogger<BuildWorkflow> logger)
    {
        CompileService = compileService ?? throw new ArgumentNullException(nameof(compileService));
        BuildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<object>> ExecuteAsync(ValidationContext context, CancellationToken cancellationToken)
    {

        // TypeSpec compilation step
        var compileResult = await CompileService.CompileTypeSpecAsync(context, cancellationToken);
        if (compileResult.IsFailure)
        {
            return compileResult;
        }
        Logger.LogInformation("TypeSpec Compilation completed successfully");

        // SDK build step  
        var buildResult = await BuildService.BuildSdkAsync(context.ValidatedSdkDir, cancellationToken);
        if (buildResult.IsFailure)
        {
            return buildResult;
        }
        Logger.LogInformation("SDK Build completed successfully");
        return Result<object>.Success(new object());
    }
}