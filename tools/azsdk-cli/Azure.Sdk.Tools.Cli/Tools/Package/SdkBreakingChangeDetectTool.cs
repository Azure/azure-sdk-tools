using System.CommandLine;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    public class SdkBreakingChangeDetectTool : LanguageMcpTool
    {
        private readonly ISpecGenSdkConfigHelper _specGenSdkConfigHelper;
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        // Command names
        private const string DetectSdkBreakingChangeCommandName = "detect-breaking-change";
        private const string DetectSdkBreakingChangToolName = "azsdk_package_detect_breaking_change";

        private const string SdkChangeJsonFileName = "SDKCHANGE.json";
        // detect command options
        private readonly Option<string> localSdkRepoPathOpt = new("--sdk-repo-path", "-r")
        {
            Description = "Absolute path to the local azure-sdk-for-{language} repository",
            Required = true,
        };

        private readonly Option<string> languageOpt = new("--language", "-l")
        {
            Description = "Language of the SDK, e.g. 'net' for .NET, 'java' for Java, 'js' for JavaScript/TypeScript, 'python' for Python, 'go' for Go",
            Required = true,
        };

        private readonly Option<string> tspConfigPathOpt = new("--tsp-config-path", "-t")
        {
            Description = "Path to the 'tspconfig.yaml' configuration file, it can be a local path or remote HTTPS URL",
            Required = false,
        };

        private readonly Option<bool> generateSDKOpt = new("--generate-sdk", "-g")
        {
            Description = "Whether to generate SDK code for the new API version before performing breaking change detection. If not specified, the tool will only compare the old and new API versions without generating SDK code.",
            Required = false,
            DefaultValueFactory = _ => false,
        };

        public SdkBreakingChangeDetectTool(
            IGitHelper gitHelper,
            ILogger<SdkBreakingChangeDetectTool> logger,
            IEnumerable<LanguageService> languageServices,
            ISpecGenSdkConfigHelper specGenSdkConfigHelper) : base(languageServices, gitHelper, logger)
        {
            _specGenSdkConfigHelper = specGenSdkConfigHelper;
        }

        protected override Command GetCommand() =>
            new McpCommand(DetectSdkBreakingChangeCommandName, "Detects breaking changes in the SDK.", DetectSdkBreakingChangToolName)
            {
                localSdkRepoPathOpt, SharedOptions.PackagePath, languageOpt, tspConfigPathOpt, generateSDKOpt,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            var localRepoPath = parseResult.GetValue(localSdkRepoPathOpt);
            var language = parseResult.GetValue(languageOpt);
            var tspConfigPath = parseResult.GetValue(tspConfigPathOpt);
            var generateSDK = parseResult.GetValue(generateSDKOpt);

            return await DetectSDKBreakingChangesAsync(packagePath, language, tspConfigPath, generateSDK, ct);

        }

        [McpServerTool(Name = DetectSdkBreakingChangToolName), Description("Detects breaking changes in the SDK.")]
        public async Task<SdkBreakingChangeDetectResponse> DetectSDKBreakingChangesAsync(
            [Description("The absolute path to the package directory..")]
            string packagePath,
            [Description("Language of the SDK, e.g. 'net' for .NET, 'java' for Java, 'js' for JavaScript/TypeScript, 'python' for Python, 'go' for Go. REQUIRED.")]
            string language,
            [Description("Path to the 'tspconfig.yaml' file. Can be a local file path or a remote HTTPS URL. Optional if running inside a local cloned azure-sdk-for-{language} repository, for example, inside 'azure-sdk-for-net' repository.")]
            string? tspConfigPath,
            [Description("Whether to generate SDK code before performing breaking change detection. If not specified, the tool will only compare the existing SDK.")]
            bool generateSDK,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
                {
                    return SdkBreakingChangeDetectResponse.CreateFailure($"The directory for the local sdk does not provide or exist at the specified path: {packagePath}. Prompt user to clone the matched SDK repository users want to generate SDK against.");
                }
                LanguageService languageService;
                if (!string.IsNullOrEmpty(language))
                {
                    languageService = GetLanguageService(SdkLanguageHelpers.GetSdkLanguage(language));
                }
                else
                {
                    languageService = await GetLanguageServiceAsync(packagePath, ct);
                }
                if (languageService == null)
                {
                    return SdkBreakingChangeDetectResponse.CreateFailure("Tooling error: unable to determine language service for the specified package path.", nextSteps: ["Create an issue at the https://github.com/Azure/azure-sdk-tools/issues/new", "contact the Azure SDK team for assistance."]);
                }
                // Discover the repository root
                var sdkRepoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
                if (sdkRepoRoot == null)
                {
                    return SdkBreakingChangeDetectResponse.CreateFailure("Unable to find git repository root from the provided package path.");
                }
                var packageInfo = await languageService.GetPackageInfo(packagePath, ct);
                if (packageInfo?.SdkType == SdkType.Management)
                {
                    // For management-plane packages, execute configured changelog update script
                    var (configContentType, configValue) = await _specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.GetSDKChanges, ct);
                    if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrEmpty(configValue))
                    {
                        logger.LogInformation("Found valid configuration for updating changelog content. Executing configured script...");

                        // Prepare script parameters
                        var scriptParameters = new Dictionary<string, string>
                        {
                            { "SdkRepoPath", sdkRepoRoot },
                            { "PackagePath", packagePath }
                        };

                        // Create and execute process options for the update-changelog-content script
                        var processOptions = _specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters);
                        if (processOptions != null)
                        {
                            logger.LogInformation("Executing changelog update script: {Script}, with parameters: {Parameters}", processOptions.Command, processOptions.WorkingDirectory);
                            var changelogResponse = await _specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "Changelog content is updated.", ["Review the changelog for accuracy and completeness", "Update metadata for the package"]);
                            if (changelogResponse != null && (changelogResponse.ResponseErrors == null || changelogResponse.ResponseErrors.Count > 0))
                            {
                                var fileStream = File.OpenRead(Path.Combine(packagePath, SdkChangeJsonFileName));
                                var sdkchanges = await JsonSerializer.DeserializeAsync<SdkChange>(fileStream, cancellationToken: ct);
                                if (sdkchanges != null)
                                {
                                    if (sdkchanges.HasBreakingChange)
                                    {
                                        return await ClassifySDKBreakingChanges(sdkchanges.ChangelogMD, ct);
                                    }
                                    else
                                    {
                                        return PackageOperationResponse.CreateSuccess("No breaking changes are detected in the SDK based on the changelog content.", packageInfo);

                                    }
                                }
                                else
                                {
                                    logger.LogWarning("Failed to deserialize the changelog update script output. Falling back to default logic to detect SDK breaking changes.");
                                }
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
                return PackageOperationResponse.CreateFailure($"An error occurred while detecting SDK breaking changes: {ex.Message}");
            }
        }

        private async Task<SdkBreakingChangeDetectResponse> ClassifySDKBreakingChanges(string sdkchange, CancellationToken cancellationToken)
        {
            return await Task.FromResult(SdkBreakingChangeDetectResponse.CreateSuccess("Breaking change classification is not implemented yet.", result: sdkchange));
        }
    }

    internal class SdkChange
    {
        [JsonPropertyName("changelog_md")]
        [Required]
        public string ChangelogMD { get; set; }
        [JsonPropertyName("hasBreakingChange")]
        [Required]
        public bool HasBreakingChange { get; set; }
    }
}
