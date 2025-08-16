using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Python-specific implementation of language repository service.
/// Uses tools like tox, pip, black, flake8, etc. for Python development workflows.
/// </summary>
public class PythonLanguageRepoService : LanguageRepoService
{
    private readonly ILogger<PythonLanguageRepoService> _logger;

    public PythonLanguageRepoService(IProcessHelper processHelper, IGitHelper gitHelper, ILogger<PythonLanguageRepoService> logger)
        : base(processHelper, gitHelper)
    {
        _logger = logger;
    }

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation($"Starting dependency analysis for Python project at: {packagePath}");

            // Find the repository root from the package path using GitHelper
            var repoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            _logger.LogInformation("Found repository root at: {RepoRoot}", repoRoot);

            // Construct path to tox.ini from repository root
            var toxConfigPath = Path.Combine(repoRoot, "eng", "tox", "tox.ini");

            // Verify the tox.ini file exists
            if (!File.Exists(toxConfigPath))
            {
                _logger.LogError("Tox configuration file not found at: {ToxConfigPath}", toxConfigPath);
                return new CLICheckResponse(1, "", $"Tox configuration file not found at: {toxConfigPath}");
            }

            _logger.LogInformation("Using tox configuration file: {ToxConfigPath}", toxConfigPath);

            // Run tox for dependency analysis
            var command = "tox";
            var args = new[] { "run", "-e", "mindependency", "-c", toxConfigPath, "--root", "." };

            _logger.LogInformation("Executing command: {Command} {Arguments}", command, string.Join(" ", args));
            var timeout = TimeSpan.FromMinutes(5);
            var result = await _processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: timeout), ct);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Dependency analysis completed successfully with exit code 0");
                return new CLICheckResponse(result.ExitCode, result.Output);
            }
            else
            {
                _logger.LogWarning("Dependency analysis failed with exit code {ExitCode}", result.ExitCode);
                return new CLICheckResponse(result.ExitCode, result.Output, "Process failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during dependency analysis");
            return new CookbookCLICheckResponse(0, $"Failed to run dependency analysis. Ensure tox is installed. Error: {ex.Message}", "https://docs.python.org/3/tutorial/venv.html");
        }
    }
}
