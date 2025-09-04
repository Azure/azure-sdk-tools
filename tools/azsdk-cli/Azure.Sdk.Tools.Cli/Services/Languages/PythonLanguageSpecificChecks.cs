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

    public string SupportedLanguage => "Python";



    public async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct = default)
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

    public async Task<CLICheckResponse> LintCodeAsync(string packagePath, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation($"Starting pylint check for Python project at: {packagePath}");

            // Find the repository root from the package path using GitHelper
            var repoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            _logger.LogInformation("Found repository root at: {RepoRoot}", repoRoot);

            // Check if azure-sdk-tools is available, if not try to install it
            var installed_tools = await EnsureAzureSdkToolsInstalledAsync(repoRoot, ct);

            if (!installed_tools)
            {
                _logger.LogError("Required tools are not installed. Aborting pylint check.");
                return new CLICheckResponse(1, "", "Required tools are not installed.");
            }

            // Run azpysdk pylint command
            var command = "azpysdk";
            var args = new[] { "pylint", "--isolate", packagePath };

            _logger.LogInformation("Executing command: {Command} {Arguments}", command, string.Join(" ", args));
            var timeout = TimeSpan.FromMinutes(10);
            var result = await _processHelper.Run(new(command, args, workingDirectory: repoRoot, timeout: timeout), ct);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Pylint check completed successfully with exit code 0");
                return new CLICheckResponse(result.ExitCode, result.Output);
            }
            else
            {
                _logger.LogWarning("Pylint check failed with exit code {ExitCode}", result.ExitCode);
                return new CLICheckResponse(result.ExitCode, result.Output, "Pylint check failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during pylint check");
            return new CLICheckResponse(1, "", $"Failed to run pylint check. Error: {ex.Message}");
        }
    }

    private async Task<bool> EnsureAzureSdkToolsInstalledAsync(string repoRoot, CancellationToken ct = default)
    {
        try
        {
            // First check if azpysdk is already available
            var checkResult = await _processHelper.Run(new("azpysdk", new[] { "--help" }, workingDirectory: repoRoot, timeout: TimeSpan.FromSeconds(30)), ct);
            if (checkResult.ExitCode == 0)
            {
                _logger.LogInformation("azpysdk is already available");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("azpysdk not found, attempting to install azure-sdk-tools. Error: {Error}", ex.Message);
        }

        // Check if azure-sdk-tools package exists in the repository
        var azureSdkToolsPath = Path.Combine(repoRoot, "eng", "tools", "azure-sdk-tools");
        if (Directory.Exists(azureSdkToolsPath))
        {
            _logger.LogInformation("Found azure-sdk-tools at: {AzureSdkToolsPath}, attempting to install", azureSdkToolsPath);
            
            // Install azure-sdk-tools using pip
            var installResult = await _processHelper.Run(new("pip", new[] { "install", azureSdkToolsPath }, workingDirectory: repoRoot, timeout: TimeSpan.FromMinutes(3)), ct);
            
            if (installResult.ExitCode == 0)
            {
                _logger.LogInformation("Successfully installed azure-sdk-tools");
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to install azure-sdk-tools. Exit code: {ExitCode}, Output: {Output}", installResult.ExitCode, installResult.Output);
                return false;
            }
        }
        else
        {
            _logger.LogWarning("azure-sdk-tools directory not found at: {AzureSdkToolsPath}", azureSdkToolsPath);
            return false;
        }
    }
}
