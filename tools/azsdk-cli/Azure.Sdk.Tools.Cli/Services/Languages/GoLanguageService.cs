// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Language-specific helper for Go packages. Provides structural package info plus lazy accessors
/// for samples directory, file extension, and version parsing.
/// </summary>
public partial class GoLanguageService : LanguageService
{
    public GoLanguageService(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.powershellHelper = powershellHelper;
    }

    private readonly string goUnix = "go";
    private readonly string goWin = "go.exe";
    private readonly string gofmtUnix = "gofmt";
    private readonly string gofmtWin = "gofmt.exe";
    private readonly string golangciLintUnix = "golangci-lint";
    private readonly string golangciLintWin = "golangci-lint.exe";
    private readonly IPowershellHelper powershellHelper;

    // Known locations for Go customization files
    private const string CustomizationPathInternalGenerate = "internal/generate";
    private const string CustomizationPathTestdataGenerate = "testdata/generate";

    public override SdkLanguage Language { get; } = SdkLanguage.Go;
    public override bool IsCustomizedCodeUpdateSupported => true;

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        var fullPath = RealPath.GetRealPath(packagePath);
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        var sdkRoot = Path.Combine(repoRoot, "sdk");

        try
        {
            var commonPS1 = Path.Join(repoRoot, "eng", "common", "scripts", "common.ps1");

            // The powershell outputs this:
            // {
            //   "Name": "sdk/messaging/azservicebus",
            //   "Version": "1.10.1-beta.1",
            //   "DirectoryPath": "../sdk/messaging/azservicebus",
            //   "ServiceDirectory": "messaging/azservicebus",
            //   "ReadMePath": "../sdk/messaging/azservicebus/README.md",
            //   "ChangeLogPath": "../sdk/messaging/azservicebus/CHANGELOG.md",
            //   "SdkType": "client",
            //   "IsNewSdk": true,
            //   "ReleaseStatus": "Unreleased",
            //   "IncludedForValidation": false,
            //   "CIParameters": {
            //     "CIMatrixConfigs": []
            //   },
            //   "VersionFile": "/home/ripark/src/az/sdk/messaging/azservicebus/internal/constants.go",
            //   "ModuleName": "azservicebus"
            // }
            logger.LogDebug("Resolving Go package info for path: {packagePath}", packagePath);
            string[] args = [$". {commonPS1}; Get-GoModuleProperties('{packagePath}') | ConvertTo-Json"];
            var processResult = await processHelper.Run(new PowershellOptions(args, workingDirectory: repoRoot), ct);

            if (processResult.ExitCode != 0)
            {
                throw new Exception($"Failed to extract package properties for {packagePath}: {processResult.Output}");
            }

            GoModulePropertiesPowershell goModuleProperties;

            try
            {
                goModuleProperties = JsonSerializer.Deserialize<GoModulePropertiesPowershell>(processResult.Stdout)
                    ?? throw new Exception($"Failed to deserialize results from Get-GoModuleProperties.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to deserialized JSON output '{processResult.Stdout}'", ex);
            }

            var sdkType = goModuleProperties.SdkType switch
            {
                "mgmt" => SdkType.Management,
                "client" => SdkType.Dataplane,
                _ => SdkType.Unknown,
            };

            var model = new PackageInfo
            {
                PackagePath = fullPath,
                RepoRoot = repoRoot,
                RelativePath = Path.GetRelativePath(sdkRoot, fullPath).TrimStart(Path.DirectorySeparatorChar),
                PackageName = goModuleProperties.Name,
                PackageVersion = goModuleProperties.Version,
                ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
                SdkType = sdkType,
                Language = SdkLanguage.Go,
                SamplesDirectory = fullPath
            };

            logger.LogDebug("Resolved Go package: {packageName} v{packageVersion}", model.PackageName ?? "(unknown)", model.PackageVersion ?? "(unknown)");
            return model;
        }
        catch (Exception ex)
        {
            // NOTE: this method, via the LanguageService, cannot throw these exceptions, so we only log it.
            logger.LogDebug(ex, "Exception thrown when trying to get package properties for {Path}", packagePath);
            return new PackageInfo()
            {
                PackagePath = fullPath,
                RepoRoot = repoRoot,
                RelativePath = Path.GetRelativePath(sdkRoot, fullPath).TrimStart(Path.DirectorySeparatorChar),
                PackageName = null,
                PackageVersion = null,
                ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
                SdkType = SdkType.Unknown,
                Language = SdkLanguage.Go,
                SamplesDirectory = fullPath
            };
        }
    }

    public override string? HasCustomizations(string packagePath, CancellationToken ct)
    {
        // Go customization files can live in different locations depending on the package.
        // Known locations include:
        //   - internal/generate (most common)
        //   - testdata/generate (e.g., azcertificates)
        // TODO: In the future, check tspconfig.yaml for "go-generate" directive for definitive detection.

        try
        {
            string[] knownLocations = [CustomizationPathInternalGenerate, CustomizationPathTestdataGenerate];
            
            foreach (var location in knownLocations)
            {
                var customizationPath = Path.Combine(packagePath, location);
                if (Directory.Exists(customizationPath))
                {
                    logger.LogDebug("Found Go customization directory at {CustomizationPath}", customizationPath);
                    return customizationPath;
                }
            }

            logger.LogDebug("No Go customization directory found in {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for Go customization files in {PackagePath}", packagePath);
            return null;
        }
    }

    /// <summary>
    /// These are the properties that come out of the Get-GoModuleProperties powershell func.
    /// </summary>
    private record GoModulePropertiesPowershell(string? Name, string? Version, string? SdkType);
}
