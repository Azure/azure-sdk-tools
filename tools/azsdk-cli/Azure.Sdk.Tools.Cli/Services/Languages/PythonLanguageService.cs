// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class PythonLanguageService : LanguageService
{
    private readonly INpxHelper npxHelper;
    private readonly IPythonHelper pythonHelper;
    private readonly IMicroagentHostService microagentHost;

    public PythonLanguageService(
        IProcessHelper processHelper,
        IPythonHelper pythonHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IMicroagentHostService? microagentHost = null)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper)
    {
        this.pythonHelper = pythonHelper;
        this.npxHelper = npxHelper;
        this.microagentHost = microagentHost!;
    }
    public override SdkLanguage Language { get; } = SdkLanguage.Python;
    public override bool IsCustomizedCodeUpdateSupported => true;

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving Python package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await PackagePathParser.ParseAsync(gitHelper, packagePath, ct);
        var (packageName, packageVersion) = await TryGetPackageInfoAsync(fullPath, ct);
        
        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for Python package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for Python package at {fullPath}", fullPath);
        }

        var sdkType = SdkType.Unknown;
        if (!string.IsNullOrWhiteSpace(packageName))
        {
            sdkType = packageName.StartsWith("azure-mgmt-", StringComparison.OrdinalIgnoreCase)
                ? SdkType.Management
                : SdkType.Dataplane;
        }
        
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.Python,
            SamplesDirectory = Path.Combine(fullPath, "samples"),
            SdkType = sdkType
        };
        
        logger.LogDebug("Resolved Python package: {sdkType} {packageName} v{packageVersion} at {relativePath}", 
            sdkType, packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);
        
        return model;
    }

private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
{
    string? packageName = null;
    string? packageVersion = null;
    
    try
    {
        logger.LogTrace("Calling get_package_properties.py for {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await PackagePathParser.ParseAsync(gitHelper, packagePath, ct);
        var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "get_package_properties.py");
        
        var result = await pythonHelper.Run(new PythonOptions(
                "python",
                [scriptPath, "-s", packagePath],
                workingDirectory: repoRoot,
                logOutputStream: false
            ),
            ct
        );
        
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
        {
            // Parse the output from get_package_properties.py
            // Format: <name> <version> <is_new_sdk> <directory> <dependent_packages>
            var lines = result.Output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                packageName = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0] : null;
                packageVersion = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null;
                
                logger.LogTrace("Python script returned: name={packageName}, version={packageVersion}", packageName, packageVersion);
                return (packageName, packageVersion);
            }
        }
        
        logger.LogWarning("Python script failed with exit code {exitCode}. Output: {output}", result.ExitCode, result.Output);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Error running Python script for {packagePath}", packagePath);
        
    }
    return (packageName, packageVersion);
}

    public override string? HasCustomizations(string packagePath, CancellationToken ct = default)
    {
        // Python SDKs can have _patch.py files in multiple locations within the package:
        // e.g., azure/packagename/_patch.py, azure/packagename/models/_patch.py, azure/packagename/operations/_patch.py
        //
        // However, autorest.python generates empty _patch.py templates by default with:
        //   __all__: List[str] = []
        //
        // A _patch.py file only has actual customizations if __all__ is non-empty.
        // See: https://github.com/Azure/autorest.python/blob/main/docs/customizations.md

        try
        {
            var patchFiles = Directory.GetFiles(packagePath, "_patch.py", SearchOption.AllDirectories);
            foreach (var patchFile in patchFiles)
            {
                if (HasNonEmptyAllExport(patchFile))
                {
                    logger.LogDebug("Found Python _patch.py with customizations at {PatchFile}", patchFile);
                    return packagePath; // Return package path since patches are scattered
                }
            }

            logger.LogDebug("No Python _patch.py files with customizations found in {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for Python customization files in {PackagePath}", packagePath);
            return null;
        }
    }

    public override async Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var result = await pythonHelper.Run(new PythonOptions(
                "pytest",
                ["tests"],
                workingDirectory: packagePath
            ),
            ct
        );

        return new TestRunResponse(result);
    }

    /// <summary>
    /// Builds/validates the Python SDK package by running the repository's configured build command.
    /// Unlike the base class which skips Python, this override uses the actual swagger_to_sdk_config
    /// build script (typically mypy/pylint or a build script) to detect errors in customization code.
    /// </summary>
    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Building Python SDK for project path: {PackagePath}", packagePath);

            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return (false, "Package path is required and cannot be empty.", null);
            }

            string fullPath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPath))
            {
                return (false, $"Package full path does not exist: {fullPath}, input package path: {packagePath}.", null);
            }

            packagePath = fullPath;

            string sdkRepoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return (false, $"Failed to discover local sdk repo with project-path: {packagePath}.", null);
            }

            PackageInfo? packageInfo = await GetPackageInfo(packagePath, ct);

            var (configContentType, configValue) = await specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.Build);
            if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrEmpty(configValue))
            {
                logger.LogDebug("Found valid configuration for Python build. Executing configured script...");

                var scriptParameters = new Dictionary<string, string>
                {
                    { "PackagePath", packagePath }
                };

                var processOptions = specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters, timeoutMinutes);
                if (processOptions == null)
                {
                    return (false, "Failed to create process options for build command.", packageInfo);
                }

                var result = await processHelper.Run(processOptions, ct);
                var trimmedOutput = (result.Output ?? string.Empty).Trim();

                if (result.ExitCode != 0)
                {
                    var errorMessage = $"Build failed with exit code {result.ExitCode}. Output:\n{trimmedOutput}";
                    logger.LogDebug("Python build failed: {ErrorMessage}", errorMessage);
                    return (false, errorMessage, packageInfo);
                }

                logger.LogDebug("Python build completed successfully.");
                return (true, null, packageInfo);
            }

            // Fallback: No build config found. Use import-based validation to detect errors
            // in _patch.py customization files. Python _patch.py files run on import, so any
            // NameError, AttributeError, or ImportError from broken super() calls will surface.
            logger.LogInformation("No build configuration found. Falling back to import-based validation...");
            return await ValidateByImportAsync(packagePath, packageInfo, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while building Python SDK code");
            return (false, $"An error occurred: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Applies error-driven patches to Python _patch.py customization files using an LLM microagent.
    /// Finds all _patch.py files with non-empty __all__ exports and passes them to the microagent
    /// along with the build error for targeted repair.
    /// </summary>
    public override async Task<List<AppliedPatch>> ApplyPatchesAsync(
        string commitSha,
        string customizationRoot,
        string packagePath,
        string buildError,
        CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(customizationRoot))
            {
                logger.LogDebug("Customization root does not exist: {Root}", customizationRoot);
                return [];
            }

            // Find all _patch.py files with actual customizations (non-empty __all__)
            var patchFiles = Directory.GetFiles(customizationRoot, "_patch.py", SearchOption.AllDirectories)
                .Where(HasNonEmptyAllExport)
                .ToList();

            if (patchFiles.Count == 0)
            {
                logger.LogDebug("No Python _patch.py files with customizations found");
                return [];
            }

            // Provide paths relative to packagePath for ReadFile tool
            var customizationFiles = patchFiles
                .Select(f => Path.GetRelativePath(packagePath, f))
                .ToList();

            // Build error-driven prompt
            var prompt = new PythonErrorDrivenPatchTemplate(
                buildError,
                packagePath,
                customizationFiles).BuildPrompt();

            // Create patch tool - baseDir is packagePath since _patch.py files are scattered
            var patchTool = new ClientCustomizationCodePatchTool(packagePath)
            {
                Name = "ClientCustomizationCodePatch",
                Description = "Apply code patches to Python _patch.py customization files only (never generated code)"
            };

            var agentDefinition = new Microagent<bool>
            {
                Instructions = prompt,
                MaxToolCalls = 30,
                Tools =
                [
                    new ReadFileTool(packagePath)
                    {
                        Name = "ReadFile",
                        Description = "Read files from the package directory (generated code, customization _patch.py files, etc.)"
                    },
                    patchTool
                ]
            };

            try
            {
                await microagentHost.RunAgentToCompletion(agentDefinition, ct);
            }
            catch (OperationCanceledException)
            {
                // Cancelled externally, re-throw
                throw;
            }
            catch (Exception agentEx)
            {
                // Microagent may have hit MaxToolCalls limit but still applied patches
                logger.LogDebug(agentEx, "Microagent terminated early");
            }

            logger.LogInformation("[STAGE] Patch application completed, patches applied: {PatchCount}", patchTool.AppliedPatches.Count);
            return patchTool.AppliedPatches;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply error-driven patches for Python");
            return [];
        }
    }

    /// <summary>
    /// Validates Python SDK by attempting to import all submodules that contain _patch.py customizations.
    /// Python _patch.py files execute on import, so broken super() calls or missing references
    /// will surface as NameError, AttributeError, or ImportError during import.
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> ValidateByImportAsync(
        string packagePath, PackageInfo? packageInfo, CancellationToken ct)
    {
        // Discover all Python module paths with _patch.py customizations
        var patchFiles = Directory.GetFiles(packagePath, "_patch.py", SearchOption.AllDirectories);
        var modulesToImport = new List<string>();

        foreach (var patchFile in patchFiles)
        {
            if (!HasNonEmptyAllExport(patchFile))
            {
                continue;
            }

            // Convert file path to Python module path
            // e.g., C:\...\azure-ai-vision-face\azure\ai\vision\face\aio\_patch.py
            //     → azure.ai.vision.face.aio
            var patchDir = Path.GetDirectoryName(patchFile)!;
            var relativePath = Path.GetRelativePath(packagePath, patchDir);
            var modulePath = relativePath.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.');
            modulesToImport.Add(modulePath);
        }

        if (modulesToImport.Count == 0)
        {
            logger.LogDebug("No customized modules found to validate via import.");
            return (true, null, packageInfo);
        }

        // Build a Python script that imports all customized modules
        var importStatements = string.Join("; ", modulesToImport.Select(m => $"import {m}"));
        var validationScript = $"import sys; sys.path.insert(0, r'{packagePath}'); {importStatements}; print('All imports successful')";

        logger.LogInformation("Validating Python imports for modules: {Modules}", string.Join(", ", modulesToImport));

        var result = await pythonHelper.Run(new PythonOptions(
            "python",
            ["-c", validationScript],
            workingDirectory: packagePath
        ), ct);

        var output = (result.Output ?? string.Empty).Trim();

        if (result.ExitCode != 0)
        {
            var errorMessage = $"Import validation failed with exit code {result.ExitCode}. Output:\n{output}";
            logger.LogDebug("Python import validation failed: {ErrorMessage}", errorMessage);
            return (false, errorMessage, packageInfo);
        }

        logger.LogDebug("Python import validation completed successfully.");
        return (true, null, packageInfo);
    }

    /// <summary>
    /// Checks if a _patch.py file has a non-empty __all__ export list, indicating actual customizations.
    /// </summary>
    private bool HasNonEmptyAllExport(string patchFilePath)
    {
        try
        {
            foreach (var line in File.ReadLines(patchFilePath))
            {
                if (line.Contains("__all__") && line.Contains("="))
                {
                    // Extract the value part after '=' to avoid confusion with type annotations
                    // e.g., "__all__: List[str] = [" -> " ["
                    var eqIndex = line.LastIndexOf('=');
                    var valuePart = line.Substring(eqIndex + 1).Trim();

                    // If value part contains quoted strings, there are exports
                    if (valuePart.Contains('"') || valuePart.Contains('\''))
                    {
                        return true;
                    }
                    
                    // If value part has [ but not ] on same line, it's multiline = non-empty
                    // e.g., "__all__: List[str] = [" → valuePart is "[" → multiline
                    if (valuePart.Contains('[') && !valuePart.Contains(']'))
                    {
                        return true;
                    }
                    
                    // Single-line empty: __all__ = [] or __all__: List[str] = []
                    return false;
                }
            }
            
            // No __all__ found - assume no customizations (template file)
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error reading Python patch file {PatchFile}", patchFilePath);
            return false;
        }
    }

}
