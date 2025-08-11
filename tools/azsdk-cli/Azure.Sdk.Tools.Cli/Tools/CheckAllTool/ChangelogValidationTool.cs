// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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

        private readonly Option<string> packagePathOption = new(["--package-path", "-p"], "Path to the package directory to check") { IsRequired = true };

        public ChangelogValidationTool(ILogger<ChangelogValidationTool> logger, IOutputService output, IGitHelper gitHelper) : base()
        {
            this.logger = logger;
            this.output = output;
            this.gitHelper = gitHelper;
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
            var result = await RunChangelogValidation(packagePath);

            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "RunChangelogValidation"), Description("Run changelog validation for SDK packages. Provide absolute path to package root as param.")]
        public async Task<CLICheckResponse> RunChangelogValidation(string packagePath)
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

                // Execute the PowerShell script
                var result = await ExecuteChangelogValidationScript(scriptPath, packagePath);
                stopwatch.Stop();

                if (result.Success)
                {
                    return new SuccessCLICheckResponse(0, System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Message = "Changelog validation completed successfully",
                        Duration = (int)stopwatch.ElapsedMilliseconds
                    }));
                }
                else
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", result.ErrorMessage);
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

        /// <summary>
        /// Executes the PowerShell changelog validation script
        /// </summary>
        /// <param name="scriptPath">Path to the PowerShell script</param>
        /// <param name="packagePath">Path to the package directory</param>
        /// <returns>Validation result</returns>
        private async Task<(bool Success, string ErrorMessage, List<string> Details)> ExecuteChangelogValidationScript(string scriptPath, string packagePath)
        {
            var details = new List<string>();
            
            try
            {
                // Handle cross-platform execution
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var fileName = isWindows ? "cmd.exe" : "pwsh";
                var arguments = isWindows 
                    ? $"/C pwsh -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -PackageName \"{Path.GetFileName(packagePath)}\""
                    : $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -PackageName \"{Path.GetFileName(packagePath)}\"";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = packagePath
                };

                using var process = new System.Diagnostics.Process();
                process.StartInfo = processStartInfo;

                logger.LogDebug($"Executing PowerShell command: {processStartInfo.FileName} {processStartInfo.Arguments}");

                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        details.Add($"Output: {e.Data}");
                        logger.LogDebug($"PowerShell Output: {e.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        details.Add($"Error: {e.Data}");
                        logger.LogWarning($"PowerShell Error: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                var exitCode = process.ExitCode;
                var output = outputBuilder.ToString().Trim();
                var error = errorBuilder.ToString().Trim();

                if (exitCode == 0)
                {
                    return (true, string.Empty, details);
                }
                else
                {
                    var errorMessage = !string.IsNullOrEmpty(error) ? error : 
                                     !string.IsNullOrEmpty(output) ? output : 
                                     $"PowerShell script exited with code {exitCode}";
                    return (false, errorMessage, details);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute PowerShell script");
                details.Add($"Exception: {ex.Message}");
                return (false, $"Failed to execute PowerShell script: {ex.Message}", details);
            }
        }
    }
}