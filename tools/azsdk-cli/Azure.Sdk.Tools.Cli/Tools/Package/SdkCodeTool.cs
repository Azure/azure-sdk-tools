using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using LibGit2Sharp;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to generate SDK code and build SDK code locally.")]
    public class SdkCodeTool(IGitHelper gitHelper, ILogger<SdkCodeTool> logger, INpxHelper npxHelper, IOutputHelper output, IProcessHelper processHelper) : MCPTool
    {
        // Command names
        private const string generateSdkCommandName = "generate";
        private const string buildSdkCommandName = "build";
        private const int commandTimeout = 30 * 60 * 1000; // 30 mins

        // Build command options
        private readonly Option<string> projectPathOpt = new(["--project-path", "-p"], "Absolute path to the SDK project") { IsRequired = true };

        // Generate command options
        private readonly Option<string> localSdkRepoPathOpt = new(["--local-sdk-repo-path", "-r"], "Absolute path to the local azure-sdk-for-{language} repository") { IsRequired = false };
        private readonly Option<string> tspConfigPathOpt = new(["--tsp-config-path", "-t"], "Path to the 'tspconfig.yaml' configuration file, it can be a local path or remote HTTPS URL") { IsRequired = false };
        private readonly Option<string> specCommitShaOpt = new(["--spec-commit-sha", "-c"], "Commit SHA of the 'azure-rest-api-specs' repository to use. Must be a valid 40-character Git SHA. Required when tspConfigPath is a local file path") { IsRequired = false };
        private readonly Option<string> specRepoFullNameOpt = new(["--spec-repo-full-name", "-s"], "Full name of the repository in 'owner/repo' format. Example: 'Azure/azure-rest-api-specs'. Required when tspConfigPath is a local file path") { IsRequired = false };
        private readonly Option<string> tspLocationPathOpt = new(["--tsp-location-path", "-l"], "Absolute path to the 'tsp-location.yaml' configuration file") { IsRequired = false };
        private readonly Option<string> emitterOpt = new(["--emitter-options", "-o"], "Emitter optionsn in key-value format. Example: 'package-version=1.0.0-beta.1'") { IsRequired = false };

        public override Command GetCommand()
        {
            var command = new Command("code", "Azure SDK code tools");
            var subCommands = new[]
            {
                new Command(generateSdkCommandName, "Generates SDK code for a specified language based on the provided 'tspconfig.yaml' or 'tsp-location.yaml'.") { localSdkRepoPathOpt, tspConfigPathOpt, specCommitShaOpt, specRepoFullNameOpt, tspLocationPathOpt, emitterOpt },
                new Command(buildSdkCommandName, "Builds SDK code for a specified language and project.") { projectPathOpt },
            };

            foreach (var subCommand in subCommands)
            {
                subCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
                command.AddCommand(subCommand);
            }
            return command;
        }

        public async override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;
            var commandParser = ctx.ParseResult;

            switch (command)
            {
                case generateSdkCommandName:
                    var localSdkRepoPath = commandParser.GetValueForOption(localSdkRepoPathOpt);
                    var tspConfigPath = commandParser.GetValueForOption(tspConfigPathOpt);
                    var specCommitSha = commandParser.GetValueForOption(specCommitShaOpt);
                    var specRepoFullName = commandParser.GetValueForOption(specRepoFullNameOpt);
                    var tspLocationPath = commandParser.GetValueForOption(tspLocationPathOpt);
                    var emitterOptions = commandParser.GetValueForOption(emitterOpt);
                    var generateResult = await GenerateSdk(localSdkRepoPath, tspConfigPath, specCommitSha, specRepoFullName, tspLocationPath, emitterOptions, ct);
                    ctx.ExitCode = ExitCode;
                    output.Output(generateResult);
                    break;
                case buildSdkCommandName:
                    var projectPath = commandParser.GetValueForOption(projectPathOpt);
                    var buildResult = await BuildSdkAsync(projectPath, ct);
                    ctx.ExitCode = ExitCode;
                    output.Output(buildResult);
                    break;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    break;
            }

            return;
        }

        [McpServerTool(Name = "azsdk_package_generate_code"), Description("Generates SDK code for a specified language using either 'tspconfig.yaml' or 'tsp-location.yaml'. Runs locally.")]
        public async Task<DefaultCommandResponse> GenerateSdk(
            [Description("Absolute path to the local Azure SDK repository. Optional. Example: 'azure-sdk-for-net'. If not provided, the tool attempts to discover the repo from the current working directory.")]
            string localSdkRepoPath,
            [Description("Path to the 'tspconfig.yaml' file. Can be a local file path (requires specCommitSha and specRepoFullName) or a remote HTTPS URL. Optional if running inside a local azure-sdk-{language} repository.")]
            string? tspConfigPath,
            [Description("Commit SHA of the 'azure-rest-api-specs' repository to use. Must be a valid 40-character Git SHA. Required when tspConfigPath is a local file path.")]
            string? specCommitSha,
            [Description("Full name of the repository in 'owner/repo' format. Example: 'Azure/azure-rest-api-specs'. Required when tspConfigPath is a local file path.")]
            string? specRepoFullName,
            [Description("Path to 'tsp-location.yaml'. Optional. May be left empty if running inside a local azure-rest-api-specs repository.")]
            string? tspLocationPath,
            [Description("Emitter options in key-value format. Optional. Example: 'package-version=1.0.0-beta.1'.")]
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
                    return await RunTspUpdate(tspLocationPath, ct);
                }

                // Handle tspconfig.yaml case
                return await GenerateSdkFromTspConfigAsync(localSdkRepoPath, tspConfigPath, specCommitSha, specRepoFullName, emitterOptions, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while generating SDK");
                return CreateFailureResponse($"An error occurred: {ex.Message}");
            }
        }

        [McpServerTool(Name = "azsdk_package_build_code"), Description("Build SDK code for a specified project locally.")]
        public async Task<DefaultCommandResponse> BuildSdkAsync(
            [Description("Absolute path to the SDK project.")]
            string projectPath,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation($"Building SDK for project path: {projectPath}");

                // Validate inputs
                if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                {
                    return CreateFailureResponse("Invalid project path. Please provide a valid project path.");
                }

                // Return if the project is python project
                if (projectPath.Contains("azure-sdk-for-python", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                    return CreateSuccessResponse("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                }

                // Get repository root path from project path
                string sdkRepoRoot = gitHelper.DiscoverRepoRoot(projectPath);
                if (string.IsNullOrEmpty(sdkRepoRoot))
                {
                    return CreateFailureResponse($"Failed to discover local sdk repo with project-path: {projectPath}.");
                }

                logger.LogInformation($"Repository root path: {sdkRepoRoot}");

                // Get the build script path and resolve full path
                string fullBuildScriptPath;
                try
                {
                    var buildScriptPath = await GetScriptPathFromConfigAsync(sdkRepoRoot, "packageOptions/buildScript/path");
                    logger.LogInformation($"Build script path: {buildScriptPath}");

                    // Resolve the full path of the build script
                    fullBuildScriptPath = Path.IsPathRooted(buildScriptPath) 
                        ? buildScriptPath 
                        : Path.Combine(sdkRepoRoot, buildScriptPath);

                    if (!File.Exists(fullBuildScriptPath))
                    {
                        return CreateFailureResponse($"Build script not found at: {fullBuildScriptPath}");
                    }
                }
                catch (Exception ex)
                {
                    return CreateFailureResponse($"Failed to get build script path: {ex.Message}");
                }

                // Run the build script
                logger.LogInformation($"Executing build script: {fullBuildScriptPath}");

                // TODO: change --module-dir to --project-path
                var options = new ProcessOptions(
                    fullBuildScriptPath,
                    new[] { "--module-dir", projectPath },
                    logOutputStream: true,
                    workingDirectory: sdkRepoRoot,
                    timeout: TimeSpan.FromMilliseconds(commandTimeout)
                );
                var buildResult = await processHelper.Run(options, ct);
                var trimmedBuildResult = (buildResult.Output ?? string.Empty).Trim();
                if (buildResult.ExitCode != 0)
                {
                    return CreateFailureResponse($"Build script failed with exit code {buildResult.ExitCode}. Output:\n{trimmedBuildResult}");
                }

                logger.LogInformation("Build script execution completed");
                return CreateSuccessResponse($"Build completed successfully. Output:\n{trimmedBuildResult}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while building SDK");
                return CreateFailureResponse($"An error occurred: {ex.Message}");
            }
        }

        // Run language-specific script to generate the SDK code from 'tspconfig.yaml'
        private async Task<DefaultCommandResponse> GenerateSdkFromTspConfigAsync(string localSdkRepoPath, string tspConfigPath, string specCommitSha, string specRepoFullName, string emitterOptions, CancellationToken ct)
        {
            // white spaces will be added by agent when it's a URL
            tspConfigPath = tspConfigPath.Trim();
            
            // Validate inputs
            logger.LogInformation($"Generating SDK at repo: {localSdkRepoPath}");
            if (string.IsNullOrEmpty(localSdkRepoPath) || !Directory.Exists(localSdkRepoPath))
            {
                return CreateFailureResponse($"The directory for the local sdk repo does not provide or exist at the specified path: {localSdkRepoPath}. Prompt user to clone the matched SDK repository users want to generate SDK against.");
            }

            // Get the generate script path
            string sdkRepoRoot = gitHelper.DiscoverRepoRoot(localSdkRepoPath);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return CreateFailureResponse($"Failed to discover local sdk repo with path: {localSdkRepoPath}.");
            }

            // Validate arguments for local tspconfig.yaml case
            if (!tspConfigPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var validationResponse = ValidateLocalTspConfig(tspConfigPath, specCommitSha, specRepoFullName);
                if (validationResponse != null)
                {
                    return validationResponse;
                }
            }
            else
            {
                logger.LogInformation($"Remote 'tspconfig.yaml' URL detected: {tspConfigPath}.");
                if (!IsValidRemoteGitHubUrlWithCommit(tspConfigPath))
                {
                    return CreateFailureResponse($"Invalid remote GitHub URL with commit: {tspConfigPath}. The URL must include a valid commit SHA. Example: https://github.com/Azure/azure-rest-api-specs/blob/dee71463cbde1d416c47cf544e34f7966a94ddcb/specification/contosowidgetmanager/Contoso.Management/tspconfig.yaml");
                }
                // For remote tspconfig.yaml case, clear specCommitSha and specRepoFullName
                specCommitSha = string.Empty;
                specRepoFullName = string.Empty;
            }

            return await RunTspInit(localSdkRepoPath, tspConfigPath, specCommitSha, specRepoFullName, ct);
        }

        // Run tsp-client update command to re-generate the SDK code
        private async Task<DefaultCommandResponse> RunTspUpdate(string tspLocationPath, CancellationToken ct)
        {
            if (!File.Exists(tspLocationPath))
            {
                return CreateFailureResponse($"The 'tsp-location.yaml' file does not exist at the specified path: {tspLocationPath}");
            }

            logger.LogInformation($"Running tsp-client update command in directory: {Path.GetDirectoryName(tspLocationPath)}");

            var tspLocationDirectory = Path.GetDirectoryName(tspLocationPath);
            var npxOptions = new NpxOptions(
                "@azure-tools/typespec-client-generator-cli",
                ["tsp-client", "update"],
                logOutputStream: true,
                workingDirectory: tspLocationDirectory,
                timeout: TimeSpan.FromMilliseconds(commandTimeout)
            );

            var tspClientResult = await npxHelper.Run(npxOptions, ct);
            if (tspClientResult.ExitCode != 0)
            {
                return CreateFailureResponse($"tsp-client update failed with exit code {tspClientResult.ExitCode}. Output:\n{tspClientResult.Output}");
            }

            logger.LogInformation("tsp-client update completed successfully");
            return CreateSuccessResponse($"SDK re-generation completed successfully using tsp-location.yaml. Output:\n{tspClientResult.Output}");
        }

        // Run tsp-client init command to re-generate the SDK code
        private async Task<DefaultCommandResponse> RunTspInit(string localSdkRepoPath, string tspConfigPath, string specCommitSha, string specRepoFullName, CancellationToken ct)
        {
            logger.LogInformation($"Running tsp-client init command.");

            // Build arguments list dynamically
            var arguments = new List<string> { "tsp-client", "init", "--update-if-exists", "--tsp-config", tspConfigPath };
            
            if (!string.IsNullOrEmpty(specCommitSha))
            {
                arguments.Add("--commit");
                arguments.Add(specCommitSha);
            }

            if (!string.IsNullOrEmpty(specRepoFullName))
            {
                arguments.Add("--repo");
                arguments.Add(specRepoFullName);
            }

            var npxOptions = new NpxOptions(
                "@azure-tools/typespec-client-generator-cli",
                arguments.ToArray(),
                logOutputStream: true,
                workingDirectory: localSdkRepoPath,
                timeout: TimeSpan.FromMilliseconds(commandTimeout)
            );

            var tspClientResult = await npxHelper.Run(npxOptions, ct);
            if (tspClientResult.ExitCode != 0)
            {
                return CreateFailureResponse($"tsp-client init failed with exit code {tspClientResult.ExitCode}. Output:\n{tspClientResult.Output}");
            }

            logger.LogInformation("tsp-client init completed successfully");
            return CreateSuccessResponse($"SDK generation completed successfully using tspconfig.yaml. Output:\n{tspClientResult.Output}");
        }

        // Validate local tspconfig.yaml
        private DefaultCommandResponse? ValidateLocalTspConfig(string tspConfigPath, string specCommitSha, string specRepoFullName)
        {
            if (!File.Exists(tspConfigPath))
            {
                return CreateFailureResponse($"The 'tspconfig.yaml' file does not exist at the specified path: {tspConfigPath}. Prompt user to clone the azure-rest-api-specs repository locally if it does not have a local copy.");
            }

            if (string.IsNullOrEmpty(specRepoFullName))
            {
                return CreateFailureResponse($"The azure-rest-api-specs repository name is not provided. Try to get the full repository name in the format 'owner/repo'.");
            }

            if (!IsValidSha(specCommitSha))
            {
                // Try to get HEAD commit SHA from local cloned azure-rest-api-specs repo
                try
                {
                    var specRepoRoot = gitHelper.DiscoverRepoRoot(tspConfigPath);
                    using var repo = new Repository(specRepoRoot);
                    var headSha = repo.Head.Tip.Sha;
                    return CreateFailureResponse($"The provided specCommitSha ('{specCommitSha}') is not a valid commit SHA. The HEAD commit SHA of the current branch in the local cloned azure-rest-api-specs repo is: {headSha}. Please use this value as the commit SHA.");
                }
                catch (Exception ex)
                {
                    return CreateFailureResponse($"Invalid commit SHA provided and failed to discover local azure-rest-api-specs repo: {ex.Message}. Please provide a valid commit SHA or ensure the azure-rest-api-specs repo is cloned.");
                }
            }

            return null;
        }

        // Gets the script path from the configuration file.
        private async Task<string> GetScriptPathFromConfigAsync(string repositoryRoot, string jsonPath)
        {
            // Construct configuration file path
            var configFilePath = Path.Combine(repositoryRoot, "eng", "spec-gen-sdk-config.json");
            logger.LogInformation($"Configuration file path: {configFilePath}");

            if (!File.Exists(configFilePath))
            {
                throw new FileNotFoundException($"Configuration file not found at: {configFilePath}");
            }

            try
            {
                // Read and parse the configuration file
                var configContent = await File.ReadAllTextAsync(configFilePath);
                using var configJson = JsonDocument.Parse(configContent);

                // Use helper method to navigate JSON path
                var (found, element) = TryGetJsonElementByPath(configJson.RootElement, jsonPath);
                if (!found)
                {
                    throw new InvalidOperationException($"Property not found at JSON path '{jsonPath}' in configuration file {configFilePath}.");
                }

                var scriptPath = element.GetString();
                if (string.IsNullOrEmpty(scriptPath))
                {
                    throw new InvalidOperationException($"Script path is empty at JSON path '{jsonPath}' in configuration file {configFilePath}.");
                }

                return scriptPath;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Error parsing JSON configuration: {ex.Message}", ex);
            }
        }

        // Try to get a JSON element by its path
        private (bool found, JsonElement element) TryGetJsonElementByPath(JsonElement root, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return (false, default);
            }

            var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            JsonElement current = root;

            foreach (var part in pathParts)
            {
                if (!current.TryGetProperty(part, out current))
                {
                    return (false, default);
                }
            }

            return (true, current);
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

        // Validate remote GitHub URL with commit SHA
        private bool IsValidRemoteGitHubUrlWithCommit(string tspConfigPath)
        {
            // Must contain /blob/ pattern
            if (!tspConfigPath.Contains("/blob/"))
            {
                return false;
            }

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

        // Helper method to create failure responses along with setting the failure state
        private DefaultCommandResponse CreateFailureResponse(string message)
        {
            SetFailure();
            return new DefaultCommandResponse
            {
                Result = "failed",
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
