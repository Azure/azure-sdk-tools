using System.CommandLine;
using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to generate SDK code locally.")]
    public class SdkGenerationTool(
        IGitHelper gitHelper,
        ILogger<SdkGenerationTool> logger,
        INpxHelper npxHelper
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        // Command names
        private const string GenerateSdkCommandName = "generate";
        private const int CommandTimeoutInMinutes = 30;

        // Generate command options
        private readonly Option<string> localSdkRepoPathOpt = new("--local-sdk-repo-path", "-r")
        {
            Description = "Absolute path to the local azure-sdk-for-{language} repository",
            Required = false,
        };

        private readonly Option<string> tspConfigPathOpt = new("--tsp-config-path", "-t")
        {
            Description = "Path to the 'tspconfig.yaml' configuration file, it can be a local path or remote HTTPS URL",
            Required = false,
        };

        private readonly Option<string> tspLocationPathOpt = new("--tsp-location-path", "-l")
        {
            Description = "Absolute path to the 'tsp-location.yaml' configuration file",
            Required = false,
        };

        private readonly Option<string> emitterOpt = new("--emitter-options", "-o")
        {
            Description = "Emitter options in key-value format. Example: 'package-version=1.0.0-beta.1'",
            Required = false,
        };

        protected override Command GetCommand() =>
            new(GenerateSdkCommandName, "Generates SDK code for a specified language based on the provided 'tspconfig.yaml' or 'tsp-location.yaml'")
            {
                localSdkRepoPathOpt, tspConfigPathOpt, tspLocationPathOpt, emitterOpt,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var localSdkRepoPath = parseResult.GetValue(localSdkRepoPathOpt);
            var tspConfigPath = parseResult.GetValue(tspConfigPathOpt);
            var tspLocationPath = parseResult.GetValue(tspLocationPathOpt);
            var emitterOptions = parseResult.GetValue(emitterOpt);
            return await GenerateSdkAsync(localSdkRepoPath, tspConfigPath, tspLocationPath, emitterOptions, ct);
        }

        [McpServerTool(Name = "azsdk_package_generate_code"), Description("Generates SDK code for a specified language using either 'tspconfig.yaml' or 'tsp-location.yaml'. Runs locally.")]
        public async Task<PackageOperationResponse> GenerateSdkAsync(
            [Description("Absolute path to the local Azure SDK repository. REQUIRED. Example: 'path/to/azure-sdk-for-net'. If not provided, the tool attempts to discover the repo from the current working directory.")]
            string localSdkRepoPath,
            [Description("Path to the 'tspconfig.yaml' file. Can be a local file path or a remote HTTPS URL. Optional if running inside a local cloned azure-sdk-for-{language} repository, for example, inside 'azure-sdk-for-net' repository.")]
            string? tspConfigPath,
            [Description("Path to 'tsp-location.yaml'. Optional. May be left empty if running inside a local cloned 'azure-rest-api-specs' repository.")]
            string? tspLocationPath,
            [Description("Emitter options in key-value format. Optional. Leave empty for defaults. Example: 'package-version=1.0.0-beta.1'.")]
            string? emitterOptions,
            CancellationToken ct = default)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(tspConfigPath) && string.IsNullOrEmpty(tspLocationPath))
                {
                    return CreateFailureResponse("Both 'tspconfig.yaml' and 'tsp-location.yaml' paths aren't provided. At least one of them is required.", null);
                }

                // Handle tsp-location.yaml case
                if (!string.IsNullOrEmpty(tspLocationPath))
                {
                    if (!tspLocationPath.EndsWith("tsp-location.yaml", StringComparison.OrdinalIgnoreCase))
                    {
                        return CreateFailureResponse($"The specified 'tsp-location.yaml' path is invalid: {tspLocationPath}. It must be an absolute path to local 'tsp-location.yaml' file.", null);
                    }
                    return await RunTspUpdate(tspLocationPath, ct);
                }

                // Handle tspconfig.yaml case
                return await GenerateSdkFromTspConfigAsync(localSdkRepoPath, tspConfigPath, emitterOptions, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while generating SDK");
                return CreateFailureResponse($"An error occurred: {ex.Message}", null);
            }
        }

        // Run language-specific script to generate the SDK code from 'tspconfig.yaml'
        private async Task<PackageOperationResponse> GenerateSdkFromTspConfigAsync(string localSdkRepoPath, string tspConfigPath, string emitterOptions, CancellationToken ct)
        {
            string specRepoFullName = string.Empty;

            // white spaces will be added by agent when it's a URL
            tspConfigPath = tspConfigPath.Trim();

            // Validate inputs
            logger.LogInformation("Generating SDK at repo: {LocalSdkRepoPath}", localSdkRepoPath);
            if (string.IsNullOrEmpty(localSdkRepoPath) || !Directory.Exists(localSdkRepoPath))
            {
                return CreateFailureResponse($"The directory for the local sdk repo does not provide or exist at the specified path: {localSdkRepoPath}. Prompt user to clone the matched SDK repository users want to generate SDK against.", null);
            }

            // Get the generate script path
            string sdkRepoRoot = gitHelper.DiscoverRepoRoot(localSdkRepoPath);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return CreateFailureResponse($"Failed to discover local sdk repo with path: {localSdkRepoPath}.", null);
            }

            string sdkRepoName = gitHelper.GetRepoName(sdkRepoRoot);
            if (!tspConfigPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Validate arguments for local tspconfig.yaml case
                if (!File.Exists(tspConfigPath))
                {
                    return CreateFailureResponse($"The 'tspconfig.yaml' file does not exist at the specified path: {tspConfigPath}. Prompt user to clone the azure-rest-api-specs repository locally if it does not have a local copy.", sdkRepoName);
                }
                specRepoFullName = await gitHelper.GetRepoFullNameAsync(tspConfigPath, findUpstreamParent: false);
            }
            else
            {
                // specRepoFullName doesn't need to be set in this case
                logger.LogInformation("Remote 'tspconfig.yaml' URL detected: {TspConfigPath}.", tspConfigPath);
                if (!IsValidRemoteGitHubUrlWithCommit(tspConfigPath))
                {
                    return CreateFailureResponse($"Invalid remote GitHub URL with commit: {tspConfigPath}. The URL must include a valid commit SHA. Example: https://github.com/Azure/azure-rest-api-specs/blob/dee71463cbde1d416c47cf544e34f7966a94ddcb/specification/contosowidgetmanager/Contoso.Management/tspconfig.yaml", sdkRepoName);
                }
            }

            return await RunTspInit(localSdkRepoPath, tspConfigPath, specRepoFullName, emitterOptions, sdkRepoName, ct);
        }

        // Run tsp-client update command to re-generate the SDK code
        private async Task<PackageOperationResponse> RunTspUpdate(string tspLocationPath, CancellationToken ct)
        {
            if (!File.Exists(tspLocationPath))
            {
                return CreateFailureResponse($"The 'tsp-location.yaml' file does not exist at the specified path: {tspLocationPath}", null);
            }

            var tspLocationDirectory = Path.GetDirectoryName(tspLocationPath);
            logger.LogInformation("Running tsp-client update command in directory: {TspLocationDirectory}", tspLocationDirectory);
            string sdkRepoName = gitHelper.GetRepoName(tspLocationPath);
            logger.LogInformation("SDK Repository Name: {SdkRepoName}", sdkRepoName);
            var npxOptions = new NpxOptions(
                "@azure-tools/typespec-client-generator-cli",
                ["tsp-client", "update"],
                logOutputStream: true,
                workingDirectory: tspLocationDirectory,
                timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
            );

            var tspClientResult = await npxHelper.Run(npxOptions, ct);
            if (tspClientResult.ExitCode != 0)
            {
                return CreateFailureResponse($"tsp-client update failed with exit code {tspClientResult.ExitCode}. Output:\n{tspClientResult.Output}", sdkRepoName);
            }

            logger.LogInformation("tsp-client update completed successfully");
            return CreateSuccessResponse($"SDK re-generation completed successfully using tsp-location.yaml. Output:\n{tspClientResult.Output}", sdkRepoName);
        }

        // Run tsp-client init command to re-generate the SDK code
        private async Task<PackageOperationResponse> RunTspInit(string localSdkRepoPath, string tspConfigPath, string specRepoFullName, string emitterOptions, string sdkRepoName, CancellationToken ct)
        {
            logger.LogInformation("Running tsp-client init command.");

            // Build arguments list dynamically
            var arguments = new List<string> { "tsp-client", "init", "--update-if-exists", "--tsp-config", tspConfigPath };

            if (!string.IsNullOrEmpty(specRepoFullName))
            {
                arguments.Add("--repo");
                arguments.Add(specRepoFullName);
            }

            if (!string.IsNullOrEmpty(emitterOptions))
            {
                arguments.Add("--emitter-options");
                arguments.Add(emitterOptions);
            }

            var npxOptions = new NpxOptions(
                "@azure-tools/typespec-client-generator-cli",
                arguments.ToArray(),
                logOutputStream: true,
                workingDirectory: localSdkRepoPath,
                timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
            );

            var tspClientResult = await npxHelper.Run(npxOptions, ct);
            if (tspClientResult.ExitCode != 0)
            {
                return CreateFailureResponse($"tsp-client init failed with exit code {tspClientResult.ExitCode}. Output:\n{tspClientResult.Output}", sdkRepoName);
            }

            logger.LogInformation("tsp-client init completed successfully");
            return CreateSuccessResponse($"SDK generation completed successfully using tspconfig.yaml. Output:\n{tspClientResult.Output}", sdkRepoName);
        }



        // Validate remote GitHub URL with commit SHA
        private bool IsValidRemoteGitHubUrlWithCommit(string tspConfigPath)
        {
            // Extract the part after /blob/ and before the next /
            var blobIndex = tspConfigPath.IndexOf("/blob/", StringComparison.OrdinalIgnoreCase);
            if (blobIndex == -1)
            {
                return false;
            }

            var afterBlob = tspConfigPath.Substring(blobIndex + 6);
            var nextSlashIndex = afterBlob.IndexOf('/');
            if (nextSlashIndex == -1)
            {
                return false;
            }

            var commitOrBranch = afterBlob.Substring(0, nextSlashIndex);

            // Validate that it's a 40-character commit SHA, not a branch name
            return IsValidSha(commitOrBranch);
        }

        // Validate commitSha: must be a 40-character hex string
        private bool IsValidSha(string sha)
        {
            if (!string.IsNullOrEmpty(sha))
            {
                var match = Regex.Match(sha, @"^[a-f0-9]{40}$", RegexOptions.IgnoreCase);
                return match.Success;
            }

            return false;
        }

        // Helper method to create failure responses along with setting the failure state
        private PackageOperationResponse CreateFailureResponse(string message, string sdkRepoName)
        {
            return new PackageOperationResponse
            {
                ResponseErrors = [message],
                SdkRepoName = sdkRepoName
            };
        }

        // Helper method to create success responses (no SetFailure needed)
        private PackageOperationResponse CreateSuccessResponse(string message, string sdkRepoName)
        {
            return new PackageOperationResponse
            {
                Result = "succeeded",
                Message = message,
                SdkRepoName = sdkRepoName
            };
        }
    }
}
