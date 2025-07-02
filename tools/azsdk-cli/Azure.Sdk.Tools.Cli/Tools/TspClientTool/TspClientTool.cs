// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TspClientTool
{
    [Description("Tools for generating SDKs using tsp-client from TypeSpec projects")]
    [McpServerToolType]
    public class TspClientTool(ILogger<TspClientTool> logger, IOutputService output, ITypeSpecHelper typeSpecHelper) : MCPTool
    {
        private const string initCommandName = "init";
        private const string updateCommandName = "update";
        private const string syncCommandName = "sync";
        private const string generateCommandName = "generate";

        public new CommandGroup[] CommandHierarchy { get; set; } = [
            new CommandGroup("tsp-client", "TypeSpec client library generation using tsp-client")
        ];

        // Common options
        private readonly Option<string> outputDirOpt = new(["--output-dir", "-o"], "Specify an alternate output directory for the generated files") { };
        private readonly Option<bool> debugOpt = new(["--debug", "-d"], "Enable debug logging") { };
        private readonly Option<bool> noPromptOpt = new(["--no-prompt", "-y"], "Skip any interactive prompts") { };

        // Init command options
        private readonly Option<string> tspConfigOpt = new(["--tsp-config", "-c"], "Path to tspconfig.yaml") { IsRequired = true };
        private readonly Option<bool> skipSyncAndGenerateOpt = new(["--skip-sync-and-generate"], "Skip syncing and generating the TypeSpec project") { };
        private readonly Option<string> localSpecRepoOpt = new(["--local-spec-repo"], "Path to local repository with the TypeSpec project") { };
        private readonly Option<bool> saveInputsOpt = new(["--save-inputs"], "Don't clean up the temp directory after generation") { };
        private readonly Option<string> emitterOptionsOpt = new(["--emitter-options"], "The options to pass to the emitter") { };
        private readonly Option<string> commitOpt = new(["--commit"], "Commit hash to be used") { };
        private readonly Option<string> repoOpt = new(["--repo"], "Repository where the project is defined") { };
        private readonly Option<bool> skipInstallOpt = new(["--skip-install"], "Skip installing dependencies") { };
        private readonly Option<string> emitterPackageJsonPathOpt = new(["--emitter-package-json-path"], "Alternate path for emitter-package.json file") { };
        private readonly Option<string[]> traceOpt = new(["--trace"], "Enable tracing during compile") { };

        public override Command GetCommand()
        {
            var rootCommand = new Command("tsp-client", "TypeSpec client library generation using tsp-client");

            // Add global options
            rootCommand.AddGlobalOption(outputDirOpt);
            rootCommand.AddGlobalOption(debugOpt);
            rootCommand.AddGlobalOption(noPromptOpt);

            // Add subcommands
            var initCommand = new Command(initCommandName, "Initialize the SDK project folder from a tspconfig.yaml")
            {
                tspConfigOpt,
                skipSyncAndGenerateOpt,
                localSpecRepoOpt,
                saveInputsOpt,
                emitterOptionsOpt,
                commitOpt,
                repoOpt,
                skipInstallOpt,
                emitterPackageJsonPathOpt,
                traceOpt
            };

            var updateCommand = new Command(updateCommandName, "Sync and generate from a TypeSpec project")
            {
                repoOpt,
                commitOpt,
                tspConfigOpt,
                localSpecRepoOpt,
                emitterOptionsOpt,
                saveInputsOpt,
                skipInstallOpt,
                traceOpt
            };

            var syncCommand = new Command(syncCommandName, "Sync TypeSpec project specified in tsp-location.yaml")
            {
                localSpecRepoOpt
            };

            var generateCommand = new Command(generateCommandName, "Generate from a TypeSpec project")
            {
                emitterOptionsOpt,
                saveInputsOpt,
                skipInstallOpt,
                traceOpt
            };

            // Set handlers
            initCommand.SetHandler(async ctx => await HandleCommand(ctx, ctx.GetCancellationToken()));
            updateCommand.SetHandler(async ctx => await HandleCommand(ctx, ctx.GetCancellationToken()));
            syncCommand.SetHandler(async ctx => await HandleCommand(ctx, ctx.GetCancellationToken()));
            generateCommand.SetHandler(async ctx => await HandleCommand(ctx, ctx.GetCancellationToken()));

            // Add subcommands to root
            rootCommand.AddCommand(initCommand);
            rootCommand.AddCommand(updateCommand);
            rootCommand.AddCommand(syncCommand);
            rootCommand.AddCommand(generateCommand);

            return rootCommand;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var commandResult = ctx.ParseResult.CommandResult;
                var commandName = commandResult.Command.Name;

                logger.LogInformation("Executing tsp-client command: {CommandName}", commandName);

                var response = commandName switch
                {
                    initCommandName => await ExecuteInitCommand(ctx),
                    updateCommandName => await ExecuteUpdateCommand(ctx),
                    syncCommandName => await ExecuteSyncCommand(ctx),
                    generateCommandName => await ExecuteGenerateCommand(ctx),
                    _ => new DefaultCommandResponse { ResponseError = $"Unknown command: {commandName}" }
                };

                ctx.ExitCode = ExitCode;
                output.Output(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing tsp-client command");
                SetFailure(1);
                ctx.ExitCode = ExitCode;
                output.Output(new DefaultCommandResponse
                {
                    ResponseError = $"Error executing tsp-client command: {ex.Message}"
                });
            }

            await Task.CompletedTask;
        }

        [McpServerTool(Name = "tsp-client-init"), Description("Initialize SDK project folder from tspconfig.yaml")]
        public async Task<DefaultCommandResponse> InitializeProject(
            string tspConfigPath,
            string? outputDir = null,
            bool skipSyncAndGenerate = false,
            string? localSpecRepo = null,
            string? commit = null,
            string? repo = null,
            bool skipInstall = false,
            string? emitterOptions = null,
            bool saveInputs = false,
            bool debug = false)
        {
            try
            {
                // Validate tspconfig path exists
                if (!File.Exists(tspConfigPath) && !tspConfigPath.StartsWith("http"))
                {
                    // Check if it's a directory containing tspconfig.yaml
                    var tspConfigInDir = Path.Combine(tspConfigPath, "tspconfig.yaml");
                    if (Directory.Exists(tspConfigPath) && File.Exists(tspConfigInDir))
                    {
                        tspConfigPath = tspConfigInDir;
                    }
                    else if (Directory.Exists(tspConfigPath) && typeSpecHelper.IsValidTypeSpecProjectPath(tspConfigPath))
                    {
                        // Use the helper to validate the path
                        logger.LogInformation("Valid TypeSpec project path detected: {Path}", tspConfigPath);
                    }
                    else
                    {
                        return new DefaultCommandResponse
                        {
                            ResponseError = $"TypeSpec config file not found: {tspConfigPath}. Please provide a valid path to tspconfig.yaml or a directory containing it."
                        };
                    }
                }

                var args = new List<string> { "init", "--tsp-config", tspConfigPath };

                AddOptionalArg(args, "--output-dir", outputDir);
                AddOptionalArg(args, "--local-spec-repo", localSpecRepo);
                AddOptionalArg(args, "--commit", commit);
                AddOptionalArg(args, "--repo", repo);
                AddOptionalArg(args, "--emitter-options", emitterOptions);

                if (skipSyncAndGenerate) args.Add("--skip-sync-and-generate");
                if (skipInstall) args.Add("--skip-install");
                if (saveInputs) args.Add("--save-inputs");
                if (debug) args.Add("--debug");

                var result = await ExecuteTspClientCommand(args);
                return new DefaultCommandResponse
                {
                    Message = result.Success ? "Project initialized successfully" : "Project initialization failed",
                    ResponseError = result.Success ? null : result.ErrorMessage,
                    Duration = result.Duration
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error initializing project with tsp-client");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Error initializing project: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "tsp-client-update"), Description("Sync and generate from TypeSpec project")]
        public async Task<DefaultCommandResponse> UpdateProject(
            string? outputDir = null,
            string? repo = null,
            string? commit = null,
            string? tspConfig = null,
            string? localSpecRepo = null,
            string? emitterOptions = null,
            bool saveInputs = false,
            bool skipInstall = false,
            bool debug = false)
        {
            try
            {
                var args = new List<string> { "update" };

                AddOptionalArg(args, "--output-dir", outputDir);
                AddOptionalArg(args, "--repo", repo);
                AddOptionalArg(args, "--commit", commit);
                AddOptionalArg(args, "--tsp-config", tspConfig);
                AddOptionalArg(args, "--local-spec-repo", localSpecRepo);
                AddOptionalArg(args, "--emitter-options", emitterOptions);

                if (saveInputs) args.Add("--save-inputs");
                if (skipInstall) args.Add("--skip-install");
                if (debug) args.Add("--debug");

                var result = await ExecuteTspClientCommand(args);
                return new DefaultCommandResponse
                {
                    Message = result.Success ? "Project updated successfully" : "Project update failed",
                    ResponseError = result.Success ? null : result.ErrorMessage,
                    Duration = result.Duration
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating project with tsp-client");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Error updating project: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "tsp-client-generate"), Description("Generate client library from TypeSpec project")]
        public async Task<DefaultCommandResponse> GenerateClientLibrary(
            string? outputDir = null,
            string? emitterOptions = null,
            bool saveInputs = false,
            bool skipInstall = false,
            bool debug = false)
        {
            try
            {
                var args = new List<string> { "generate" };

                AddOptionalArg(args, "--output-dir", outputDir);
                AddOptionalArg(args, "--emitter-options", emitterOptions);

                if (saveInputs) args.Add("--save-inputs");
                if (skipInstall) args.Add("--skip-install");
                if (debug) args.Add("--debug");

                var result = await ExecuteTspClientCommand(args);
                return new DefaultCommandResponse
                {
                    Message = result.Success ? "Client library generated successfully" : "Client library generation failed",
                    ResponseError = result.Success ? null : result.ErrorMessage,
                    Duration = result.Duration
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating client library with tsp-client");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Error generating client library: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "validate-typespec-project"), Description("Validate if a path contains a valid TypeSpec project")]
        public DefaultCommandResponse ValidateTypeSpecProject(string? projectPath)
        {
            try
            {
                if (string.IsNullOrEmpty(projectPath))
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = "Project path cannot be null or empty"
                    };
                }

                var isValid = typeSpecHelper.IsValidTypeSpecProjectPath(projectPath);
                var isManagementPlane = false;
                var relativePath = string.Empty;

                if (isValid)
                {
                    try
                    {
                        isManagementPlane = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(projectPath);
                        relativePath = typeSpecHelper.GetTypeSpecProjectRelativePath(projectPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning("Could not determine additional project details: {Error}", ex.Message);
                    }
                }

                return new DefaultCommandResponse
                {
                    Message = isValid ? "Valid TypeSpec project" : "Invalid TypeSpec project",
                    Result = new
                    {
                        IsValid = isValid,
                        IsManagementPlane = isManagementPlane,
                        RelativePath = relativePath,
                        ProjectPath = projectPath
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validating TypeSpec project");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Error validating TypeSpec project: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "tsp-client-full-workflow"), Description("Complete workflow: init, sync, and generate for a TypeSpec project")]
        public async Task<DefaultCommandResponse> FullWorkflow(
            string tspConfigPath,
            string? outputDir = null,
            string? localSpecRepo = null,
            string? commit = null,
            string? repo = null,
            string? emitterOptions = null,
            bool debug = false)
        {
            try
            {
                var response = new DefaultCommandResponse();
                var messages = new List<string>();
                var totalDuration = 0;

                // Step 1: Initialize
                logger.LogInformation("Starting tsp-client full workflow - Step 1: Initialize");
                var initResult = await InitializeProject(tspConfigPath, outputDir, false, localSpecRepo, commit, repo, false, emitterOptions, false, debug);
                if (!string.IsNullOrEmpty(initResult.ResponseError))
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Initialization failed: {initResult.ResponseError}"
                    };
                }
                messages.Add("✓ Project initialized successfully");
                totalDuration += (int)initResult.Duration;

                // Step 2: Sync (if not skipped in init)
                logger.LogInformation("Starting tsp-client full workflow - Step 2: Sync");
                var syncResult = await ExecuteTspClientCommand(new List<string> { "sync" });
                if (!syncResult.Success)
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Sync failed: {syncResult.ErrorMessage}"
                    };
                }
                messages.Add("✓ Project synced successfully");
                totalDuration += syncResult.Duration;

                // Step 3: Generate
                logger.LogInformation("Starting tsp-client full workflow - Step 3: Generate");
                var generateResult = await ExecuteTspClientCommand(new List<string> { "generate" });
                if (!generateResult.Success)
                {
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Generation failed: {generateResult.ErrorMessage}"
                    };
                }
                messages.Add("✓ Client library generated successfully");
                totalDuration += generateResult.Duration;

                return new DefaultCommandResponse
                {
                    Message = "Full workflow completed successfully:\n" + string.Join("\n", messages),
                    Duration = totalDuration,
                    Result = new
                    {
                        Steps = new[] { "init", "sync", "generate" },
                        AllStepsCompleted = true,
                        TotalDuration = totalDuration
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in tsp-client full workflow");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Error in full workflow: {ex.Message}"
                };
            }
        }

        private async Task<DefaultCommandResponse> ExecuteInitCommand(InvocationContext ctx)
        {
            var tspConfig = ctx.ParseResult.GetValueForOption(tspConfigOpt);
            var outputDir = ctx.ParseResult.GetValueForOption(outputDirOpt);
            var skipSyncAndGenerate = ctx.ParseResult.GetValueForOption(skipSyncAndGenerateOpt);
            var localSpecRepo = ctx.ParseResult.GetValueForOption(localSpecRepoOpt);
            var commit = ctx.ParseResult.GetValueForOption(commitOpt);
            var repo = ctx.ParseResult.GetValueForOption(repoOpt);
            var skipInstall = ctx.ParseResult.GetValueForOption(skipInstallOpt);
            var emitterOptions = ctx.ParseResult.GetValueForOption(emitterOptionsOpt);
            var saveInputs = ctx.ParseResult.GetValueForOption(saveInputsOpt);
            var debug = ctx.ParseResult.GetValueForOption(debugOpt);

            return await InitializeProject(tspConfig!, outputDir, skipSyncAndGenerate, localSpecRepo, commit, repo, skipInstall, emitterOptions, saveInputs, debug);
        }

        private async Task<DefaultCommandResponse> ExecuteUpdateCommand(InvocationContext ctx)
        {
            var outputDir = ctx.ParseResult.GetValueForOption(outputDirOpt);
            var repo = ctx.ParseResult.GetValueForOption(repoOpt);
            var commit = ctx.ParseResult.GetValueForOption(commitOpt);
            var tspConfig = ctx.ParseResult.GetValueForOption(tspConfigOpt);
            var localSpecRepo = ctx.ParseResult.GetValueForOption(localSpecRepoOpt);
            var emitterOptions = ctx.ParseResult.GetValueForOption(emitterOptionsOpt);
            var saveInputs = ctx.ParseResult.GetValueForOption(saveInputsOpt);
            var skipInstall = ctx.ParseResult.GetValueForOption(skipInstallOpt);
            var debug = ctx.ParseResult.GetValueForOption(debugOpt);

            return await UpdateProject(outputDir, repo, commit, tspConfig, localSpecRepo, emitterOptions, saveInputs, skipInstall, debug);
        }

        private async Task<DefaultCommandResponse> ExecuteSyncCommand(InvocationContext ctx)
        {
            var outputDir = ctx.ParseResult.GetValueForOption(outputDirOpt);
            var localSpecRepo = ctx.ParseResult.GetValueForOption(localSpecRepoOpt);
            var debug = ctx.ParseResult.GetValueForOption(debugOpt);

            var args = new List<string> { "sync" };
            AddOptionalArg(args, "--output-dir", outputDir);
            AddOptionalArg(args, "--local-spec-repo", localSpecRepo);
            if (debug) args.Add("--debug");

            var result = await ExecuteTspClientCommand(args);
            return new DefaultCommandResponse
            {
                Message = result.Success ? "Project synced successfully" : "Project sync failed",
                ResponseError = result.Success ? null : result.ErrorMessage,
                Duration = result.Duration
            };
        }

        private async Task<DefaultCommandResponse> ExecuteGenerateCommand(InvocationContext ctx)
        {
            var outputDir = ctx.ParseResult.GetValueForOption(outputDirOpt);
            var emitterOptions = ctx.ParseResult.GetValueForOption(emitterOptionsOpt);
            var saveInputs = ctx.ParseResult.GetValueForOption(saveInputsOpt);
            var skipInstall = ctx.ParseResult.GetValueForOption(skipInstallOpt);
            var debug = ctx.ParseResult.GetValueForOption(debugOpt);

            return await GenerateClientLibrary(outputDir, emitterOptions, saveInputs, skipInstall, debug);
        }

        private static void AddOptionalArg(List<string> args, string flag, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                args.Add(flag);
                args.Add(value);
            }
        }

        private async Task<(bool Success, string? ErrorMessage, int Duration)> ExecuteTspClientCommand(List<string> args)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                logger.LogInformation("Executing npx @azure-tools/typespec-client-generator-cli with args: {Args}", string.Join(" ", args));

                // Check if npx and the package are available
                if (!await IsTspClientAvailable())
                {
                    var commandDuration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                    return (false, "npx or @azure-tools/typespec-client-generator-cli is not available. Please ensure Node.js and npm are installed.", commandDuration);
                }

                var arguments = string.Join(" ", args.Select(arg => arg.Contains(" ") ? $"\"{arg}\"" : arg));
                
                string output;
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                if (isWindows)
                {
                    output = RunProcess("cmd.exe", $"/C npx @azure-tools/typespec-client-generator-cli {arguments}", Environment.CurrentDirectory);
                }
                else
                {
                    output = RunProcess("npx", $"@azure-tools/typespec-client-generator-cli {arguments}", Environment.CurrentDirectory);
                }

                var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                var success = !output.Contains("failed") && !output.Contains("error");

                if (!success)
                {
                    SetFailure(1);
                    var errorMessage = $"tsp-client command failed: {output}";
                    
                    // Add helpful context to common errors
                    if (errorMessage.Contains("MODULE_NOT_FOUND") || errorMessage.Contains("command not found"))
                    {
                        errorMessage += "\n\nSuggestion: Make sure Node.js and npm are installed, and @azure-tools/typespec-client-generator-cli is available via npx.";
                    }
                    else if (errorMessage.Contains("tspconfig.yaml"))
                    {
                        errorMessage += "\n\nSuggestion: Ensure the tspconfig.yaml file exists and is properly formatted.";
                    }
                    
                    return (false, errorMessage, duration);
                }

                logger.LogInformation("npx @azure-tools/typespec-client-generator-cli completed successfully");
                return (true, null, duration);
            }
            catch (Exception ex)
            {
                var exceptionDuration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                logger.LogError(ex, "Failed to execute npx @azure-tools/typespec-client-generator-cli command");
                SetFailure(1);
                return (false, ex.Message, exceptionDuration);
            }
        }

        private static string RunProcess(string command, string args, string workingDirectory)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };
            var output = new StringBuilder();
            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                        output.AppendLine(args.Data);
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                        output.AppendLine(args.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit(100000);
                if (process.ExitCode != 0)
                {
                    output.Append($"{Environment.NewLine}tsp-client command failed!!!");
                }
            }
            return output.ToString();
        }

        private async Task<bool> IsTspClientAvailable()
        {
            try
            {
                string output;
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                if (isWindows)
                {
                    output = RunProcess("cmd.exe", "/C npx @azure-tools/typespec-client-generator-cli --version", Environment.CurrentDirectory);
                }
                else
                {
                    output = RunProcess("npx", "@azure-tools/typespec-client-generator-cli --version", Environment.CurrentDirectory);
                }
                
                return !output.Contains("failed") && !output.Contains("error") && !string.IsNullOrEmpty(output.Trim());
            }
            catch
            {
                return false;
            }
        }
    }
}
