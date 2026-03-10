// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class PythonLanguageService : LanguageService
{
    private readonly INpxHelper npxHelper;
    private readonly IPythonHelper pythonHelper;
    private readonly ICopilotAgentRunner copilotAgentRunner;

    public PythonLanguageService(
        IProcessHelper processHelper,
        IPythonHelper pythonHelper,
        INpxHelper npxHelper,
        ICopilotAgentRunner copilotAgentRunner,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IPackageInfoHelper packageInfoHelper,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, packageInfoHelper, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.pythonHelper = pythonHelper;
        this.npxHelper = npxHelper;
        this.copilotAgentRunner = copilotAgentRunner;
    }
    public override SdkLanguage Language { get; } = SdkLanguage.Python;
    public override bool IsCustomizedCodeUpdateSupported => true;

    /// <summary>
    /// Python packages are identified by setup.py or pyproject.toml files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["setup.py", "pyproject.toml"];

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving Python package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
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
            var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
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
        // e.g., azure/packagename/_patch.py, azure/packagename/models/_patch.py, azure/packagename/operations/_patch.py, azure/packagename/_operations/_patch.py
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

    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo, string? ArtifactPath)> PackAsync(
        string packagePath, string? outputPath = null, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Packing Python SDK project at: {PackagePath}", packagePath);

            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return (false, "Package path is required and cannot be empty.", null, null);
            }

            string fullPath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPath))
            {
                return (false, $"Package path does not exist: {fullPath}", null, null);
            }

            var packageInfo = await GetPackageInfo(fullPath, ct);
            var packageName = packageInfo?.PackageName ?? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar));

            var args = new List<string> { packageName };
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                args.AddRange(["-d", outputPath]);
            }

            var result = await pythonHelper.Run(new PythonOptions(
                    "sdk_build",
                    args.ToArray(),
                    workingDirectory: fullPath,
                    timeout: TimeSpan.FromMinutes(timeoutMinutes)
                ),
                ct
            );

            if (result.ExitCode != 0)
            {
                var errorMessage = $"sdk_build command failed with exit code {result.ExitCode}. Output:\n{result.Output}";
                logger.LogError("{ErrorMessage}", errorMessage);
                return (false, errorMessage, packageInfo, null);
            }

            // sdk_build outputs to {repoRoot}/.artifacts/{packageName} by default
            var distDir = outputPath
                ?? (packageInfo?.RepoRoot != null
                    ? Path.Combine(packageInfo.RepoRoot, ".artifacts", packageName)
                    : Path.Combine(fullPath, "dist"));
            string? artifactPath = null;
            if (Directory.Exists(distDir))
            {
                // Prefer .whl over .tar.gz
                var whlFiles = Directory.GetFiles(distDir, "*.whl", SearchOption.TopDirectoryOnly);
                if (whlFiles.Length > 0)
                {
                    artifactPath = whlFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
                }
                else
                {
                    var tarFiles = Directory.GetFiles(distDir, "*.tar.gz", SearchOption.TopDirectoryOnly);
                    if (tarFiles.Length > 0)
                    {
                        artifactPath = tarFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
                    }
                }
            }

            logger.LogInformation("Pack completed successfully. Artifact: {ArtifactPath}", artifactPath ?? "(unknown)");
            return (true, null, packageInfo, artifactPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while packing Python SDK");
            return (false, $"An error occurred: {ex.Message}", null, null);
        }
    }

    /// <summary>
    /// Runs pylint and mypy as the "build" step for Python packages (Python has no compiler).
    /// </summary>
    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(
        string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        var packageInfo = await GetPackageInfo(packagePath, ct);
        var check = await LintCode(packagePath, cancellationToken: ct);
        return check.ExitCode == 0
            ? (true, null, packageInfo)
            : (false, check.CheckStatusDetails, packageInfo);
    }

    public override async Task<List<AppliedPatch>> ApplyPatchesAsync(
        string customizationRoot,
        string packagePath,
        string buildContext,
        CancellationToken ct)
    {
        try
        {
            var patchFiles = Directory.GetFiles(customizationRoot, "_patch.py", SearchOption.AllDirectories)
                .Where(HasNonEmptyAllExport)
                .ToList();

            if (patchFiles.Count == 0)
            {
                logger.LogDebug("No _patch.py files with customizations found in {Root}", customizationRoot);
                return [];
            }

            var patchFilePaths = patchFiles.Select(f => Path.GetRelativePath(customizationRoot, f)).ToList();
            var readFilePaths = patchFiles.Select(f => Path.GetRelativePath(packagePath, f)).ToList();
            var patchLog = new ConcurrentBag<AppliedPatch>();

            var prompt = new PythonErrorDrivenPatchTemplate(
                buildContext,
                packagePath,
                customizationRoot,
                readFilePaths,
                patchFilePaths).BuildPrompt();

            var agent = new CopilotAgent<string>
            {
                Instructions = prompt,
                MaxIterations = 25,
                Tools =
                [
                    FileTools.CreateReadFileTool(customizationRoot, includeLineNumbers: true,
                        description: "Read files from the package directory (generated code, _patch.py files, etc.)"),
                    CodePatchTools.CreateCodePatchTool(customizationRoot,
                        description: "Apply code patches to _patch.py customization files only",
                        onPatchApplied: patchLog.Add)
                ]
            };

            try
            {
                await copilotAgentRunner.RunAsync(agent, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception agentEx)
            {
                logger.LogDebug(agentEx, "CopilotAgent terminated early");
            }

            var appliedPatches = patchLog.ToList();
            logger.LogInformation("Patch application completed, patches applied: {PatchCount}", appliedPatches.Count);
            return appliedPatches;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply patches");
            return [];
        }
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
                    // If line contains quoted strings, there are exports
                    if (line.Contains('"') || line.Contains('\''))
                    {
                        return true;
                    }

                    // If line has [ but not ] on the same line after the =, it's multiline and non-empty.
                    var valueAfterEquals = line[(line.LastIndexOf('=') + 1)..].Trim();
                    if (valueAfterEquals.StartsWith('[') && !valueAfterEquals.StartsWith("[]"))
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
