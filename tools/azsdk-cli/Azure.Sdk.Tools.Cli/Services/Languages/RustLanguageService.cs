// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Language-specific service for Rust packages. Since the Rust SDK repo does not have a
/// swagger_to_sdk_config.json, the build script path is hardcoded to eng/scripts/build-sdk.ps1.
/// </summary>
public sealed class RustLanguageService : LanguageService
{
    private const string BuildScriptRelativePath = "eng/scripts/build-sdk.ps1";

    public RustLanguageService(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IPackageInfoHelper packageInfoHelper,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, packageInfoHelper, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.powershellHelper = powershellHelper;
    }

    private readonly IPowershellHelper powershellHelper;

    public override SdkLanguage Language { get; } = SdkLanguage.Rust;

    /// <summary>
    /// Rust packages are identified by Cargo.toml files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["Cargo.toml"];

    /// <summary>
    /// Builds Rust SDK code by executing the hardcoded build script at eng/scripts/build-sdk.ps1.
    /// Unlike other languages, Rust does not use swagger_to_sdk_config.json for build configuration.
    /// </summary>
    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(
        string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Building Rust SDK for project path: {PackagePath}", packagePath);

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

            PackageInfo? packageInfo = null;
            try
            {
                packageInfo = await GetPackageInfo(packagePath, ct);
            }
            catch (NotImplementedException)
            {
                logger.LogDebug("GetPackageInfo not yet implemented for Rust, proceeding without package info.");
            }

            var scriptPath = Path.Combine(sdkRepoRoot, BuildScriptRelativePath);
            if (!File.Exists(scriptPath))
            {
                return (false, $"Build script not found at: {scriptPath}.", packageInfo);
            }

            logger.LogDebug("Executing Rust build script: {ScriptPath}", scriptPath);

            var options = new PowershellOptions(
                scriptPath,
                ["-PackagePath", packagePath],
                logOutputStream: true,
                workingDirectory: sdkRepoRoot,
                timeout: TimeSpan.FromMinutes(timeoutMinutes)
            );

            var result = await powershellHelper.Run(options, ct);
            var trimmedOutput = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                var errorMessage = $"Build failed with exit code {result.ExitCode}. Output:\n{trimmedOutput}";
                logger.LogDebug("Build failed: {ErrorMessage}", errorMessage);
                return (false, errorMessage, packageInfo);
            }

            logger.LogDebug("Build completed successfully.");
            return (true, null, packageInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while building Rust SDK code");
            return (false, $"An error occurred: {ex.Message}", null);
        }
    }
}
