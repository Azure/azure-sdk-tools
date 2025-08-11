// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Configuration;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// This tool runs changelog validation for SDK projects.
    /// </summary>
    [Description("Run changelog validation for SDK projects")]
    [McpServerToolType]
    public class ChangelogValidationTool : MCPTool
    {
        private readonly ILogger<ChangelogValidationTool> logger;
        private readonly IOutputService output;
        private readonly IGitHelper gitHelper;
        private readonly IProcessHelper processHelper;

        private readonly Option<string> packagePathOption = new(["--package-path", "-p"], "Path to the package directory to check") { IsRequired = true };

        public ChangelogValidationTool(ILogger<ChangelogValidationTool> logger, IOutputService output, IGitHelper gitHelper, IProcessHelper processHelper) : base()
        {
            this.logger = logger;
            this.output = output;
            this.gitHelper = gitHelper;
            this.processHelper = processHelper;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("changelog-validation", "Run changelog validation for SDK projects");
            command.AddOption(packagePathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var packagePath = ctx.ParseResult.GetValueForOption(packagePathOption);
            var result = RunChangelogValidation(packagePath);

            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "RunChangelogValidation"), Description("Run changelog validation for SDK packages. Provide absolute path to package root as param.")]
        public CLICheckResponse RunChangelogValidation(string packagePath)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                logger.LogInformation($"Starting changelog validation for package at: {packagePath}");
                
                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                // Find the SDK repository root by looking for common repository indicators
                // Start from the package path and work upwards to find the SDK repo root
                var packageRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
                if (string.IsNullOrEmpty(packageRepoRoot))
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", $"Could not find repository root from package path: {packagePath}");
                }

                // Construct the path to the PowerShell script in the SDK repository
                // The script should be in the package's repository root, not relative to this tool's location
                var scriptPath = Path.Combine(packageRepoRoot, Constants.ENG_COMMON_SCRIPTS_PATH, "Verify-ChangeLog.ps1");
                
                if (!File.Exists(scriptPath))
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", $"PowerShell script not found at expected location: {scriptPath}");
                }

                // Execute the PowerShell script using ProcessHelper
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var command = isWindows ? "cmd.exe" : "pwsh";
                var args = isWindows 
                    ? new[] { "/C", "pwsh", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-PackageName", Path.GetFileName(packagePath) }
                    : new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-PackageName", Path.GetFileName(packagePath) };

                var processResult = processHelper.RunProcess(command, args, packagePath);
                stopwatch.Stop();

                if (processResult.ExitCode == 0)
                {
                    return new SuccessCLICheckResponse(0, System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Message = "Changelog validation completed successfully",
                        Duration = (int)stopwatch.ElapsedMilliseconds,
                        Output = processResult.Output
                    }));
                }
                else
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, processResult.Output, $"Changelog validation failed with exit code {processResult.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running changelog validation");
                stopwatch.Stop();
                SetFailure(1);
                return new FailureCLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
            }
        }
    }
}