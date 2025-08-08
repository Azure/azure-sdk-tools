using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Python-specific implementation of language repository service.
/// Uses tools like tox, pip, black, flake8, etc. for Python development workflows.
/// </summary>
public class PythonLanguageRepoService : LanguageRepoService
{
    private readonly ILogger _logger;
    private readonly IGitHelper _gitHelper;

    public PythonLanguageRepoService(string packagePath, IGitHelper gitHelper, ILogger? logger = null) : base(packagePath)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _gitHelper = gitHelper ?? throw new ArgumentNullException(nameof(gitHelper));
    }

    public override async Task<ICLICheckResponse> AnalyzeDependenciesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation($"Starting dependency analysis for Python project at: {_packagePath}");
            
            // Find the repository root from the package path using GitHelper
            var repoRoot = _gitHelper.DiscoverRepoRoot(_packagePath);
            _logger.LogInformation("Found repository root at: {RepoRoot}", repoRoot);
            
            // Construct path to tox.ini from repository root
            var toxConfigPath = Path.Combine(repoRoot, "eng", "tox", "tox.ini");
            
            // Verify the tox.ini file exists
            if (!File.Exists(toxConfigPath))
            {
                _logger.LogError("Tox configuration file not found at: {ToxConfigPath}", toxConfigPath);
                return CreateFailureResponse($"Tox configuration file not found at: {toxConfigPath}");
            }
            
            _logger.LogInformation("Using tox configuration file: {ToxConfigPath}", toxConfigPath);
            
            // Run tox for dependency analysis
            var command = "tox";
            var arguments = $"run -e mindependency -c \"{toxConfigPath}\" --root .";
            
            _logger.LogInformation("Executing command: {Command} {Arguments}", command, arguments);
            
            var result = await RunCommandAsync(command, arguments, ct);
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Dependency analysis completed successfully with exit code 0");
                return CreateSuccessResponse($"Dependency analysis completed successfully.\n{result.Output}");
            }
            else
            {
                _logger.LogWarning("Dependency analysis failed with exit code {ExitCode}", result.ExitCode);
                var errorMessage = result is FailureCLICheckResponse failure ? failure.Error : "";
                return CreateFailureResponse($"Dependency analysis failed with exit code {result.ExitCode}.\n{errorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during dependency analysis");
            return CreateCookbookResponse(
                "https://docs.python.org/3/tutorial/venv.html", 
                $"Failed to run dependency analysis. Ensure tox is installed. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to run command line tools asynchronously.
    /// </summary>
    private async Task<ICLICheckResponse> RunCommandAsync(string fileName, string arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = _packagePath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

    await process.WaitForExitAsync(ct);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (process.ExitCode == 0)
        {
            return new SuccessCLICheckResponse(process.ExitCode, output);
        }
        else
        {
            return new FailureCLICheckResponse(process.ExitCode, output, error);
        }
    }
}
