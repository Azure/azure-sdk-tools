// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tool to detect sdk breaking changes for a package.")]
    public class SdkBreakingChangeDetectTool : LanguageMcpTool
    {
        private readonly ISpecGenSdkConfigHelper _specGenSdkConfigHelper;
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private readonly ISdkBreakingChangeClassificationService _classifyService;
        // Command names
        private const string DetectSdkBreakingChangeCommandName = "detect-breaking-change";
        private const string DetectSdkBreakingChangeToolName = "azsdk_package_detect_breaking_change";

        private const string SdkChangeJsonFileName = "sdk-changes.json";

        // detect command options
        public static Option<string> PackagePathOpt = new("--package-path", "-p")
        {
            Description = "Path to the package directory to check.",
            Required = true,
        };
        private readonly Option<string> tspConfigPathOpt = new("--tsp-config-path", "-t")
        {
            Description = "Path to the 'tspconfig.yaml' configuration file, it can be a local path or remote HTTPS URL",
            Required = false,
        };

        private readonly Option<bool> changesOnlyOpt = new("--changes-only")
        {
            Description = "Detect SDK changes only, without analyzing or classifying them.",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        private readonly Option<string> sdkChangeJsonFilePathOpt = new("--sdk-change-json-file-path", "-s")
        {
            Description = "Path to the SDK change json file. It is a local file path. Optional.",
            Required = false,
        };
        public SdkBreakingChangeDetectTool(
            IGitHelper gitHelper,
            ILogger<SdkBreakingChangeDetectTool> logger,
            IEnumerable<LanguageService> languageServices,
            ISpecGenSdkConfigHelper specGenSdkConfigHelper,
            ISdkBreakingChangeClassificationService classifyService) : base(languageServices, gitHelper, logger)
        {
            _specGenSdkConfigHelper = specGenSdkConfigHelper;
            _classifyService = classifyService;
        }

        protected override Command GetCommand() =>
            new McpCommand(DetectSdkBreakingChangeCommandName, "Detects breaking changes in the SDK.", DetectSdkBreakingChangeToolName)
            {
                PackagePathOpt, tspConfigPathOpt, changesOnlyOpt, sdkChangeJsonFilePathOpt,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(PackagePathOpt);
            var tspConfigPath = parseResult.GetValue(tspConfigPathOpt);
            var changesOnly = parseResult.GetValue(changesOnlyOpt);
            var sdkChangeJsonFilePath = parseResult.GetValue(sdkChangeJsonFilePathOpt);

            return await DetectSDKBreakingChangesAsync(packagePath, tspConfigPath, changesOnly, sdkChangeJsonFilePath, ct);

        }

        [McpServerTool(Name = DetectSdkBreakingChangeToolName), Description("Detects breaking changes in the SDK.")]
        public async Task<PackageOperationResponse> DetectSDKBreakingChangesAsync(
            [Description("The absolute path to the package directory. REQUIRED. Example: 'path/to/azure-sdk-for-go/sdk/resourcemanager/webpubsub/armwebpubsub'")]
            string packagePath,
            [Description("Path to the 'tspconfig.yaml' file. It is a local file path. Optional.")]
            string? tspConfigPath = null,
            [Description("Detect SDK changes only, without analyzing or classifying them.")]
            bool changesOnly = false,
            [Description("Path to the SDK change json file. It is a local file path. Optional.")]
            string? localSdkChangeJsonFilePath = null,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Parameters: packagePath={PackagePath}, tspConfigPath={TspConfigPath}, changesOnly={ChangesOnly}, localSdkChangeJsonFilePath={LocalSdkChangeJsonFilePath}",
                    packagePath, tspConfigPath ?? "null", changesOnly, localSdkChangeJsonFilePath ?? "null");

                if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
                {
                    return new PackageOperationResponse
                    {
                        ResponseError = $"The directory for the local sdk does not provide or exist at the specified path: {packagePath}. Prompt user to clone the matched SDK repository users want to generate SDK against."
                    };
                }

                LanguageService languageService = await GetLanguageServiceAsync(packagePath, ct);

                if (languageService == null)
                {
                    return new PackageOperationResponse
                    {
                        ResponseError = "Tooling error: unable to determine language service for the specified package path.",
                    };
                }
                // Discover the repository root
                var sdkRepoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
                if (sdkRepoRoot == null)
                {
                    return new PackageOperationResponse
                    {
                        ResponseError = "Unable to find git repository root from the provided package path."
                    };
                }
                var packageInfo = await languageService.GetPackageInfo(packagePath, ct);
                SdkChange? sdkChange = null;
                if (!string.IsNullOrEmpty(localSdkChangeJsonFilePath))
                {
                    logger.LogInformation("Using local SDK change JSON file at: {FilePath}", localSdkChangeJsonFilePath);

                    if (!File.Exists(localSdkChangeJsonFilePath))
                    {
                        logger.LogInformation("Local SDK change JSON file not found at: {FilePath}. Proceeding to retrieve SDK changes using the configured script.", localSdkChangeJsonFilePath);
                    }
                    else
                    {
                        // Read and deserialize the local SDK change JSON file
                        try
                        {
                            await using (var fileStream = File.OpenRead(localSdkChangeJsonFilePath))
                            {
                                sdkChange = await JsonSerializer.DeserializeAsync<SdkChange>(
                                    fileStream,
                                    cancellationToken: ct);
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            logger.LogError(jsonEx, "Failed to deserialize the local SDK change JSON file {FilePath}. Proceeding to retrieve SDK changes using the configured script.", localSdkChangeJsonFilePath);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "An unexpected error occurred while reading the local SDK change JSON file {FilePath}. Proceeding to retrieve SDK changes using the configured script.", localSdkChangeJsonFilePath);
                        }
                    }
                }

                if (sdkChange == null)
                {
                    logger.LogInformation("Retrieve SDK changes using the configured script.");
                    // execute configured sdk change retrieve script
                    var (configContentType, configValue) = await _specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.GetSdkChanges, ct);
                    if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrEmpty(configValue))
                    {
                        logger.LogInformation("Found valid configuration for getting sdk changes. Executing configured script...");

                        // Prepare script parameters
                        string tempDir = Path.GetTempPath();
                        // The SDK change file path is constructed using the temporary directory, service name, a new GUID, language and the SDK change JSON file name.
                        string sdkChangeFileName = $"{packageInfo.ServiceName ?? "unknownService"}-{Guid.NewGuid().ToString("N")}-{languageService.Language}-{SdkChangeJsonFileName}";
                        var sdkChangeFilePath = Path.Combine(tempDir, sdkChangeFileName);
                        var scriptParameters = new Dictionary<string, string>
                        {
                            { "SdkRepoPath", sdkRepoRoot },
                            { "PackagePath", packagePath },
                            {"OutputJsonFile", sdkChangeFilePath }
                        };

                        // Create and execute process options for the get-sdk-changes script
                        var processOptions = _specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters);
                        if (processOptions != null)
                        {
                            var sdkChangeResponse = await _specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "SDK changes are retrieved.");

                            // Fixed condition: proceed when there are NO errors (Count == 0 or null)
                            if (sdkChangeResponse != null && (sdkChangeResponse.ResponseErrors == null || sdkChangeResponse.ResponseErrors.Count == 0))
                            {
                                if (!File.Exists(sdkChangeFilePath))
                                {
                                    logger.LogWarning("SDK change file not found at: {FilePath}", sdkChangeFilePath);
                                    return new PackageOperationResponse
                                    {
                                        ResponseError = $"SDK change file not found: {sdkChangeFilePath}"
                                    };
                                }
                                try
                                {
                                    // Read and deserialize the JSON file with proper disposal
                                    await using (var fileStream = File.OpenRead(sdkChangeFilePath))
                                    {
                                        sdkChange = await JsonSerializer.DeserializeAsync<SdkChange>(
                                            fileStream,
                                            cancellationToken: ct);
                                    }

                                    // clean up the SDK change file after reading
                                    File.Delete(sdkChangeFilePath);
                                }
                                catch (JsonException jsonEx)
                                {
                                    logger.LogError(jsonEx, "Failed to deserialize the SDK change script output.");
                                    return new PackageOperationResponse
                                    {
                                        ResponseError = "Failed to deserialize the SDK change script output.",
                                        Language = languageService.Language,
                                        PackageName = packageInfo?.PackageName,
                                    };
                                }
                            }
                            else
                            {
                                logger.LogError("Failed to retrieve SDK changes using the configured script. Errors: {Errors}", sdkChangeResponse?.ResponseErrors);
                                return new PackageOperationResponse
                                {
                                    ResponseError = "Failed to retrieve SDK changes using the configured script.",
                                    Language = languageService.Language,
                                    PackageName = packageInfo?.PackageName,
                                };
                            }
                        }
                    }
                }

                if (sdkChange != null)
                {
                    if (sdkChange.HasBreakingChange && !changesOnly)
                    {
                        var tspProjectPath = tspConfigPath != null ? Path.GetDirectoryName(tspConfigPath) : null;
                        var sdkBreakingPattern = await languageService.GetSdkBreakingPattern(sdkRepoRoot, ct);
                        var sdkBreakingChanges = await _classifyService.ClassifySdkBreakingChangesAsync(sdkChange.SdkChangeMD, sdkBreakingPattern, languageService.Language.ToString(), tspProjectPath, ct);
                        if (sdkBreakingChanges.Count == 0)
                        {
                            logger.LogError("Failed to classify SDK breaking changes.");
                            return new PackageOperationResponse
                            {
                                ResponseError = "Failed to classify SDK breaking changes.",
                                Language = languageService.Language,
                                PackageName = packageInfo?.PackageName,
                            };
                        }
                        var result = new SdkBreakingChangeDetectResult
                        {
                            HasBreakingChange = true,
                            BreakingChanges = sdkBreakingChanges,
                        };
                        return new PackageOperationResponse()
                        {
                            Result = result,
                            Message = "SDK breaking changes detected and classified.",
                            Language = languageService.Language,
                            PackageName = packageInfo?.PackageName,
                        };
                    }
                    else
                    {
                        var result = new SdkBreakingChangeDetectResult
                        {
                            HasBreakingChange = sdkChange.HasBreakingChange,
                            BreakingChanges = [],
                            SdkChangeMD = sdkChange.SdkChangeMD,
                        };

                        return new PackageOperationResponse()
                        {
                            Result = result,
                            Message = sdkChange.HasBreakingChange ? "SDK changes detected. Breaking change classification skipped as per the 'changes-only' option." : "No SDK breaking changes detected.",
                            Language = languageService.Language,
                            PackageName = packageInfo?.PackageName,
                        };
                    }
                }
                
                // Run default logic to detect SDK breaking changes
                logger.LogInformation("Running default logic to detect SDK breaking changes for the package...");
                return await languageService.DetectSdkBreakingChangeAsync(packagePath, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while detecting SDK breaking changes.");
                return new PackageOperationResponse
                {
                    ResponseError = $"An error occurred while detecting SDK breaking changes: {ex.Message}"
                };
            }
        }
    }

    internal class SdkChange
    {
        [JsonPropertyName("changes")]
        [JsonRequired]
        public string SdkChangeMD { get; set; }
        [JsonPropertyName("hasBreakingChange")]
        [JsonRequired]
        public bool HasBreakingChange { get; set; }
    }
}
