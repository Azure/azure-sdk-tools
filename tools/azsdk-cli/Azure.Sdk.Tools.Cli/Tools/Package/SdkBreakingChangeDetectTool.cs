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
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    public class SdkBreakingChangeDetectTool : LanguageMcpTool
    {
        private readonly ISpecGenSdkConfigHelper _specGenSdkConfigHelper;
        private readonly ITspClientHelper _tspClientHelper;
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private readonly ICopilotAgentRunner copilotAgentRunner;
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
            ITspClientHelper tspClientHelper,
            ICopilotAgentRunner copilotAgentRunner) : base(languageServices, gitHelper, logger)
        {
            _specGenSdkConfigHelper = specGenSdkConfigHelper;
            _tspClientHelper = tspClientHelper;
            this.copilotAgentRunner = copilotAgentRunner;
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
        public async Task<SdkBreakingChangeDetectResponse> DetectSDKBreakingChangesAsync(
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
                var gitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (!string.IsNullOrWhiteSpace(gitHubToken))
                {
                    logger.LogInformation("Using GITHUB_TOKEN from environment variable for Copilot SDK authentication.");
                }
                else
                {
                    // If no GITHUB_TOKEN is provided, log a warning. Some Copilot features may not work properly without it.
                    logger.LogWarning("No GITHUB_TOKEN environment variable found. Some Copilot features may not work properly.");
                }
                logger.LogInformation("Parameters: packagePath={PackagePath}, tspConfigPath={TspConfigPath}, changesOnly={ChangesOnly}",
                    packagePath, tspConfigPath ?? "null", changesOnly);

                if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
                {
                    return new SdkBreakingChangeDetectResponse
                    {
                        ResponseError = $"The directory for the local sdk does not provide or exist at the specified path: {packagePath}. Prompt user to clone the matched SDK repository users want to generate SDK against."
                    };
                }

                
                LanguageService languageService = await GetLanguageServiceAsync(packagePath, ct);

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
                                    return new SdkBreakingChangeDetectResponse
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
                                        return new SdkBreakingChangeDetectResponse
                                        {
                                            HasBreakingChanges = true,
                                            BreakingChanges = await ClassifySDKBreakingChanges(sdkchanges.ChangelogMD, sdkRepoRoot, languageService, tspProjectPath, ct),
                                            Language = languageService.Language,
                                        };
                                    }
                                    else
                                    {
                                        return new SdkBreakingChangeDetectResponse
                                        {
                                            HasBreakingChanges = sdkchanges.HasBreakingChange,
                                            BreakingChanges = [],
                                            SdkChangesMd = sdkchanges.ChangelogMD,
                                            Language = languageService.Language,
                                        };
                                    }
                                }
                                else
                                {
                                    logger.LogError("Failed to deserialize the SDK change script output. Falling back to default logic to detect SDK breaking changes.");
                                    return new SdkBreakingChangeDetectResponse
                                    {
                                        ResponseError = "Failed to deserialize the SDK change script output. Falling back to default logic to detect SDK breaking changes.",
                                        Language = languageService.Language,
                                    };
                                }
                            }
                            else
                            {
                                logger.LogError("SDK change script execution failed or returned errors.");
                                return new SdkBreakingChangeDetectResponse
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
                return new SdkBreakingChangeDetectResponse
                {
                    ResponseError = $"An error occurred while detecting SDK breaking changes: {ex.Message}"
                };
            }
        }

        private async Task<SdkBreakingChange[]> ClassifySDKBreakingChanges(string sdkchange, string sdkRepoRoot, LanguageService languageService, string? tspProjectPath, CancellationToken ct)
        {
            var sdkBreakingPattern = await languageService.GetSDKBreakingPattern(sdkRepoRoot, ct);
            var agent = new CopilotAgent<string>
            {
                Instructions = BuildClassifyInstructions(sdkchange, sdkBreakingPattern, SdkLanguageHelpers.ToWorkItemString(languageService.Language), tspProjectPath),
                Model = "claude-opus-4.5"
            };
            var result = await copilotAgentRunner.RunAsync(agent, ct);
            var breakings = ParseClassifyResult(result);

            return breakings;
        }

        private string BuildClassifyInstructions(string sdkchange, string sdkchangeToBreakingPattern, string language, string tspProjectPath)
        {
            var template = new SdkBreakingChangeClassificationTemplate(sdkchangeToBreakingPattern, sdkchange, language, tspProjectPath);
            return template.BuildPrompt();
        }

        private SdkBreakingChange[] ParseClassifyResult(string result)
        {
            try
            {
                // Updated regex to capture the full breaking change line (everything until newline)
                // Changed from \S+ to [^\n]+ to capture the entire line including spaces
                Regex ResultBlockRex = new(
                    @"\[(?<id>[^\]]+)\]\s*\n\s*breaking:\s*(?<breaking>[^\n]+)\s*\n\s*category:\s*(?<category>[^\n]+)\s*\n\s*resolution:\s*(?<resolution>[^\n]*)\s*\n\s*originBreaks:\s*(?<originBreaks>[^\n]*)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
                var sdkBreakingChanges = new List<SdkBreakingChange>();
                foreach (Match match in ResultBlockRex.Matches(result))
                {
                    var id = match.Groups["id"].Value.Trim();
                    var breaking = match.Groups["breaking"].Value.Trim();
                    var category = match.Groups["category"].Value.Trim();
                    var resolution = match.Groups["resolution"].Value.Trim();
                    var originBreaks = match.Groups["originBreaks"].Value.Trim();

                    SdkBreakingChange breakingChange = new SdkBreakingChange
                    {
                        BreakingChange = breaking,
                        Category = category,
                        Resolution = resolution,
                        OriginBreaks = !string.IsNullOrEmpty(originBreaks) ? originBreaks.Split('\n').Select(s => s.Trim()).ToList() : new List<string>()
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
        [JsonPropertyName("resolution")]
        public string? Resolution { get; set; }
        [JsonPropertyName("originBreaks")]
        public List<string>? OriginBreaks { get; set; }
    }
}
