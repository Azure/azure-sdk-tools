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
    [McpServerToolType, Description("This type contains the tools to build/compile SDK code locally.")]
    public class SdkBuildTool: MCPTool
    {
        // Command names
        private const string BuildSdkCommandName = "build";
        private const string AzureSdkForPythonRepoName = "azure-sdk-for-python";
        private const int CommandTimeoutInMinutes = 30;

        private readonly IOutputHelper _output;
        private readonly IProcessHelper _processHelper;
        private readonly IGitHelper _gitHelper;
        private readonly ISdkRepoConfigHelper _sdkRepoConfigHelper;
        private readonly ILogger<SdkBuildTool> _logger;

        public SdkBuildTool(IGitHelper gitHelper, ILogger<SdkBuildTool> logger, IOutputHelper output, IProcessHelper processHelper, ISdkRepoConfigHelper sdkRepoConfigHelper): base()
        {
            _gitHelper = gitHelper;
            _logger = logger;
            _output = output;
            _processHelper = processHelper;
            _sdkRepoConfigHelper = sdkRepoConfigHelper;
            CommandHierarchy = [ SharedCommandGroups.Package, SharedCommandGroups.SourceCode ];
        }

        public override Command GetCommand()
        {
            var command = new Command(BuildSdkCommandName, "Builds SDK source code for a specified language and project.");
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
            _output.Output(buildResult);
        }

        [McpServerTool(Name = "azsdk_package_build_code"), Description("Build/compile SDK code for a specified project locally.")]
        public async Task<DefaultCommandResponse> BuildSdkAsync(
            [Description("Absolute path to the SDK project.")]
            string packagePath,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation($"Building SDK for project path: {packagePath}");

                // Validate inputs
                if (string.IsNullOrEmpty(packagePath))
                {
                    return CreateFailureResponse("Package path is required.");
                }

                if (!Directory.Exists(packagePath))
                {
                    return CreateFailureResponse($"Path does not exist: {packagePath}");
                }

                // Get repository root path from project path
                string sdkRepoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
                if (string.IsNullOrEmpty(sdkRepoRoot))
                {
                    return CreateFailureResponse($"Failed to discover local sdk repo with project-path: {packagePath}.");
                }

                _logger.LogInformation($"Repository root path: {sdkRepoRoot}");

                string sdkRepoName = _gitHelper.GetRepoName(sdkRepoRoot);
                _logger.LogInformation($"Repository name: {sdkRepoName}");

                // Return if the project is python project
                if (sdkRepoName.Contains(AzureSdkForPythonRepoName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                    return CreateSuccessResponse("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                }

                // Get the build configuration (command or script path)
                ProcessOptions options;
                try
                {
                    var (configType, configValue) = await _sdkRepoConfigHelper.GetBuildConfigurationAsync(sdkRepoRoot);

                    if (configType == BuildConfigType.Command)
                    {
                        // Execute as command
                        var variables = new Dictionary<string, string>
                        {
                            { "packagePath", packagePath }
                        };

                        var substitutedCommand = _sdkRepoConfigHelper.SubstituteCommandVariables(configValue, variables);
                        _logger.LogInformation($"Executing build command: {substitutedCommand}");

                        var commandParts = _sdkRepoConfigHelper.ParseCommand(substitutedCommand);
                        if (commandParts.Length == 0)
                        {
                            return CreateFailureResponse($"Invalid build command: {substitutedCommand}");
                        }

                        options = new ProcessOptions(
                            commandParts[0],
                            commandParts.Skip(1).ToArray(),
                            logOutputStream: true,
                            workingDirectory: packagePath,
                            timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
                        );
                    }
                    else // BuildConfigType.ScriptPath
                    {
                        // Execute as script file
                        var fullBuildScriptPath = Path.IsPathRooted(configValue)
                            ? configValue
                            : Path.Combine(sdkRepoRoot, configValue);

                        if (!File.Exists(fullBuildScriptPath))
                        {
                            return CreateFailureResponse($"Build script not found at: {fullBuildScriptPath}");
                        }

                        _logger.LogInformation($"Executing build script file: {fullBuildScriptPath}");

                        options = new ProcessOptions(
                            fullBuildScriptPath,
                            ["--package-path", packagePath],
                            logOutputStream: true,
                            workingDirectory: sdkRepoRoot,
                            timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
                        );
                    }
                }
                catch (Exception ex)
                {
                    return CreateFailureResponse($"Failed to get build configuration: {ex.Message}");
                }

                // Run the build script or command
                _logger.LogInformation($"Executing build process...");
                var buildResult = await _processHelper.Run(options, ct);
                var trimmedBuildResult = (buildResult.Output ?? string.Empty).Trim();
                if (buildResult.ExitCode != 0)
                {
                    return CreateFailureResponse($"Build process failed with exit code {buildResult.ExitCode}. Output:\n{trimmedBuildResult}");
                }

                _logger.LogInformation("Build process execution completed");
                return CreateSuccessResponse($"Build completed successfully. Output:\n{trimmedBuildResult}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while building SDK");
                return CreateFailureResponse($"An error occurred: {ex.Message}");
            }
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
