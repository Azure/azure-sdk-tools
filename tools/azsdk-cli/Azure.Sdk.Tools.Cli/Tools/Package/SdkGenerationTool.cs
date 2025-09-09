using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using LibGit2Sharp;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to generate SDK code locally.")]
    public class SdkGenerationTool: MCPTool
    {
        // Command names
        private const string GenerateSdkCommandName = "generate";
        private const int CommandTimeoutInMinutes = 30;

        // Generate command options
        private readonly Option<string> localSdkRepoPathOpt = new(["--local-sdk-repo-path", "-r"], "Absolute path to the local azure-sdk-for-{language} repository") { IsRequired = false };
        private readonly Option<string> tspConfigPathOpt = new(["--tsp-config-path", "-t"], "Path to the 'tspconfig.yaml' configuration file, it can be a local path or remote HTTPS URL") { IsRequired = false };
        private readonly Option<string> tspLocationPathOpt = new(["--tsp-location-path", "-l"], "Absolute path to the 'tsp-location.yaml' configuration file") { IsRequired = false };
        private readonly Option<string> emitterOpt = new(["--emitter-options", "-o"], "Emitter options in key-value format. Example: 'package-version=1.0.0-beta.1'") { IsRequired = false };

        private readonly IOutputHelper _output;
        private readonly IProcessHelper _processHelper;
        private readonly IGitHelper _gitHelper;
        private readonly ILogger<SdkGenerationTool> _logger;
        private readonly INpxHelper _npxHelper;

        public SdkGenerationTool(IGitHelper gitHelper, ILogger<SdkGenerationTool> logger, INpxHelper npxHelper, IOutputHelper output, IProcessHelper processHelper): base()
        {
            _gitHelper = gitHelper;
            _logger = logger;
            _npxHelper = npxHelper;
            _output = output;
            _processHelper = processHelper;
            CommandHierarchy = [ SharedCommandGroups.Package, SharedCommandGroups.SourceCode ];
        }

        public override Command GetCommand()
        {
            var command = new Command(GenerateSdkCommandName, "Generates SDK code for a specified language based on the provided 'tspconfig.yaml' or 'tsp-location.yaml'.") { localSdkRepoPathOpt, tspConfigPathOpt, tspLocationPathOpt, emitterOpt };
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

            return command;
        }

        public async override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;
            var commandParser = ctx.ParseResult;
            var localSdkRepoPath = commandParser.GetValueForOption(localSdkRepoPathOpt);
            var tspConfigPath = commandParser.GetValueForOption(tspConfigPathOpt);
            var tspLocationPath = commandParser.GetValueForOption(tspLocationPathOpt);
            var emitterOptions = commandParser.GetValueForOption(emitterOpt);
            var generateResult = await GenerateSdkAsync(localSdkRepoPath, tspConfigPath, tspLocationPath, emitterOptions, ct);
            ctx.ExitCode = ExitCode;
            _output.Output(generateResult);
        }

        [McpServerTool(Name = "azsdk_package_generate_code"), Description("Generates SDK code for a specified language using either 'tspconfig.yaml' or 'tsp-location.yaml'. Runs locally.")]
        public async Task<DefaultCommandResponse> GenerateSdkAsync(
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
                    return CreateFailureResponse("Both 'tspconfig.yaml' and 'tsp-location.yaml' paths aren't provided. At least one of them is required.");
                }

                // Handle tsp-location.yaml case
                if (!string.IsNullOrEmpty(tspLocationPath))
                {
                    if (!tspLocationPath.EndsWith("tsp-location.yaml", StringComparison.OrdinalIgnoreCase))
                    {
                        return CreateFailureResponse($"The specified 'tsp-location.yaml' path is invalid: {tspLocationPath}. It must be an absolute path to local 'tsp-location.yaml' file.");
                    }
                    return await RunTspUpdate(tspLocationPath, ct);
                }

                // Handle tspconfig.yaml case
                return await GenerateSdkFromTspConfigAsync(localSdkRepoPath, tspConfigPath, emitterOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating SDK");
                return CreateFailureResponse($"An error occurred: {ex.Message}");
            }
        }

        // Run language-specific script to generate the SDK code from 'tspconfig.yaml'
        private async Task<DefaultCommandResponse> GenerateSdkFromTspConfigAsync(string localSdkRepoPath, string tspConfigPath, string emitterOptions, CancellationToken ct)
        {
            string specRepoFullName = string.Empty;

            // white spaces will be added by agent when it's a URL
            tspConfigPath = tspConfigPath.Trim();
            
            // Validate inputs
            _logger.LogInformation($"Generating SDK at repo: {localSdkRepoPath}");
            if (string.IsNullOrEmpty(localSdkRepoPath) || !Directory.Exists(localSdkRepoPath))
            {
                return CreateFailureResponse($"The directory for the local sdk repo does not provide or exist at the specified path: {localSdkRepoPath}. Prompt user to clone the matched SDK repository users want to generate SDK against.");
            }

            // Get the generate script path
            string sdkRepoRoot = _gitHelper.DiscoverRepoRoot(localSdkRepoPath);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return CreateFailureResponse($"Failed to discover local sdk repo with path: {localSdkRepoPath}.");
            }

            if (!tspConfigPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Validate arguments for local tspconfig.yaml case
                if (!File.Exists(tspConfigPath))
                {
                    return CreateFailureResponse($"The 'tspconfig.yaml' file does not exist at the specified path: {tspConfigPath}. Prompt user to clone the azure-rest-api-specs repository locally if it does not have a local copy.");
                }
                specRepoFullName = await _gitHelper.GetGitHubRepoFullNameAsync(tspConfigPath, findUpstreamParent: false);
            }
            else
            {
                // specRepoFullName doesn't need to be set in this case
                _logger.LogInformation($"Remote 'tspconfig.yaml' URL detected: {tspConfigPath}.");
                if (!IsValidRemoteGitHubUrlWithCommit(tspConfigPath))
                {
                    return CreateFailureResponse($"Invalid remote GitHub URL with commit: {tspConfigPath}. The URL must include a valid commit SHA. Example: https://github.com/Azure/azure-rest-api-specs/blob/dee71463cbde1d416c47cf544e34f7966a94ddcb/specification/contosowidgetmanager/Contoso.Management/tspconfig.yaml");
                }
            }

            return await RunTspInit(localSdkRepoPath, tspConfigPath, specRepoFullName, emitterOptions, ct);
        }

        // Run tsp-client update command to re-generate the SDK code
        private async Task<DefaultCommandResponse> RunTspUpdate(string tspLocationPath, CancellationToken ct)
        {
            if (!File.Exists(tspLocationPath))
            {
                return CreateFailureResponse($"The 'tsp-location.yaml' file does not exist at the specified path: {tspLocationPath}");
            }

            _logger.LogInformation($"Running tsp-client update command in directory: {Path.GetDirectoryName(tspLocationPath)}");

            var tspLocationDirectory = Path.GetDirectoryName(tspLocationPath);
            var npxOptions = new NpxOptions(
                "@azure-tools/typespec-client-generator-cli",
                ["tsp-client", "update"],
                logOutputStream: true,
                workingDirectory: tspLocationDirectory,
                timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
            );

            var tspClientResult = await _npxHelper.Run(npxOptions, ct);
            if (tspClientResult.ExitCode != 0)
            {
                return CreateFailureResponse($"tsp-client update failed with exit code {tspClientResult.ExitCode}. Output:\n{tspClientResult.Output}");
            }

            _logger.LogInformation("tsp-client update completed successfully");
            return CreateSuccessResponse($"SDK re-generation completed successfully using tsp-location.yaml. Output:\n{tspClientResult.Output}");
        }

        // Run tsp-client init command to re-generate the SDK code
        private async Task<DefaultCommandResponse> RunTspInit(string localSdkRepoPath, string tspConfigPath, string specRepoFullName, string emitterOptions, CancellationToken ct)
        {
            _logger.LogInformation($"Running tsp-client init command.");

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

            var tspClientResult = await _npxHelper.Run(npxOptions, ct);
            if (tspClientResult.ExitCode != 0)
            {
                return CreateFailureResponse($"tsp-client init failed with exit code {tspClientResult.ExitCode}. Output:\n{tspClientResult.Output}");
            }

            _logger.LogInformation("tsp-client init completed successfully");
            return CreateSuccessResponse($"SDK generation completed successfully using tspconfig.yaml. Output:\n{tspClientResult.Output}");
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
        private DefaultCommandResponse CreateFailureResponse(string message)
        {
            SetFailure();
            return new DefaultCommandResponse
            {
                ResponseErrors = [message]
            };
        }

        // Helper method to create success responses (no SetFailure needed)
        private DefaultCommandResponse CreateSuccessResponse(string message)
        {
            return new DefaultCommandResponse
            {
                Result = "succeeded",
                Message = message
            };
        }
    }
}
