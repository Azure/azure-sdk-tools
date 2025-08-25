using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using LibGit2Sharp;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [McpServerToolType, Description("This type contains the tools to generate SDK code and build SDK code.")]
    public class SdkCodeTool(IProcessHelper processHelper, ILogger<SdkCodeTool> logger, IOutputService output, IGitHelper gitHelper) : MCPTool
    {
        // Command names
        private const string generateSdkCommandName = "generate";
        private const string buildSdkCommandName = "build";

        private const int commandTimeout = 30 * 60 * 1000; // 30 mins
        public static readonly string[] ValidLanguages = { ".net", "go", "java", "javascript", "python"};

        // Build command options
        private readonly Option<string> languageOpt = new(["--language", "-l"], "Programming language for the SDK") { IsRequired = true };
        private readonly Option<string> projectPathOpt = new(["--project-path", "-p"], "Absolute path to the SDK project") { IsRequired = true };

        // Generate command options
        private readonly Option<string> localSdkRepoPathOpt = new(["--local-sdk-repo-path", "-r"], "The absolute root path to the local azure SDK repository") { IsRequired = false };
        private readonly Option<string> tspConfigPathOpt = new(["--tsp-config-path", "-t"], "Path to the 'tspconfig.yaml' configuration file, it can be a local path or remote URL") { IsRequired = false };
        private readonly Option<string> specCommitShaOpt = new(["--spec-commit-sha", "-c"], "The head commit SHA of the local cloned azure-rest-api-specs repository") { IsRequired = false };
        private readonly Option<string> specRepoFullNameOpt = new(["--spec-repo-full-name", "-s"], "The owner and name of the azure-rest-api-specs repository") { IsRequired = false };
        private readonly Option<string> tspLocationPathOpt = new(["--tsp-location-path", "-l"], "Absolute path to the 'tsp-location.yaml' configuration file") { IsRequired = false };
        private readonly Option<string> emitterOpt = new(["--emitter-options", "-o"], "The emitter options to pass to the tsp command") { IsRequired = false };

        public override Command GetCommand()
        {
            var command = new Command("code", "Azure SDK code tools");
            var subCommands = new[]
            {
                new Command(generateSdkCommandName, "Generates SDK code for a specified language based on provided 'tspconfig.yaml' or 'tsp-location.yaml'.") { localSdkRepoPathOpt, tspConfigPathOpt, specCommitShaOpt, specRepoFullNameOpt, tspLocationPathOpt, emitterOpt },
                new Command(buildSdkCommandName, "Builds SDK code for a specified language and project.") { languageOpt, projectPathOpt },
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
                    var generateResult = await GenerateSdk(localSdkRepoPath, tspConfigPath, specCommitSha, specRepoFullName, tspLocationPath, emitterOptions);
                    ctx.ExitCode = ExitCode;
                    output.Output(generateResult);
                    break;
                case buildSdkCommandName:
                    var language = commandParser.GetValueForOption(languageOpt);
                    var projectPath = commandParser.GetValueForOption(projectPathOpt);
                    var buildResult = await BuildSdkAsync(language, projectPath);
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

        [McpServerTool(Name = "azsdk_code_generate"), Description("Generates SDK code for a specified language based on the provided 'tspconfig.yaml' or 'tsp-location.yaml'.")]
        public async Task<DefaultCommandResponse> GenerateSdk(string localSdkRepoPath, string tspConfigPath, string specCommitSha, string specRepoFullName, string tspLocationPath, string emitterOptions)
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
                    return RunTspUpdate(tspLocationPath);
                }

                // Handle tspconfig.yaml case
                return await GenerateSdkFromTspConfigAsync(localSdkRepoPath, tspConfigPath, specCommitSha, specRepoFullName, emitterOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while generating SDK");
                return CreateFailureResponse($"An error occurred: {ex.Message}");
            }
        }

        [McpServerTool(Name = "azsdk_code_build"), Description("Build sdk code for a specified project locally.")]
        public async Task<DefaultCommandResponse> BuildSdkAsync(string language, string projectPath)
        {
            try
            {
                logger.LogInformation($"Building SDK for language: {language}, project path: {projectPath}");
                
                // Validate inputs
                if (!Directory.Exists(projectPath))
                {
                    return CreateFailureResponse("Invalid project path. Please provide a valid project path.");
                }
                
                if (!ValidLanguages.Contains(language.ToLower()))
                {
                    return CreateFailureResponse($"Invalid language '{language}'. Please provide a valid SDK language. Valid languages are: {string.Join(", ", ValidLanguages)}");
                }

                // Get repository root path from project path
                string sdkRepoRoot;
                try
                {
                    sdkRepoRoot = gitHelper.DiscoverRepoRoot(projectPath);
                }
                catch (Exception ex)
                {
                    return CreateFailureResponse($"Failed to discover local sdk repo root: {ex.Message} with {projectPath}.");
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
                var buildResult = processHelper.RunProcess(fullBuildScriptPath, new[] { "--module-dir", projectPath }, sdkRepoRoot, commandTimeout);
                if (buildResult.ExitCode != 0)
                {
                    return CreateFailureResponse($"Build script failed with exit code {buildResult.ExitCode}. Output:\n{buildResult.Output}");
                }

                logger.LogInformation("Build script execution completed");
                return CreateSuccessResponse($"Build completed successfully. Output:\n{buildResult.Output}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while building SDK");
                return CreateFailureResponse($"An error occurred: {ex.Message}");
            }
        }

        // Run language-specific script to generate the SDK code from 'tspconfig.yaml'
        private async Task<DefaultCommandResponse> GenerateSdkFromTspConfigAsync(string localSdkRepoPath, string tspConfigPath, string specCommitSha, string specRepoFullName, string emitterOptions)
        {
            // Validate inputs
            if (string.IsNullOrEmpty(localSdkRepoPath) || !Directory.Exists(localSdkRepoPath))
            {
                return CreateFailureResponse($"The directory for the local sdk repo does not provide or exist at the specified path: {localSdkRepoPath}. Prompt user to clone the matched SDK repository users want to generate SDK against.");
            }

            // Get the generate script path
            string sdkRepoRoot;
            try
            {
                sdkRepoRoot = gitHelper.DiscoverRepoRoot(localSdkRepoPath);
            }
            catch (Exception ex)
            {
                return CreateFailureResponse($"Failed to discover local sdk repo: {ex.Message} with path: {localSdkRepoPath}.");
            }

            string fullGenerateScriptPath;
            try
            {
                var generateScriptPath = await GetScriptPathFromConfigAsync(sdkRepoRoot, "generateOptions/generateScript/path");
                logger.LogInformation($"Generate script path: {generateScriptPath}");

                // Resolve the full path of the generate script
                fullGenerateScriptPath = Path.IsPathRooted(generateScriptPath)
                    ? generateScriptPath
                    : Path.Combine(sdkRepoRoot, generateScriptPath);

                if (!File.Exists(fullGenerateScriptPath))
                {
                    return CreateFailureResponse($"Generate script not found at: {fullGenerateScriptPath}");
                }
            }
            catch (Exception ex)
            {
                return CreateFailureResponse($"Failed to get generate script path: {ex.Message}");
            }

            // Prepare script arguments based on config type
            string[] generateScriptArgs = PrepareScriptArguments(tspConfigPath, specCommitSha, specRepoFullName);
            
            // Validate arguments for local tspconfig.yaml case
            if (!tspConfigPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                var validationResponse = ValidateLocalTspConfig(tspConfigPath, specCommitSha, specRepoFullName);
                if (validationResponse != null)
                {
                    return validationResponse;
                }
            }

            var generateResult = processHelper.RunProcess(fullGenerateScriptPath, generateScriptArgs, sdkRepoRoot, commandTimeout);
            if (generateResult.ExitCode != 0)
            {
                return CreateFailureResponse($"Generate script failed with exit code {generateResult.ExitCode}. Output:\n{generateResult.Output}");
            }

            return CreateSuccessResponse($"Generate completed successfully. Output:\n{generateResult.Output}");
        }

        // Run tsp-client update command to re-generate the SDK code
        private DefaultCommandResponse RunTspUpdate(string tspLocationPath)
        {
            if (!File.Exists(tspLocationPath))
            {
                return CreateFailureResponse($"The 'tsp-location.yaml' file does not exist at the specified path: {tspLocationPath}");
            }

            logger.LogInformation($"Running tsp-client update command in directory: {Path.GetDirectoryName(tspLocationPath)}");

            var tspLocationDirectory = Path.GetDirectoryName(tspLocationPath);
            var tspClientResult = processHelper.RunProcess("tsp-client", new[] { "update", "--save-inputs" }, tspLocationDirectory, commandTimeout);

            if (tspClientResult.ExitCode != 0)
            {
                return CreateFailureResponse($"tsp-client update failed with exit code {tspClientResult.ExitCode}. Output:\n{tspClientResult.Output}");
            }

            logger.LogInformation("tsp-client update completed successfully");
            return CreateSuccessResponse($"SDK re-generation completed successfully using tsp-location.yaml. Output:\n{tspClientResult.Output}");
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

        // Helper method to prepare script arguments
        private string[] PrepareScriptArguments(string tspConfigPath, string specCommitSha, string specRepoFullName)
        {
            if (tspConfigPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return new[] {
                    "--tsp-config-path", tspConfigPath,
                    "--run-mode", "mcp"
                };
            }
            else
            {
                return new[] {
                    "--tsp-config-path", tspConfigPath,
                    "--head-sha", specCommitSha,
                    "--spec-repo-name", specRepoFullName,
                    "--run-mode", "mcp"
                };
            }
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
            return !string.IsNullOrEmpty(sha) && sha.Length == 40 && sha.All(c => "0123456789abcdefABCDEF".Contains(c));
        }

        // Helper method to create failure responses along with setting the failure state
        private DefaultCommandResponse CreateFailureResponse(string message)
        {
            SetFailure();
            return new DefaultCommandResponse
            {
                Result = "failed",
                Message = message
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
