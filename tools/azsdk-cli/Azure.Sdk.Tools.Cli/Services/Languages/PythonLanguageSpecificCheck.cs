using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Python-specific implementation of language checks.
/// </summary>
public class PythonLanguageSpecificCheck : ILanguageSpecificCheck
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<PythonLanguageSpecificCheck> _logger;

    public PythonLanguageSpecificCheck(
        IProcessHelper processHelper, 
        INpxHelper npxHelper, 
        IGitHelper gitHelper, 
        ILogger<PythonLanguageSpecificCheck> logger)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
    }

    public string SupportedLanguage => "Python";

    public bool CanHandle(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
        {
            return false;
        }

        var repositoryPath = _gitHelper.DiscoverRepoRoot(packagePath);

        // Get the repository name from the directory path
        var repoName = Path.GetFileName(repositoryPath?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.ToLowerInvariant() ?? "";

        _logger.LogInformation($"Repository name: {repoName}");

        // Extract the language from the repository name
        if (repoName.Contains("azure-sdk-for-python"))
        {
            _logger.LogInformation("Detected language: python from repository name");
            return true;
        }
        return false;
    }

    public async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Python-specific dependency analysis for package at {PackagePath}", packagePath);

        try
        {
            var issues = new List<string>();

            // Check for requirements.txt
            var requirementsPath = Path.Combine(packagePath, "requirements.txt");
            if (File.Exists(requirementsPath))
            {
                var requirements = await File.ReadAllTextAsync(requirementsPath, ct);
                _logger.LogDebug("Found requirements.txt with {Length} characters", requirements.Length);
                
                await ValidatePythonRequirements(requirements, issues, ct);
            }

            // Check for setup.py
            var setupPyPath = Path.Combine(packagePath, "setup.py");
            if (File.Exists(setupPyPath))
            {
                var setupContent = await File.ReadAllTextAsync(setupPyPath, ct);
                _logger.LogDebug("Found setup.py with {Length} characters", setupContent.Length);
                
                await ValidateSetupPyDependencies(setupContent, issues, ct);
            }

            // Check for pyproject.toml
            var pyprojectPath = Path.Combine(packagePath, "pyproject.toml");
            if (File.Exists(pyprojectPath))
            {
                var pyprojectContent = await File.ReadAllTextAsync(pyprojectPath, ct);
                _logger.LogDebug("Found pyproject.toml with {Length} characters", pyprojectContent.Length);
                
                await ValidatePyprojectDependencies(pyprojectContent, issues, ct);
            }

            var statusDetails = issues.Any() ? string.Join("\n", issues) : "Python dependency analysis completed successfully";
            var exitCode = issues.Any() ? 1 : 0;

            return new CLICheckResponse(exitCode, statusDetails, 
                exitCode == 0 ? "Python dependencies are valid" : "Python dependency issues found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Python dependency analysis");
            return new CLICheckResponse(1, ex.ToString(), $"Python dependency analysis failed: {ex.Message}");
        }
    }

    private async Task ValidatePythonRequirements(string requirements, List<string> issues, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requirements))
        {
            issues.Add("requirements.txt is empty");
            return;
        }

        var lines = requirements.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#") || string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            // Check for version constraints
            if (!trimmed.Contains("==") && !trimmed.Contains(">=") && !trimmed.Contains("~="))
            {
                issues.Add($"Requirement '{trimmed}' should specify version constraints");
            }
        }

        await Task.CompletedTask;
    }

    private async Task ValidateSetupPyDependencies(string setupContent, List<string> issues, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(setupContent))
        {
            issues.Add("setup.py is empty");
            return;
        }

        // Check for basic setup.py structure
        if (!setupContent.Contains("setup("))
        {
            issues.Add("setup.py does not contain setup() function call");
        }

        // Check for required metadata
        var requiredFields = new[] { "name", "version", "description", "author" };
        foreach (var field in requiredFields)
        {
            if (!setupContent.Contains($"{field}="))
            {
                issues.Add($"setup.py missing required field: {field}");
            }
        }

        await Task.CompletedTask;
    }

    private async Task ValidatePyprojectDependencies(string pyprojectContent, List<string> issues, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pyprojectContent))
        {
            issues.Add("pyproject.toml is empty");
            return;
        }

        // Basic TOML structure validation
        if (!pyprojectContent.Contains("[project]") && !pyprojectContent.Contains("[tool.setuptools]"))
        {
            issues.Add("pyproject.toml should contain [project] or [tool.setuptools] section");
        }

        await Task.CompletedTask;
    }
}