// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Produces <see cref="PackageInfo"/> for .NET packages.
/// </summary>
public sealed partial class DotnetLanguageService: LanguageService
{
    private const string DotNetCommand = "dotnet";
    private const string RequiredDotNetVersion = "9.0.102"; // TODO - centralize this as part of env setup tool
    private const string GeneratedFolderName = "Generated";
    private static readonly TimeSpan CodeChecksTimeout = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan AotCompatTimeout = TimeSpan.FromMinutes(5);

    private readonly IPowershellHelper powershellHelper;

    public DotnetLanguageService(
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

    public override SdkLanguage Language { get; } = SdkLanguage.DotNet;
    public override bool IsCustomizedCodeUpdateSupported => true;

    private static readonly string[] separator = new[] { "' '" };

    /// <summary>
    /// Gets the default samples directory path relative to the package path.
    /// </summary>
    /// <param name="packagePath">The package path</param>
    /// <returns>The default samples directory path</returns>
    private static string GetDefaultSamplesDirectory(string packagePath) => Path.Combine(packagePath, "tests", "samples");

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving .NET package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
        var (packageName, packageVersion, sdkType) = await TryGetPackageInfoAsync(fullPath, ct);

        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for .NET package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for .NET package at {fullPath}", fullPath);
        }
        if (sdkType == null)
        {
            logger.LogWarning("Could not determine SDK type for .NET package at {fullPath}", fullPath);
        }

        var samplesDirectory = FindSamplesDirectory(fullPath);

        var parsedSdkType = sdkType switch
        {
            "client" => SdkType.Dataplane,
            "mgmt" => SdkType.Management,
            "functions" => SdkType.Functions,
            _ => SdkType.Unknown
        };

        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.DotNet,
            SamplesDirectory = samplesDirectory,
            SdkType = parsedSdkType
        };

        logger.LogDebug("Resolved .NET package: {packageName} v{packageVersion} at {relativePath} (as {parsedSdkType})",
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath, parsedSdkType.ToString() ?? "(unknown)");

        return model;
    }

    private async Task<(string? Name, string? Version, string? SdkType)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var csproj = Directory.GetFiles(Path.Combine(packagePath, "src"), "*.csproj").FirstOrDefault();

            if (csproj == null)
            {
                logger.LogWarning("No .csproj file found in {packagePath}", packagePath);
                return (null, null, null);
            }

            logger.LogTrace("Getting package info via MSBuild for: {csproj}", csproj);

            var result = await processHelper.Run(new ProcessOptions(
                command: "dotnet",
                args: ["msbuild", csproj, "-getTargetResult:GetPackageInfo", "-nologo"]
            ), ct);

            if (result == null || result.ExitCode != 0)
            {
                logger.LogTrace("MSBuild GetPackageInfo failed, returning null values");
                return (null, null, null);
            }

            // Parse JSON output
            using var jsonDoc = JsonDocument.Parse(result.Stdout);
            var targetResults = jsonDoc.RootElement.GetProperty("TargetResults");
            var getPackageInfo = targetResults.GetProperty("GetPackageInfo");
            var items = getPackageInfo.GetProperty("Items");

            // Identity field which contains the package info
            var identity = items[0].GetProperty("Identity").GetString();

            // Parse the identity string:  'pkgPath' 'serviceDir' 'pkgName' 'pkgVersion' 'sdkType' 'isNewSdk' 'dllFolder' 'AotCompatOptOut'
            var parts = identity?.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim('\'', ' '))
                .ToArray();

            if (parts?.Length >= 5) // for now we only need items in the first 5 positions
            {
                var name = parts[2]; // pkgName
                var version = parts[3]; // pkgVersion
                var sdkType = parts[4]; // sdkType

                // Validate we got actual values before returning
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(version))
                {
                    logger.LogTrace("Found package info via MSBuild: {name} v{version} ({sdkType})",
                        name, version, sdkType);

                    return (name, version, sdkType);
                }
            }

            logger.LogTrace("Unable to parse identity string, returning null values");
            return (null, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting .NET package info from {packagePath} using MSBuild GetPackageInfo target", packagePath);
            return (null, null, null);
        }
    }

    /// <summary>
    /// Finds the samples directory by looking for folders under tests that contain files with "#region Snippet:" in their content
    /// </summary>
    /// <param name="packagePath">The package path to search under</param>
    /// <returns>The path to the samples directory, or a default path if not found</returns>
    private string FindSamplesDirectory(string packagePath)
    {
        try
        {
            var testsPath = Path.Combine(packagePath, "tests");
            if (!Directory.Exists(testsPath))
            {
                logger.LogTrace("Tests directory not found at {testsPath}", testsPath);
                return GetDefaultSamplesDirectory(packagePath);
            }

            // Get all subdirectories under tests (sorted for consistent behavior across platforms)
            var testSubdirectories = Directory.GetDirectories(testsPath).OrderBy(d => d).ToArray();

            foreach (var directory in testSubdirectories)
            {
                // Look for .cs files containing "#region Snippet:" in their content
                var sampleFiles = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                    .Where(file =>
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            return content.Contains("#region Snippet:", StringComparison.Ordinal);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .ToArray();

                if (sampleFiles.Length > 0)
                {
                    logger.LogTrace("Found samples directory at {directory} with {count} files containing snippet regions",
                        directory, sampleFiles.Length);
                    return directory;
                }
            }

            logger.LogTrace("No samples directory found under {testsPath}, using default", testsPath);
            return GetDefaultSamplesDirectory(packagePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for samples directory under {packagePath}, using default", packagePath);
            return GetDefaultSamplesDirectory(packagePath);
        }
    }

    public override async Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var testsPath = Path.Combine(packagePath, "tests");
        var workingDirectory = Directory.Exists(testsPath) ? testsPath : packagePath;

        var result = await processHelper.Run(new ProcessOptions(
                command: "dotnet",
                args: ["test"],
                workingDirectory: workingDirectory
            ),
            ct
        );

        return new TestRunResponse(result);
    }

    public override string? HasCustomizations(string packagePath, CancellationToken ct = default)
    {
        // In azure-sdk-for-net, generated code lives in the Generated folder.
        // Customizations are partial types defined outside the Generated folder.
        // Example: sdk/ai/Azure.AI.DocumentIntelligence/src/
        //   - Generated/ (generated code)
        //   - Customized/ or other folders (customization code with partial classes)

        try
        {
            var srcDir = Path.Combine(packagePath, "src");
            if (!Directory.Exists(srcDir))
            {
                return null;
            }

            var generatedDirMarker = Path.DirectorySeparatorChar + GeneratedFolderName + Path.DirectorySeparatorChar;
            var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains(generatedDirMarker, StringComparison.OrdinalIgnoreCase));

            foreach (var file in csFiles)
            {
                try
                {
                    foreach (var line in File.ReadLines(file))
                    {
                        if (line.Contains("partial class"))
                        {
                            logger.LogDebug("Found .NET partial class in {FilePath}", file);
                            return srcDir;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read file {FilePath} for partial class detection", file);
                }
            }

            logger.LogDebug("No .NET partial classes found in {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for .NET customization files in {PackagePath}", packagePath);
            return null;
        }
    }
}
