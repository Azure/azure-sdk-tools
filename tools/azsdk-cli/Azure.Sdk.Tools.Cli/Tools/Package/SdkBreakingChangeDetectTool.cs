using System.CommandLine;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    public class SdkBreakingChangeDetectTool : LanguageMcpTool
    {
        private readonly ISpecGenSdkConfigHelper _specGenSdkConfigHelper;
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private readonly ICopilotAgentRunner copilotAgentRunner;
        // Command names
        private const string DetectSdkBreakingChangeCommandName = "detect-breaking-change";
        private const string DetectSdkBreakingChangToolName = "azsdk_package_detect_breaking_change";

        private const string SdkChangeJsonFileName = "SDKCHANGE.json";
        // detect command options
        //private readonly Option<string> localSdkRepoPathOpt = new("--sdk-repo-path", "-r")
        //{
        //    Description = "Absolute path to the local azure-sdk-for-{language} repository",
        //    Required = true,
        //};

        private readonly Option<string> languageOpt = new("--language", "-l")
        {
            Description = "Language of the SDK, e.g. 'net' for .NET, 'java' for Java, 'js' for JavaScript/TypeScript, 'python' for Python, 'go' for Go",
            Required = false,
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
            ISpecGenSdkConfigHelper specGenSdkConfigHelper,
            ICopilotAgentRunner copilotAgentRunner) : base(languageServices, gitHelper, logger)
        {
            _specGenSdkConfigHelper = specGenSdkConfigHelper;
            this.copilotAgentRunner = copilotAgentRunner;
        }

        protected override Command GetCommand() =>
            new McpCommand(DetectSdkBreakingChangeCommandName, "Detects breaking changes in the SDK.", DetectSdkBreakingChangToolName)
            {
                SharedOptions.PackagePath, languageOpt, tspConfigPathOpt, generateSDKOpt,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            //var localRepoPath = parseResult.GetValue(localSdkRepoPathOpt);
            var language = parseResult.GetValue(languageOpt);
            var tspConfigPath = parseResult.GetValue(tspConfigPathOpt);
            var generateSDK = parseResult.GetValue(generateSDKOpt);

            return await DetectSDKBreakingChangesAsync(packagePath, language, tspConfigPath, generateSDK, ct);

        }

        [McpServerTool(Name = DetectSdkBreakingChangToolName), Description("Detects breaking changes in the SDK.")]
        public async Task<SdkBreakingChangeDetectResponse> DetectSDKBreakingChangesAsync(
            [Description("The absolute path to the package directory..")]
            string packagePath,
            [Description("Language of the SDK, e.g. 'net' for .NET, 'java' for Java, 'js' for JavaScript/TypeScript, 'python' for Python, 'go' for Go. Optional if running inside a local cloned azure-sdk-for-{language} repository.")]
            string? language = null,
            [Description("Path to the 'tspconfig.yaml' file. Can be a local file path or a remote HTTPS URL. Optional if running inside a local cloned azure-sdk-for-{language} repository, for example, inside 'azure-sdk-for-net' repository.")]
            string? tspConfigPath = null,
            [Description("Whether to generate SDK code before performing breaking change detection. If not specified, the tool will only compare the existing SDK.")]
            bool generateSDK = false,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Parameters: packagePath={PackagePath}, language={Language}, tspConfigPath={TspConfigPath}, generateSDK={GenerateSDK}",
                    packagePath, language ?? "null", tspConfigPath ?? "null", generateSDK);

                if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
                {
                    return new SdkBreakingChangeDetectResponse
                    {
                        ResponseError = $"The directory for the local sdk does not provide or exist at the specified path: {packagePath}. Prompt user to clone the matched SDK repository users want to generate SDK against."
                    };
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
                    return new SdkBreakingChangeDetectResponse
                    {
                        ResponseError = "Tooling error: unable to determine language service for the specified package path.",
                    };
                }
                // Discover the repository root
                var sdkRepoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
                if (sdkRepoRoot == null)
                {
                    return new SdkBreakingChangeDetectResponse
                    {
                        ResponseError = "Unable to find git repository root from the provided package path."
                    };
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
                            { "PackagePath", packagePath },
                            {"OutputJsonFile", Path.Combine(packagePath, SdkChangeJsonFileName) }
                        };

                        // Create and execute process options for the update-changelog-content script
                        var processOptions = _specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters);
                        if (processOptions != null)
                        {
                            var changelogResponse = await _specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "Changelog content is updated.", ["Review the changelog for accuracy and completeness", "Update metadata for the package"]);

                            // Fixed condition: proceed when there are NO errors (Count == 0 or null)
                            if (changelogResponse != null && (changelogResponse.ResponseErrors == null || changelogResponse.ResponseErrors.Count == 0))
                            {
                                var sdkChangeFilePath = Path.Combine(packagePath, SdkChangeJsonFileName);

                                if (!File.Exists(sdkChangeFilePath))
                                {
                                    logger.LogWarning("SDK change file not found at: {FilePath}", sdkChangeFilePath);
                                    return new SdkBreakingChangeDetectResponse
                                    {
                                        ResponseError = $"SDK change file not found: {sdkChangeFilePath}"
                                    };
                                }

                                // Read and deserialize the JSON file with proper disposal
                                using var fileStream = File.OpenRead(sdkChangeFilePath);
                                var sdkchanges = await JsonSerializer.DeserializeAsync<SdkChange>(fileStream, cancellationToken: ct);

                                if (sdkchanges != null)
                                {
                                    if (sdkchanges.HasBreakingChange)
                                    {
                                        return new SdkBreakingChangeDetectResponse
                                        {
                                            HasBreakingChanges = true,
                                            BreakingChanges = await ClassifySDKBreakingChanges(sdkchanges.ChangelogMD, sdkRepoRoot, languageService, ct),
                                            Language = languageService.Language,
                                        };
                                    }
                                    else
                                    {
                                        return new SdkBreakingChangeDetectResponse
                                        {
                                            ResponseError = "No breaking changes are detected in the SDK based on the changelog content."
                                        };
                                    }
                                }
                                else
                                {
                                    logger.LogWarning("Failed to deserialize the changelog update script output. Falling back to default logic to detect SDK breaking changes.");
                                }
                            }
                            else
                            {
                                logger.LogWarning("Changelog update script execution failed or returned errors.");
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
                return new SdkBreakingChangeDetectResponse
                {
                    ResponseError = $"An error occurred while detecting SDK breaking changes: {ex.Message}"
                };
            }
        }

        private async Task<SdkBreakingChange[]> ClassifySDKBreakingChanges(string sdkchange, string sdkRepoRoot, LanguageService languageService, CancellationToken ct)
        {
            var sdkBreakingPattern = await languageService.GetSDKBreakingPattern(sdkRepoRoot, ct);
            var agent = new CopilotAgent<string>
            {
                Instructions = BuildClassifyInstructions(sdkchange, sdkBreakingPattern, SdkLanguageHelpers.ToWorkItemString(languageService.Language)),
                Model = "claude-opus-4.5"
            };
            var result = await copilotAgentRunner.RunAsync(agent, ct);
            var breakings = ParseClassifyResult(result);
            //logger.LogInformation("copilot agent completed. hasBreakingChange: {hasBreakingChanges}, Breaking Changes: {breakingChanges}", result.HasBreakingChanges, string.Join("\n", result.BreakingChanges));
            // For demonstration purposes, we'll just return a response indicating no breaking changes were found
            return breakings;
        }

        private string BuildClassifyInstructions(string sdkchange, string sdkchangeToBreakingPattern, string language)
        {
            var template = new SdkBreakingChangeClassificationTemplate(sdkchangeToBreakingPattern, sdkchange, language);
            return template.BuildPrompt();
        }

        private SdkBreakingChange[] ParseClassifyResult(string result)
        {
            try
            {
                // Updated regex to capture the full breaking change line (everything until newline)
                // Changed from \S+ to [^\n]+ to capture the entire line including spaces
                Regex ResultBlockRex = new(
                    @"\[(?<id>[^\]]+)\]\s*\n\s*breaking:\s*(?<breaking>[^\n]+)\s*\n\s*category:\s*(?<category>[^\n]+)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
                var sdkBreakingChanges = new List<SdkBreakingChange>();
                foreach (Match match in ResultBlockRex.Matches(result))
                {
                    var id = match.Groups["id"].Value.Trim();
                    var breaking = match.Groups["breaking"].Value.Trim();
                    var category = match.Groups["category"].Value.Trim();

                    SdkBreakingChange breakingChange = new SdkBreakingChange
                    {
                        BreakingChange = breaking,
                        Category = category,
                    };
                    sdkBreakingChanges.Add(breakingChange);
                }
                return sdkBreakingChanges.ToArray();
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "JSON parsing error while parsing agent response");
                return Array.Empty<SdkBreakingChange>();
            }
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

    public class SdkBreakingChange
    {
        [JsonPropertyName("breakingchange")]
        [Required]
        public string BreakingChange { get; set; }
        [JsonPropertyName("category")]
        [Required]
        public string Category { get; set; }
    }
}
