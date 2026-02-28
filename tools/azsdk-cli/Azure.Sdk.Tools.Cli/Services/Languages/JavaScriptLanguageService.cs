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
        IPackageInfoHelper packageInfoHelper,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, packageInfoHelper, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.npxHelper = npxHelper;
    }
    public override SdkLanguage Language { get; } = SdkLanguage.JavaScript;
    public override bool IsCustomizedCodeUpdateSupported => true;

    /// <summary>
    /// JavaScript packages are identified by package.json files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["package.json"];

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving JavaScript package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
        var (packageName, packageVersion, sdkType) = await TryGetPackageInfoAsync(fullPath, ct);

        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for JavaScript package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for JavaScript package at {fullPath}", fullPath);
        }
        if (sdkType == SdkType.Unknown)
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
                    sdkType = sdkTypeValue switch
                    {
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

    public override string? HasCustomizations(string packagePath, CancellationToken ct)
    {
        // In azure-sdk-for-js, the presence of a "generated" folder at the same level
        // as package.json indicates the package has customizations (code outside generated/).

        try
        {
            var generatedFolder = Path.Combine(packagePath, GeneratedFolderName);
            if (Directory.Exists(generatedFolder))
            {
                // If generated folder exists, customizations are everything outside it
                var srcDir = Path.Combine(packagePath, "src");
                if (Directory.Exists(srcDir))
                {
                    logger.LogDebug("Found JavaScript customization root at {SrcDir}", srcDir);
                    return srcDir;
                }
                // Fall back to package path if no src folder
                return packagePath;
            }

            logger.LogDebug("No JavaScript generated folder found in {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for JavaScript customization files in {PackagePath}", packagePath);
            return null;
        }
    }
}
