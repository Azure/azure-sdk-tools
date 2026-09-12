// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.Text.Json;
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
            Description = "Path to the SDK change json file. It is a local file path. It will be ignored if changes-only is set. Optional.",
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
            SdkChange? sdkChange = null;
            PackageInfo? packageInfo = null;
            try
            {
                ct.ThrowIfCancellationRequested();
                logger.LogInformation("Parameters: packagePath={PackagePath}, tspConfigPath={TspConfigPath}, changesOnly={ChangesOnly}, localSdkChangeJsonFilePath={LocalSdkChangeJsonFilePath}",
                    packagePath, tspConfigPath ?? "null", changesOnly, localSdkChangeJsonFilePath ?? "null");

                if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
                {
                    return CreateFailure($"The directory for the local sdk does not provide or exist at the specified path: {packagePath}. Prompt user to clone the matched SDK repository users want to generate SDK against.");
                }

                LanguageService languageService = await GetLanguageServiceAsync(packagePath, ct);

                if (languageService == null)
                {
                    return CreateFailure("Tooling error: unable to determine language service for the specified package path.");
                }
                // Discover the repository root
                var sdkRepoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
                if (sdkRepoRoot == null)
                {
                    return CreateFailure($"Unable to find git repository root from the provided package path: {packagePath}. Please ensure the package path is within a valid git repository.");
                }
                packageInfo = await languageService.GetPackageInfo(packagePath, ct);
                if (!string.IsNullOrEmpty(localSdkChangeJsonFilePath) && !changesOnly)
                {
                    logger.LogInformation("Using local SDK change JSON file at: {FilePath}", localSdkChangeJsonFilePath);

                    if (!File.Exists(localSdkChangeJsonFilePath))
                    {
                        logger.LogInformation("Local SDK change JSON file not found at: {FilePath}. Proceeding to retrieve SDK changes using the configured script.", localSdkChangeJsonFilePath);
                    }
                    else
                    {
                        // Read and deserialize the local SDK change JSON file
                        sdkChange = await ReadSdkChangeAsync(localSdkChangeJsonFilePath, ct);
                    }
                }
                // If sdkChange is still null, attempt to retrieve it using the configured script
                if (sdkChange == null)
                {
                    var retrieval = await RetrieveSdkChangeFromScriptAsync(sdkRepoRoot, packagePath, packageInfo, languageService, ct);
                    if (retrieval.failure != null)
                    {
                        return retrieval.failure;
                    }
                    sdkChange = retrieval.sdkChange;
                }

                if (sdkChange != null)
                {
                    var dotnetDetails = languageService.Language == SdkLanguage.DotNet && sdkChange.Details != null
                        ? JsonSerializer.SerializeToElement(sdkChange.Details).Deserialize<DotnetSdkChangeDetails>()
                        : null;
                    if (languageService.Language == SdkLanguage.DotNet &&
                        string.IsNullOrWhiteSpace(dotnetDetails?.BaselineVersion))
                    {
                        return new PackageOperationResponse
                        {
                            Result = CreateUnclassifiedResult(sdkChange),
                            BreakingChangeStatus = SdkBreakingChangeStatus.Inconclusive,
                            Message = "No GA baseline provenance is available; compatibility was not evaluated. Supply a detector report with details.baselineVersion.",
                            Language = languageService.Language,
                            PackageName = packageInfo?.PackageName,
                        };
                    }
                    if (sdkChange.HasBreakingChange && !changesOnly)
                    {
                        // analyze and classify the breaking changes
                        return await ClassifySdkBreakingChangesAsync(sdkChange, sdkRepoRoot, languageService, packageInfo, tspConfigPath, ct);
                    }
                    else
                    {
                        return new PackageOperationResponse()
                        {
                            Result = CreateUnclassifiedResult(sdkChange),
                            BreakingChangeStatus = sdkChange.HasBreakingChange ? SdkBreakingChangeStatus.Detected : SdkBreakingChangeStatus.Clean,
                            Message = sdkChange.HasBreakingChange
                                ? "SDK changes detected. Breaking change classification skipped as per the 'changes-only' option."
                                : "No SDK breaking changes detected.",
                            Language = languageService.Language,
                            PackageName = packageInfo?.PackageName,
                        };
                    }
                }

                // Run default logic to detect SDK breaking changes
                logger.LogInformation("Running default logic to detect SDK breaking changes for the package...");
                var fallbackResponse = await languageService.DetectSdkBreakingChangeAsync(packagePath, ct);
                ct.ThrowIfCancellationRequested();
                fallbackResponse.BreakingChangeStatus ??= fallbackResponse.OperationStatus == Status.Succeeded && fallbackResponse.ExitCode == 0
                    ? SdkBreakingChangeStatus.Clean : SdkBreakingChangeStatus.Failed;
                return fallbackResponse;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while detecting SDK breaking changes.");
                var response = CreateFailure($"An error occurred while detecting SDK breaking changes: {ex.Message}", packageInfo);
                if (sdkChange != null)
                {
                    response.Result = CreateUnclassifiedResult(sdkChange);
                }
                return response;
            }
        }

        private static PackageOperationResponse CreateFailure(string message, PackageInfo? packageInfo = null)
        {
            var response = PackageOperationResponse.CreateFailure(message, packageInfo);
            response.BreakingChangeStatus = SdkBreakingChangeStatus.Failed;
            return response;
        }

        private static async Task<SdkChange> ReadSdkChangeAsync(string path, CancellationToken ct)
        {
            await using var stream = File.OpenRead(path);
            var change = await JsonSerializer.DeserializeAsync<SdkChange>(stream, cancellationToken: ct);
            if (change == null || string.IsNullOrWhiteSpace(change.SdkChangeMD))
            {
                throw new JsonException($"SDK change file '{path}' must contain nonempty changes (Markdown) and a Boolean hasBreakingChange.");
            }
            return change;
        }

        private static SdkBreakingChangeDetectionResult CreateUnclassifiedResult(SdkChange sdkChange) => new()
        {
            HasBreakingChange = sdkChange.HasBreakingChange,
            SdkChangeMD = sdkChange.SdkChangeMD,
            Details = sdkChange.Details,
        };

        private async Task<(SdkChange? sdkChange, PackageOperationResponse? failure)> RetrieveSdkChangeFromScriptAsync(string sdkRepoRoot, string packagePath, PackageInfo packageInfo, LanguageService languageService, CancellationToken ct)
        {
            logger.LogInformation("Retrieve SDK changes using the configured script.");
            SdkChange? sdkChange = null;
            // execute configured sdk change retrieve script
            var (configContentType, configValue) = await _specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.GetSdkChanges, ct);
            ct.ThrowIfCancellationRequested();
            if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrWhiteSpace(configValue))
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
                    try
                    {
                        var sdkChangeResponse = await _specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "SDK changes are retrieved.");
                        ct.ThrowIfCancellationRequested();
                        if (sdkChangeResponse == null || sdkChangeResponse.OperationStatus == Status.Failed || sdkChangeResponse.ExitCode != 0)
                        {
                            return (null, CreateFailure($"Failed to retrieve SDK changes using the configured script: {sdkChangeResponse?.ResponseError}{Environment.NewLine}{string.Join(Environment.NewLine, sdkChangeResponse?.ResponseErrors ?? [])}", packageInfo));
                        }
                        if (!File.Exists(sdkChangeFilePath))
                        {
                            return (null, CreateFailure("The SDK change script did not produce its output JSON file.", packageInfo));
                        }
                        sdkChange = await ReadSdkChangeAsync(sdkChangeFilePath, ct);
                    }
                    finally
                    {
                        try
                        {
                            File.Delete(sdkChangeFilePath);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            logger.LogWarning(ex, "Could not delete SDK change report at {Path}.", sdkChangeFilePath);
                        }
                    }
                }
                else
                {
                    logger.LogError("Failed to create process options for the configured script.");
                    return (null, CreateFailure("Failed to create process options for the configured script.", packageInfo));
                }
            }
            else
            {
                logger.LogInformation("No valid SDK change retrieval configuration was found. It is not implemented.");
            }

            return (sdkChange, null);
        }

        /// <summary>
        /// Analyzes and classifies the SDK breaking changes from SDK change data.
        /// </summary>
        private async Task<PackageOperationResponse> ClassifySdkBreakingChangesAsync(SdkChange sdkChange, string sdkRepoRoot, LanguageService languageService, PackageInfo? packageInfo, string? tspConfigPath, CancellationToken ct)
        {
            // analyze and classify the breaking changes
            var tspProjectPath = tspConfigPath != null ? Path.GetDirectoryName(tspConfigPath) : null;
            var sdkBreakingPattern = await languageService.GetSdkBreakingPattern(sdkRepoRoot, ct);
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sdkBreakingPattern))
            {
                var failure = CreateFailure("SDK breaking-change classification requires a configured packageOptions.sdkBreakingChangePatternFile catalog.", packageInfo);
                failure.Result = CreateUnclassifiedResult(sdkChange);
                return failure;
            }
            var sdkBreakingChangeResult = await _classifyService.ClassifySdkBreakingChangesAsync(sdkChange.SdkChangeMD, sdkBreakingPattern, languageService.Language.ToString(), tspProjectPath, ct);
            ct.ThrowIfCancellationRequested();
            if (sdkBreakingChangeResult == null)
            {
                logger.LogError("Failed to classify SDK breaking changes.");
                return new PackageOperationResponse
                {
                    ResponseError = "Failed to classify SDK breaking changes.",
                    BreakingChangeStatus = SdkBreakingChangeStatus.Failed,
                    Result = CreateUnclassifiedResult(sdkChange),
                    Language = languageService.Language,
                    PackageName = packageInfo?.PackageName,
                };
            }
            var validationError = languageService.ValidateBreakingChangeClassification(sdkBreakingChangeResult);
            if (validationError != null)
            {
                return new PackageOperationResponse
                {
                    ResponseError = validationError,
                    BreakingChangeStatus = SdkBreakingChangeStatus.Failed,
                    Result = CreateUnclassifiedResult(sdkChange),
                    Language = languageService.Language,
                    PackageName = packageInfo?.PackageName,
                };
            }
            sdkBreakingChangeResult.SdkChangeMD = sdkChange.SdkChangeMD;
            sdkBreakingChangeResult.Details = sdkChange.Details;
            return new PackageOperationResponse()
            {
                Result = sdkBreakingChangeResult,
                BreakingChangeStatus = SdkBreakingChangeStatus.Classified,
                Message = "SDK breaking changes detected and classified.",
                Language = languageService.Language,
                PackageName = packageInfo?.PackageName,
            };
        }
    }
}
