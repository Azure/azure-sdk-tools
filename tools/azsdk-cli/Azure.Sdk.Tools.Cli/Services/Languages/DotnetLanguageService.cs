// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Xml.Linq;
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
    private static readonly TimeSpan CodeChecksTimeout = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan AotCompatTimeout = TimeSpan.FromMinutes(5);

    private readonly IPowershellHelper powershellHelper;

    public DotnetLanguageService(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
        IGitHelper gitHelper,        
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper)
    {
        this.powershellHelper = powershellHelper;
    }

    public override SdkLanguage Language { get; } = SdkLanguage.DotNet;
    /// <summary>
    /// Gets the default samples directory path relative to the package path.
    /// </summary>
    /// <param name="packagePath">The package path</param>
    /// <returns>The default samples directory path</returns>
    private static string GetDefaultSamplesDirectory(string packagePath) => Path.Combine(packagePath, "tests", "samples");

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving .NET package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, packagePath);
        var (packageName, packageVersion) = await TryGetPackageInfoAsync(fullPath, ct);
        
        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for .NET package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for .NET package at {fullPath}", fullPath);
        }
        
        var samplesDirectory = FindSamplesDirectory(fullPath);
        
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.DotNet,
            SamplesDirectory = samplesDirectory
        };
        
        logger.LogDebug("Resolved .NET package: {packageName} v{packageVersion} at {relativePath}", 
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);
        
        return model;
    }


    private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var csproj = Directory.GetFiles(Path.Combine(packagePath, "src"), "*.csproj").FirstOrDefault();

            if (csproj == null) 
            {
                logger.LogWarning("No .csproj file found in {packagePath}", packagePath);
                return (null, null); 
            }
            
            logger.LogTrace("Reading .csproj file: {csproj}", csproj);
            var content = await File.ReadAllTextAsync(csproj, ct);

            // Parse XML
            var doc = XDocument.Parse(content);
            
            // Extract name from PackageId, AssemblyName, or file name
            string? name = null;
            var packageId = doc.Descendants("PackageId").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(packageId))
            {
                name = packageId;
                logger.LogTrace("Found package name from PackageId: {name}", name);
            }
            else
            {
                var assemblyName = doc.Descendants("AssemblyName").FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    name = assemblyName;
                    logger.LogTrace("Found package name from AssemblyName: {name}", name);
                }
                else
                {
                    name = Path.GetFileNameWithoutExtension(csproj);
                    logger.LogTrace("Using file name as package name: {name}", name);
                }
            }

            // Extract version from Version, or VersionPrefix + VersionSuffix
            string? version = null;
            var versionElement = doc.Descendants("Version").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(versionElement))
            {
                version = versionElement;
                logger.LogTrace("Found version from Version tag: {version}", version);
            }
            else
            {
                var versionPrefix = doc.Descendants("VersionPrefix").FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(versionPrefix))
                {
                    var versionSuffix = doc.Descendants("VersionSuffix").FirstOrDefault()?.Value;
                    version = !string.IsNullOrWhiteSpace(versionSuffix) 
                        ? $"{versionPrefix}-{versionSuffix}" 
                        : versionPrefix;
                    logger.LogTrace("Found version from VersionPrefix/Suffix: {version}", version);
                }
                else
                {
                    logger.LogTrace("No version information found in .csproj");
                }
            }

            return (name, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading .NET package info from {packagePath}", packagePath);
            return (null, null);
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
        var result = await processHelper.Run(new ProcessOptions(
                command: "dotnet",
                args: ["test"],
                workingDirectory: packagePath
            ),
            ct
        );

        return new TestRunResponse(result);
    }

    public override List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
    {
        return categories.TryGetValue("dotnet", out var requirements) ? requirements : new List<SetupRequirements.Requirement>();
    }
}
