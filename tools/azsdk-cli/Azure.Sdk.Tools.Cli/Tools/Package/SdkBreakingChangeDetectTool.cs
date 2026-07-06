// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ClassifyItems;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    public class SdkBreakingChangeDetectTool : LanguageMcpTool
    {
        private readonly ISpecGenSdkConfigHelper _specGenSdkConfigHelper;
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private readonly IClassifyService _classifyService;
        // Command names
        private const string DetectSdkBreakingChangeCommandName = "detect-breaking-change";
        private const string DetectSdkBreakingChangToolName = "azsdk_package_detect_breaking_change";

        private const string SdkChangeJsonFileName = "SDKCHANGE.json";

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
        public SdkBreakingChangeDetectTool(
            IGitHelper gitHelper,
            ILogger<SdkBreakingChangeDetectTool> logger,
            IEnumerable<LanguageService> languageServices,
            ISpecGenSdkConfigHelper specGenSdkConfigHelper,
            IClassifyService classifyService) : base(languageServices, gitHelper, logger)
        {
            _specGenSdkConfigHelper = specGenSdkConfigHelper;
            _classifyService = classifyService;
        }

        protected override Command GetCommand() =>
            new McpCommand(DetectSdkBreakingChangeCommandName, "Detects breaking changes in the SDK.", DetectSdkBreakingChangToolName)
            {
                PackagePathOpt, tspConfigPathOpt, changesOnlyOpt,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(PackagePathOpt);
            var tspConfigPath = parseResult.GetValue(tspConfigPathOpt);
            var changesOnly = parseResult.GetValue(changesOnlyOpt);

            return await DetectSDKBreakingChangesAsync(packagePath, tspConfigPath, changesOnly, ct);

        }

        [McpServerTool(Name = DetectSdkBreakingChangToolName), Description("Detects breaking changes in the SDK.")]
        public async Task<PackageOperationResponse> DetectSDKBreakingChangesAsync(
            [Description("The absolute path to the package directory. REQUIRED. Example: 'path/to/azure-sdk-for-go/sdk/resourcemanager/webpubsub/armwebpubsub'")]
            string packagePath,
            [Description("Path to the 'tspconfig.yaml' file. It is a local file path. Optional.")]
            string? tspConfigPath = null,
            [Description("Detect SDK changes only, without analyzing or classifying them.")]
            bool changesOnly = false,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Parameters: packagePath={PackagePath}, tspConfigPath={TspConfigPath}, changesOnly={ChangesOnly}",
                    packagePath, tspConfigPath ?? "null", changesOnly);

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
                // TODO: remove the following check when .net SDKs is ready
                if (languageService.Language != SdkLanguage.DotNet)
                {
                    // execute configured sdk change retrieve script
                    var (configContentType, configValue) = await _specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.GetSDKChanges, ct);
                    if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrEmpty(configValue))
                    {
                        logger.LogInformation("Found valid configuration for getting sdk changes. Executing configured script...");

                        // Prepare script parameters
                        var scriptParameters = new Dictionary<string, string>
                        {
                            { "SdkRepoPath", sdkRepoRoot },
                            { "PackagePath", packagePath },
                            {"OutputJsonFile", Path.Combine(packagePath, SdkChangeJsonFileName) }
                        };

                        // Create and execute process options for the get-sdk-changes script
                        var processOptions = _specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters);
                        if (processOptions != null)
                        {
                            var sdkChangeResponse = await _specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "SDK changes are retrieved.");

                            // Fixed condition: proceed when there are NO errors (Count == 0 or null)
                            if (sdkChangeResponse != null && (sdkChangeResponse.ResponseErrors == null || sdkChangeResponse.ResponseErrors.Count == 0))
                            {
                                var sdkChangeFilePath = Path.Combine(packagePath, SdkChangeJsonFileName);

                                if (!File.Exists(sdkChangeFilePath))
                                {
                                    logger.LogWarning("SDK change file not found at: {FilePath}", sdkChangeFilePath);
                                    return new PackageOperationResponse
                                    {
                                        ResponseError = $"SDK change file not found: {sdkChangeFilePath}"
                                    };
                                }

                                // Read and deserialize the JSON file with proper disposal
                                using var fileStream = File.OpenRead(sdkChangeFilePath);
                                var sdkchanges = await JsonSerializer.DeserializeAsync<SdkChange>(fileStream, cancellationToken: ct);

                                // clean up the SDK change file after reading
                                fileStream.Close();
                                File.Delete(sdkChangeFilePath);

                                if (sdkchanges != null)
                                {
                                    if (sdkchanges.HasBreakingChange && !changesOnly)
                                    {
                                        var tspProjectPath = tspConfigPath != null ? await gitHelper.DiscoverRepoRootAsync(tspConfigPath, ct) : null;
                                        var sdkBreakingPattern = await languageService.GetSDKBreakingPattern(sdkRepoRoot, ct);
                                        var classifyRequest = new ClassifySdkBreakingChangesRequest(sdkchanges.ChangelogMD, sdkRepoRoot, sdkBreakingPattern, languageService.Language.ToString(), tspProjectPath);
                                        var classifyResult = await _classifyService.ClassifyItemsAsync(ClassificationKind.SdkBreakingChange, classifyRequest, ct);
                                        if (classifyResult == null || classifyResult.ClassifiedResult == null)
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
                                            HasBreakingChanges = true,
                                            BreakingChanges = classifyResult.ClassifiedResult as List<SdkBreakingChange> ?? new List<SdkBreakingChange>(),
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
                                            HasBreakingChanges = sdkchanges.HasBreakingChange,
                                            BreakingChanges = [],
                                            SdkChangesMd = sdkchanges.ChangelogMD,
                                        };
                                        return new PackageOperationResponse()
                                        {
                                            Result = result,
                                            Message = sdkchanges.HasBreakingChange ? "SDK changes detected but no breaking changes found." : "No SDK breaking changes detected.",
                                            Language = languageService.Language,
                                            PackageName = packageInfo?.PackageName,
                                        };
                                    }
                                }
                                else
                                {
                                    logger.LogError("Failed to deserialize the SDK change script output. Falling back to default logic to detect SDK breaking changes.");
                                    return new PackageOperationResponse
                                    {
                                        ResponseError = "Failed to deserialize the SDK change script output. Falling back to default logic to detect SDK breaking changes.",
                                        Language = languageService.Language,
                                        PackageName = packageInfo?.PackageName,
                                    };
                                }
                            }
                            else
                            {
                                logger.LogError("SDK change script execution failed or returned errors.");
                                return new PackageOperationResponse
                                {
                                    ResponseError = $"SDK change script execution failed or returned errors: {string.Join("; ", sdkChangeResponse?.ResponseErrors ?? new List<string> { "Unknown error" })}",
                                    Language = languageService.Language,
                                };
                            }
                        }
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
        [Required]
        public string ChangelogMD { get; set; }
        [JsonPropertyName("hasBreakingChange")]
        [Required]
        public bool HasBreakingChange { get; set; }
    }

    public class SdkBreakingChange
    {
        [JsonPropertyName("breakingChange")]
        [Required]
        public string BreakingChange { get; set; }
        [JsonPropertyName("category")]
        [Required]
        public string Category { get; set; }
        [JsonPropertyName("resolution")]
        public string? Resolution { get; set; }
        [JsonPropertyName("originBreaks")]
        public List<string>? OriginBreaks { get; set; }
    }
    public class SdkBreakingChangeDetectResult
    {
        [JsonPropertyName("breakingChanges")]
        public List<SdkBreakingChange> BreakingChanges { get; set; } = new List<SdkBreakingChange>();
        [JsonPropertyName("hasBreakingChanges")]
        public bool HasBreakingChanges { get; set; }
        [JsonPropertyName("SdkChangesMd")]
        public string? SdkChangesMd { get; set; } = null;
    }
}
