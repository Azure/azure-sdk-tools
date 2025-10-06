using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// JavaScript-specific implementation of language repository service.
/// Uses tools like npm, yarn, node, eslint, etc. for JavaScript development workflows.
/// </summary>
public class JavaScriptLanguageSpecificChecks : ILanguageSpecificChecks
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<JavaScriptLanguageSpecificChecks> _logger;

    public JavaScriptLanguageSpecificChecks(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<JavaScriptLanguageSpecificChecks> logger)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
    }

    public async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        // Implementation for analyzing dependencies in a JavaScript project
        return await Task.FromResult(new CLICheckResponse());
    }

    public async Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Running 'pnpm run update-snippets' in {PackagePath}", packagePath);

            var result = await _processHelper.Run(new(
                    "pnpm",
                    ["run", "update-snippets"],
                    workingDirectory: packagePath
                ),
                cancellationToken
            );

            if (result.ExitCode != 0)
            {
                _logger.LogError("'pnpm run update-snippets' failed with exit code {ExitCode}", result.ExitCode);
                return new CLICheckResponse(result)
                {
                    NextSteps = ["Review the error output and attempt to resolve the issue."]
                };
            }

            return new CLICheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating snippets for JavaScript project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error updating snippets: {ex.Message}");
        }
    }
}
