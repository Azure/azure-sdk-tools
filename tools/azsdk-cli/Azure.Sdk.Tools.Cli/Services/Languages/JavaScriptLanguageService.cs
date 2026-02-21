// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class JavaScriptLanguageService : LanguageService
{
    private const string GeneratedFolderName = "generated";
    private readonly INpxHelper npxHelper;

    public JavaScriptLanguageService(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,        
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.npxHelper = npxHelper;
    }
    public override SdkLanguage Language { get; } = SdkLanguage.JavaScript;
    public override bool IsCustomizedCodeUpdateSupported => true;

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving JavaScript package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await PackagePathParser.ParseAsync(gitHelper, packagePath, ct);
        var (packageName, packageVersion, sdkType) = await TryGetPackageInfoAsync(fullPath, ct);
        
        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for JavaScript package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for JavaScript package at {fullPath}", fullPath);
        }
        if(sdkType == SdkType.Unknown)
        {
            logger.LogWarning("Could not determine SDK type for JavaScript package at {fullPath}", fullPath);
        }
        
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            SdkType = sdkType,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = SdkLanguage.JavaScript,
            SamplesDirectory = Path.Combine(fullPath, "samples-dev")
        };
        
        logger.LogDebug("Resolved JavaScript package: {packageName} v{packageVersion} (type {sdkType}) at {relativePath}", 
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", sdkType, relativePath);
        
        return model;
    }

    private async Task<(string? Name, string? Version, SdkType sdkType)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var path = Path.Combine(packagePath, "package.json");
            if (!File.Exists(path)) 
            {
                logger.LogWarning("No package.json file found at {path}", path);
                return (null, null, SdkType.Unknown); 
            }
            
            logger.LogTrace("Reading package.json from {path}", path);
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }, ct);

            string? name = null;
            string? version = null;
            SdkType sdkType = SdkType.Unknown;

            if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var nameValue = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(nameValue)) 
                { 
                    name = nameValue;
                    logger.LogTrace("Found package name: {name}", name);
                }
            }
            else
            {
                logger.LogTrace("No name property found in package.json");
            }

            if (doc.RootElement.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == JsonValueKind.String)
            {
                var versionValue = versionProp.GetString();
                if (!string.IsNullOrWhiteSpace(versionValue)) 
                { 
                    version = versionValue;
                    logger.LogTrace("Found version: {version}", version);
                }
            }
            else
            {
                logger.LogTrace("No version property found in package.json");
            }

            if (doc.RootElement.TryGetProperty("sdk-type", out var sdkTypeProp) && sdkTypeProp.ValueKind == JsonValueKind.String)
            {
                var sdkTypeValue = sdkTypeProp.GetString();
                if (!string.IsNullOrWhiteSpace(sdkTypeValue)) 
                { 
                    sdkType = sdkTypeValue switch {
                        "client" => SdkType.Dataplane,
                        "mgmt" => SdkType.Management,
                        _ => SdkType.Unknown,
                    };
                    logger.LogTrace("Found SDK type: {sdkType}", sdkType);
                }
            }
            else
            {
                logger.LogTrace("No sdk-type property found in package.json");
            }

            return (name, version, sdkType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading JavaScript package info from {packagePath}", packagePath);
            return (null, null, SdkType.Unknown);
        }
    }

    public override async Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var result = await processHelper.Run(new ProcessOptions(
                command: "npm",
                args: ["run", "test"],
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
            logger.LogInformation("Packing JavaScript SDK project at: {PackagePath}", packagePath);

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

            var args = new List<string> { "pack" };
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                args.AddRange(["--pack-destination", outputPath]);
            }

            var result = await processHelper.Run(new ProcessOptions(
                    command: "pnpm",
                    args: args.ToArray(),
                    workingDirectory: fullPath,
                    timeout: TimeSpan.FromMinutes(timeoutMinutes)
                ),
                ct
            );

            if (result.ExitCode != 0)
            {
                var errorMessage = $"pnpm pack failed with exit code {result.ExitCode}. Output:\n{result.Output}";
                logger.LogError("{ErrorMessage}", errorMessage);
                return (false, errorMessage, packageInfo, null);
            }

            // pnpm pack outputs the tarball filename to stdout
            var artifactPath = ExtractTarballPath(result.Output, fullPath, outputPath);
            logger.LogInformation("Pack completed successfully. Artifact: {ArtifactPath}", artifactPath ?? "(unknown)");
            return (true, null, packageInfo, artifactPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while packing JavaScript SDK");
            return (false, $"An error occurred: {ex.Message}", null, null);
        }
    }

    /// <summary>
    /// Attempts to extract the .tgz file path from pnpm pack output.
    /// </summary>
    private static string? ExtractTarballPath(string output, string packagePath, string? outputPath)
    {
        // pnpm pack typically outputs the tarball filename
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                var dir = outputPath ?? packagePath;
                var candidatePath = Path.Combine(dir, trimmed);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
                // The line itself might be a full path
                if (File.Exists(trimmed))
                {
                    return trimmed;
                }
            }
        }

        // Fallback: look for .tgz files in the directory
        var searchDir = outputPath ?? packagePath;
        if (Directory.Exists(searchDir))
        {
            var tgzFiles = Directory.GetFiles(searchDir, "*.tgz", SearchOption.TopDirectoryOnly);
            if (tgzFiles.Length > 0)
            {
                return tgzFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
            }
        }

        return null;
    }

    public override bool HasCustomizations(string packagePath, CancellationToken ct)
    {
        // In azure-sdk-for-js, the presence of a "generated" folder at the same level
        // as package.json indicates the package has customizations (code outside generated/).

        try
        {
            var generatedFolder = Path.Combine(packagePath, GeneratedFolderName);
            if (Directory.Exists(generatedFolder))
            {
                logger.LogDebug("Found JavaScript generated folder at {GeneratedFolder}", generatedFolder);
                return true;
            }

            logger.LogDebug("No JavaScript generated folder found in {PackagePath}", packagePath);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for JavaScript customization files in {PackagePath}", packagePath);
            return false;
        }
    }
}
