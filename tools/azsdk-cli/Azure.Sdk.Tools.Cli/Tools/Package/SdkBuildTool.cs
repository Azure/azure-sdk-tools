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
    [McpServerToolType, Description("This type contains the tools to build SDK code locally.")]
    public class SdkBuildTool: MCPTool
    {
        // Command names
        private const string buildSdkCommandName = "build";
        private const int commandTimeoutInMinutes = 30;

        private readonly IOutputHelper output;
        private readonly IProcessHelper processHelper;
        private readonly IGitHelper gitHelper;
        private readonly ILogger<SdkBuildTool> logger;

        public SdkBuildTool(IGitHelper gitHelper, ILogger<SdkBuildTool> logger, IOutputHelper output, IProcessHelper processHelper): base()
        {
            this.gitHelper = gitHelper;
            this.logger = logger;
            this.output = output;
            this.processHelper = processHelper;
            CommandHierarchy = [ SharedCommandGroups.Package, SharedCommandGroups.SourceCode ];
        }

        public override Command GetCommand()
        {
            var command = new Command(buildSdkCommandName, "Builds SDK source code for a specified language and project.");
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            command.AddOption(SharedOptions.PackagePath);

            return command;
        }

        public async override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;
            var commandParser = ctx.ParseResult;
            var packagePath = commandParser.GetValueForOption(SharedOptions.PackagePath);
            var buildResult = await BuildSdkAsync(packagePath, ct);
            ctx.ExitCode = ExitCode;
            output.Output(buildResult);
        }

        [McpServerTool(Name = "azsdk_package_build_code"), Description("Build SDK code for a specified project locally.")]
        public async Task<DefaultCommandResponse> BuildSdkAsync(
            [Description("Absolute path to the SDK project.")]
            string packagePath,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation($"Building SDK for project path: {packagePath}");

                // Validate inputs
                if (string.IsNullOrEmpty(packagePath))
                {
                    return CreateFailureResponse("Package path is required.");
                }

                if (!Directory.Exists(packagePath))
                {
                    return CreateFailureResponse($"Path does not exist: {packagePath}");
                }

                // Return if the project is python project
                if (packagePath.Contains("azure-sdk-for-python", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                    return CreateSuccessResponse("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                }

                // Get repository root path from project path
                string sdkRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
                if (string.IsNullOrEmpty(sdkRepoRoot))
                {
                    return CreateFailureResponse($"Failed to discover local sdk repo with project-path: {packagePath}.");
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
                    ["--module-dir", packagePath],
                    logOutputStream: true,
                    workingDirectory: sdkRepoRoot,
                    timeout: TimeSpan.FromMinutes(commandTimeoutInMinutes)
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
