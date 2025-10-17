using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Python-specific implementation of language checks.
/// </summary>
public class PythonLanguageSpecificChecks : ILanguageSpecificChecks
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<PythonLanguageSpecificChecks> _logger;

    public PythonLanguageSpecificChecks(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<PythonLanguageSpecificChecks> logger)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
    }

    public async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
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

    public async Task<CLICheckResponse> UpdateSnippetsAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting snippet update for Python project at: {PackagePath}", packagePath);

            // Find the repository root from the package path using GitHelper
            var repoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            _logger.LogInformation("Found repository root at: {RepoRoot}", repoRoot);

            // Construct path to the Python snippet updater script
            var scriptPath = Path.Combine(repoRoot, "eng", "tools", "azure-sdk-tools", "ci_tools", "snippet_update", "python_snippet_updater.py");

            // Check if the script exists
            if (!File.Exists(scriptPath))
            {
                _logger.LogError("Python snippet updater script not found at: {ScriptPath}", scriptPath);
                return new CLICheckResponse(1, "", $"Python snippet updater script not found at: {scriptPath}");
            }

            _logger.LogInformation("Using Python snippet updater script: {ScriptPath}", scriptPath);

            // Check if Python is available
            var pythonCheckResult = await _processHelper.Run(new("python", ["--version"], timeout: TimeSpan.FromSeconds(10)), cancellationToken);
            if (pythonCheckResult.ExitCode != 0)
            {
                _logger.LogError("Python is not installed or not available in PATH");
                return new CLICheckResponse(1, "", "Python is not installed or not available in PATH. Please install Python to use snippet update functionality.");
            }

            _logger.LogInformation("Python is available: {PythonVersion}", pythonCheckResult.Output.Trim());

            // Run the Python snippet updater
            var command = "python";
            var args = new[] { scriptPath, packagePath };

            _logger.LogInformation("Executing command: {Command} {Arguments}", command, string.Join(" ", args));
            var timeout = TimeSpan.FromMinutes(5);
            var result = await _processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: timeout), cancellationToken);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Snippet update completed successfully - all snippets are up to date");
                return new CLICheckResponse(result.ExitCode, "All snippets are up to date");
            }
            else
            {
                _logger.LogWarning("Snippet update detected out-of-date snippets with exit code {ExitCode}", result.ExitCode);
                return new CLICheckResponse(result.ExitCode, result.Output, "Some snippets were updated or need attention");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating snippets for Python project at: {PackagePath}", packagePath);
            return new CLICheckResponse(1, "", $"Error updating snippets: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> LintCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        // Implementation for linting code in a Python project
        // Could use pylint, flake8, black --check, etc.
        return await Task.FromResult(new CLICheckResponse(0, "Code linting not yet implemented for Python", ""));
    }

    public async Task<CLICheckResponse> FormatCodeAsync(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        // Implementation for formatting code in a Python project
        // Could use black, autopep8, yapf, etc.
        return await Task.FromResult(new CLICheckResponse(0, "Code formatting not yet implemented for Python", ""));
    }
}
