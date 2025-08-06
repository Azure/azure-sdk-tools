using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Python-specific implementation of language repository service.
/// Uses tools like tox, pip, black, flake8, etc. for Python development workflows.
/// </summary>
public class PythonLanguageRepoService : LanguageRepoService
{
    private readonly ILogger _logger;

    public PythonLanguageRepoService(string repositoryPath, ILogger? logger = null) : base(repositoryPath)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public override async Task<ICLICheckResponse> AnalyzeDependenciesAsync()
    {
        try
        {
            _logger.LogInformation($"Starting dependency analysis for Python project at: {_repositoryPath}");
            
            // Run tox for dependency analysis
            var command = "tox";
            var toxConfigPath = Path.Join("..", "..", "..", "eng", "tox", "tox.ini");
            var arguments = $"run -e mindependency -c {toxConfigPath} --root .";
            
            _logger.LogInformation("Executing command: {Command} {Arguments}", command, arguments);
            
            var result = await RunCommandAsync(command, arguments);
            
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

    public override async Task<ICLICheckResponse> FormatCodeAsync()
    {
        try
        {
            _logger.LogInformation("Starting code formatting for Python project at: {RepositoryPath}", _repositoryPath);
            
            // Run black for code formatting
            var result = await RunCommandAsync("black", ".");
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Code formatting completed successfully with exit code 0");
                return CreateSuccessResponse($"Code formatting completed successfully.\n{result.Output}");
            }
            else
            {
                _logger.LogWarning("Code formatting failed with exit code {ExitCode}", result.ExitCode);
                var errorMessage = result is FailureCLICheckResponse failure ? failure.Error : "";
                return CreateFailureResponse($"Code formatting failed with exit code {result.ExitCode}.\n{errorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during code formatting");
            return CreateCookbookResponse(
                "https://black.readthedocs.io/en/stable/", 
                $"Failed to run code formatting. Ensure black is installed. Error: {ex.Message}");
        }
    }

    public override async Task<ICLICheckResponse> LintCodeAsync()
    {
        try
        {
            _logger.LogInformation("Starting linting for Python project at: {RepositoryPath}", _repositoryPath);
            
            // Run flake8 for linting
            var result = await RunCommandAsync("flake8", ".");
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Linting completed successfully with exit code 0");
                return CreateSuccessResponse($"Linting completed successfully.\n{result.Output}");
            }
            else
            {
                _logger.LogWarning("Linting found issues with exit code {ExitCode}", result.ExitCode);
                return CreateFailureResponse($"Linting found issues.\n{result.Output}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during linting");
            return CreateCookbookResponse(
                "https://flake8.pycqa.org/en/latest/", 
                $"Failed to run linting. Ensure flake8 is installed. Error: {ex.Message}");
        }
    }

    public override async Task<ICLICheckResponse> RunTestsAsync()
    {
        try
        {
            _logger.LogInformation("Starting tests for Python project at: {RepositoryPath}", _repositoryPath);
            
            // Run pytest
            var result = await RunCommandAsync("pytest", "--verbose");
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Tests passed successfully with exit code 0");
                return CreateSuccessResponse($"Tests passed successfully.\n{result.Output}");
            }
            else
            {
                _logger.LogWarning("Tests failed with exit code {ExitCode}", result.ExitCode);
                return CreateFailureResponse($"Tests failed with exit code {result.ExitCode}.\n{result.Output}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during test execution");
            return CreateCookbookResponse(
                "https://docs.pytest.org/en/stable/", 
                $"Failed to run tests. Ensure pytest is installed. Error: {ex.Message}");
        }
    }

    public override async Task<ICLICheckResponse> BuildProjectAsync()
    {
        try
        {
            _logger.LogInformation("Starting project build for Python project at: {RepositoryPath}", _repositoryPath);
            
            // Run pip install in development mode
            var result = await RunCommandAsync("pip", "install -e .");
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Project build completed successfully with exit code 0");
                return CreateSuccessResponse($"Project build completed successfully.\n{result.Output}");
            }
            else
            {
                _logger.LogWarning("Project build failed with exit code {ExitCode}", result.ExitCode);
                var errorMessage = result is FailureCLICheckResponse failure ? failure.Error : "";
                return CreateFailureResponse($"Project build failed with exit code {result.ExitCode}.\n{errorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during project build");
            return CreateCookbookResponse(
                "https://pip.pypa.io/en/stable/", 
                $"Failed to build project. Ensure pip is available. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to run command line tools asynchronously.
    /// </summary>
    private async Task<ICLICheckResponse> RunCommandAsync(string fileName, string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = _repositoryPath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

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
