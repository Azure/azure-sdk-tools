// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class JavaScriptLanguageService : LanguageService
{
    private readonly INpxHelper npxHelper;

    public JavaScriptLanguageService(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,        
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper)
    {
        this.npxHelper = npxHelper;
    }
    public override SdkLanguage Language { get; } = SdkLanguage.JavaScript;

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving JavaScript package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, packagePath);
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
            Language = Models.SdkLanguage.JavaScript,
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
                logger.LogTrace("No sdkType property found in package.json");
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

    public override List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
    {
        return categories.TryGetValue("javascript", out var requirements) ? requirements : new List<SetupRequirements.Requirement>();
    }
}
