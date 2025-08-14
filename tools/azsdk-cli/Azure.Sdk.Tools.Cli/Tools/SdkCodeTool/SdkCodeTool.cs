using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.TeamFoundation.Common;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [McpServerToolType, Description("This type contains the tools to generate SDK code and build SDK code.")]
    public class SdkCodeTool(IProcessHelper processHelper, ILogger<SdkCodeTool> logger, IOutputService output) : MCPTool
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
        private readonly Option<string> tspConfigPathOpt = new(["--tsp-config-path", "-t"], "Absolute path to the 'tspconfig.yaml' configuration file") { IsRequired = false };
        private readonly Option<string> commitShaOpt = new(["--commit-sha", "-c"], "The head commit SHA of the local cloned azure-rest-api-specs repository") { IsRequired = false };
        private readonly Option<string> specRepoFullNameOpt = new(["--spec-repo-full-name", "-s"], "The owner and name of the azure-rest-api-specs repository") { IsRequired = false };
        private readonly Option<string> tspLocationPathOpt = new(["--tsp-location-path", "-l"], "Absolute path to the 'tsp-location.yaml' configuration file") { IsRequired = false };
        private readonly Option<string> emitterOpt = new(["--emitter-options", "-o"], "The emitter options to pass to the tsp command") { IsRequired = false };

        public override Command GetCommand()
        {
            var command = new Command("code", "Azure SDK code tools");
            var subCommands = new[]
            {
                new Command(generateSdkCommandName, "Generates SDK code for a specified language based on provided 'tspconfig.yaml' or 'tsp-location.yaml'.") { localSdkRepoPathOpt, tspConfigPathOpt, commitShaOpt, specRepoFullNameOpt, tspLocationPathOpt, emitterOpt },
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
            var result = string.Empty;

            switch (command)
            {
                case generateSdkCommandName:
                    var localSdkRepoPath = commandParser.GetValueForOption(localSdkRepoPathOpt);
                    var tspConfigPath = commandParser.GetValueForOption(tspConfigPathOpt);
                    var commitSha = commandParser.GetValueForOption(commitShaOpt);
                    var specRepoFullName = commandParser.GetValueForOption(specRepoFullNameOpt);
                    var tspLocationPath = commandParser.GetValueForOption(tspLocationPathOpt);
                    var emitterOptions = commandParser.GetValueForOption(emitterOpt);
                    result = GenerateSdk(localSdkRepoPath, tspConfigPath, commitSha, specRepoFullName, tspLocationPath, emitterOptions);
                    ctx.ExitCode = ExitCode;
                    output.Output(result);
                    break;
                case buildSdkCommandName:
                    var language = commandParser.GetValueForOption(languageOpt);
                    var projectPath = commandParser.GetValueForOption(projectPathOpt);
                    result = await BuildSdkAsync(language, projectPath);
                    ctx.ExitCode = ExitCode;
                    output.Output(result);
                    break;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    break;
            }

            return;
        }

        [McpServerTool(Name = "azsdk_code_generate"), Description("Generates SDK code for a specified language based on the provided 'tspconfig.yaml' or 'tsp-location.yaml'.")]
        public string GenerateSdk(string localSdkRepoPath, string tspConfigPath, string commitSha, string specRepoFullName, string tspLocationPath, string emitterOptions)
        {
            try
            {
                if (string.IsNullOrEmpty(tspConfigPath) && string.IsNullOrEmpty(tspLocationPath))
                {
                    return "Both 'tspconfig.yaml' and 'tsp-location.yaml' paths aren't provided. At least one of them is required.";
                }

                if (!string.IsNullOrEmpty(tspLocationPath))
                {
                    if (!File.Exists(tspLocationPath))
                    {
                        return $"The 'tsp-location.yaml' file does not exist at the specified path: {tspLocationPath}";
                    }
                    // run "tsp-client update --save-inputs" command to regenerate SDK code on the folder of tspLocationPath
                    logger.LogInformation($"Running tsp-client update command in directory: {Path.GetDirectoryName(tspLocationPath)}");
            
                    var tspLocationDirectory = Path.GetDirectoryName(tspLocationPath);                    
                    var tspClientResult = processHelper.RunProcess("tsp-client", new[] { "update", "--save-inputs" }, tspLocationDirectory, commandTimeout);
                    if (tspClientResult.ExitCode != 0)
                    {
                        SetFailure(1);
                        return $"tsp-client update failed with exit code {tspClientResult.ExitCode}. Output:\n{tspClientResult.Output}";
                    }
                    
                    logger.LogInformation("tsp-client update completed successfully");
                    return $"SDK re-generation completed successfully using tsp-location.yaml. Output:\n{tspClientResult.Output}";
                }
                else
                {
                    // handle tspConfigPath case
                    if (!File.Exists(tspConfigPath))
                    {
                        return $"The 'tspconfig.yaml' file does not exist at the specified path: {tspConfigPath}";
                    }

                    if (string.IsNullOrEmpty(localSdkRepoPath) || !Directory.Exists(localSdkRepoPath))
                    {
                        return $"The directory for the local sdk repo does not provide or exist at the specified path: {localSdkRepoPath}. Prompt user to clone the matched SDK repository user want to generate SDK for.";
                    }

                    if (string.IsNullOrEmpty(specRepoFullName))
                    {
                        return $"The azure-rest-api-specs repository name is not provided. Try to get the full repository name in the format 'owner/repo'.";
                    }

                    // invoke command "tsp-client init --update-if-exists --tsp-config $tspConfigPath --repo $sdkRepoName --commit $commitSha" on root directory of sdk repo
                    logger.LogInformation($"Running tsp-client init command with tspconfig: {tspConfigPath}");
                    
                    var typespecProjectPath = Path.GetDirectoryName(tspConfigPath);
                    string[] args = { "init", "--update-if-exists", "--tsp-config", tspConfigPath, "--repo", specRepoFullName, "--commit", commitSha, "--local-spec-repo", typespecProjectPath };
                    var tspInitResult = processHelper.RunProcess("tsp-client", args, localSdkRepoPath, commandTimeout);

                    if (tspInitResult.ExitCode != 0)
                    {
                        SetFailure(1);
                        return $"tsp-client init failed with exit code {tspInitResult.ExitCode}. Output:\n{tspInitResult.Output}";
                    }
                    
                    logger.LogInformation("tsp-client init completed successfully");
                    return $"SDK generation completed successfully using tspconfig.yaml. Output:\n{tspInitResult.Output}";
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while generating SDK");
                SetFailure(1);
                return $"An error occurred: {ex.Message}";
            }
        }

        [McpServerTool(Name = "azsdk_code_build"), Description("Build sdk code for a specified project locally.")]
        public async Task<string> BuildSdkAsync(string language, string projectPath)
        {
            try
            {
                logger.LogInformation($"Building SDK for language: {language}");
                logger.LogInformation($"SDK project path: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    return "Invalid project path. Please provide a valid project path.";
                }
                
                if (!ValidLanguages.Contains(language.ToLower()))
                {
                    return "Invalid language. Please provide a valid SDK language.";
                }

                // Get repository root path from project path
                var repositoryRoot = GetRepositoryRoot(projectPath);
                if (string.IsNullOrEmpty(repositoryRoot))
                {
                    return "Could not find repository root from the provided project path.";
                }
                
                logger.LogInformation($"Repository root path: {repositoryRoot}");

                // Construct configuration file path
                var configFilePath = Path.Combine(repositoryRoot, "eng", "spec-gen-sdk-config.json");
                logger.LogInformation($"Configuration file path: {configFilePath}");

                if (!File.Exists(configFilePath))
                {
                    return $"Configuration file not found at: {configFilePath}";
                }

                // Read and parse the configuration file
                var configContent = await File.ReadAllTextAsync(configFilePath);
                var configJson = JsonDocument.Parse(configContent);

                // Get the build script path from JSON path "packageOptions/buildScript/path"
                if (!configJson.RootElement.TryGetProperty("packageOptions", out var packageOptions) ||
                    !packageOptions.TryGetProperty("buildScript", out var buildScript) ||
                    !buildScript.TryGetProperty("path", out var pathElement))
                {
                    return "Build script path not found in configuration file at packageOptions/buildScript/path";
                }

                var buildScriptPath = pathElement.GetString();
                if (string.IsNullOrEmpty(buildScriptPath))
                {
                    return "Build script path is empty in configuration file";
                }

                logger.LogInformation($"Build script path: {buildScriptPath}");

                // Resolve the full path of the build script
                var fullBuildScriptPath = Path.IsPathRooted(buildScriptPath) 
                    ? buildScriptPath 
                    : Path.Combine(repositoryRoot, buildScriptPath);

                if (!File.Exists(fullBuildScriptPath))
                {
                    return $"Build script not found at: {fullBuildScriptPath}";
                }

                // Run the build script
                logger.LogInformation($"Executing build script: {fullBuildScriptPath}");

                var buildResult = processHelper.RunProcess(fullBuildScriptPath, new[] { "--module-dir", projectPath }, repositoryRoot, commandTimeout);
                if (buildResult.ExitCode != 0)
                {
                    SetFailure(1);
                    return $"Build script failed with exit code {buildResult.ExitCode}. Output:\n{buildResult.Output}";
                }
                logger.LogInformation("Build script execution completed");
                return $"Build completed successfully. Output:\n{buildResult.Output}";
            }
            catch (Exception ex)
            {
                SetFailure(1);
                return $"An error occurred: {ex.Message}";
            }
        }

        private string GetRepositoryRoot(string projectPath)
        {
            var currentDir = new DirectoryInfo(projectPath);
            
            while (currentDir != null)
            {
                // Check for common repository indicators or eng folder
                if (Directory.Exists(Path.Combine(currentDir.FullName, ".git")) ||
                    Directory.Exists(Path.Combine(currentDir.FullName, "eng")))
                {
                    return currentDir.FullName;
                }
                
                currentDir = currentDir.Parent;
            }
            
            return null;
        }
    }
}
