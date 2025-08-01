// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CheckAllTool
{
    /// <summary>
    /// This tool runs README validation for SDK projects.
    /// </summary>
    [Description("Run README validation for SDK projects")]
    [McpServerToolType]
    public class ReadmeValidationTool : MCPTool
    {
        private readonly ILogger<ReadmeValidationTool> logger;
        private readonly IOutputService output;
        private readonly IGitHelper gitHelper;

        private readonly Option<string> projectPathOption = new(["--project-path", "-p"], "Path to the project directory to check") { IsRequired = true };
        private readonly Option<string> settingsPathOption = new(["--settings-path", "-s"], "Path to the README validation settings file (.docsettings.yml)");

        public ReadmeValidationTool(ILogger<ReadmeValidationTool> logger, IOutputService output, IGitHelper gitHelper) : base()
        {
            this.logger = logger;
            this.output = output;
            this.gitHelper = gitHelper;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("verifyReadme", "Run README validation for SDK projects");
            command.AddOption(projectPathOption);
            command.AddOption(settingsPathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var projectPath = ctx.ParseResult.GetValueForOption(projectPathOption);
                var settingsPath = ctx.ParseResult.GetValueForOption(settingsPathOption);
                var result = await RunReadmeValidation(projectPath, settingsPath);

                output.Output(result);
                ctx.ExitCode = ExitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running README validation");
                SetFailure(1);
                output.Output(new DefaultCommandResponse
                {
                    ResponseError = $"Error occurred while running README validation: {ex.Message}"
                });
                ctx.ExitCode = ExitCode;
            }
        }

        [McpServerTool(Name = "VerifyReadme"), Description("Run README validation for SDK projects. Provide absolute path to project root as param.")]
        public async Task<DefaultCommandResponse> RunReadmeValidation(string projectPath, string settingsPath = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                logger.LogInformation($"Starting README validation for project at: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Project path does not exist: {projectPath}"
                    };
                }

                // Find the SDK repository root by looking for common repository indicators
                // Start from the project path and work upwards to find the SDK repo root
                var projectRepoRoot = gitHelper.FindRepositoryRoot(projectPath);
                if (string.IsNullOrEmpty(projectRepoRoot))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Could not find repository root from project path: {projectPath}"
                    };
                }

                // Construct the path to the PowerShell script in the SDK repository
                // The script should be in the project's repository root, not relative to this tool's location
                var scriptPath = Path.Combine(projectRepoRoot, "eng", "common", "scripts", "Verify-Readme.ps1");
                
                if (!File.Exists(scriptPath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"PowerShell script not found at expected location: {scriptPath}"
                    };
                }

                // Execute the PowerShell script
                var result = await ExecuteReadmeValidationScript(scriptPath, projectPath, projectRepoRoot, settingsPath);
                stopwatch.Stop();

                if (result.Success)
                {
                    return new DefaultCommandResponse
                    {
                        Message = "README validation completed successfully",
                        Duration = (int)stopwatch.ElapsedMilliseconds,
                        Result = new CheckResult
                        {
                            CheckType = "README Validation",
                            Success = true,
                            Message = "README validation completed successfully",
                            Duration = (int)stopwatch.ElapsedMilliseconds,
                            Details = result.Details
                        }
                    };
                }
                else
                {
                    // Don't set failure status for validation issues - these are results, not errors
                    // Only set failure for actual tool errors (missing files, etc.)
                    var isToolError = result.ErrorMessage.Contains("not found") || 
                                     result.ErrorMessage.Contains("does not exist") ||
                                     result.ErrorMessage.Contains("Could not find") ||
                                     result.ErrorMessage.Contains("Failed to execute");
                    
                    if (isToolError)
                    {
                        SetFailure(1);
                    }
                    
                    return new DefaultCommandResponse
                    {
                        Message = isToolError ? null : "README validation completed with issues",
                        ResponseError = isToolError ? result.ErrorMessage : null,
                        Duration = (int)stopwatch.ElapsedMilliseconds,
                        Result = new CheckResult
                        {
                            CheckType = "README Validation",
                            Success = false,
                            Message = result.ErrorMessage,
                            Duration = (int)stopwatch.ElapsedMilliseconds,
                            Details = result.Details
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running README validation");
                stopwatch.Stop();
                SetFailure(1);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Unhandled exception: {ex.Message}",
                    Duration = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// Executes the PowerShell README validation script
        /// </summary>
        /// <param name="scriptPath">Path to the PowerShell script</param>
        /// <param name="projectPath">Path to the project directory</param>
        /// <param name="repoRoot">Path to the repository root</param>
        /// <param name="settingsPath">Optional path to the settings file</param>
        /// <returns>Validation result</returns>
        private async Task<(bool Success, string ErrorMessage, List<string> Details)> ExecuteReadmeValidationScript(string scriptPath, string projectPath, string repoRoot, string settingsPath = null)
        {
            var details = new List<string>();
            
            try
            {
                // Handle cross-platform execution
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var fileName = isWindows ? "cmd.exe" : "pwsh";
                
                // Use provided settings path or find it automatically
                var finalSettingsPath = settingsPath ?? FindReadmeSettingsPath(repoRoot);
                if (string.IsNullOrEmpty(finalSettingsPath))
                {
                    return (false, "Could not find README validation settings file (.docsettings.yml or similar)", details);
                }

                var arguments = isWindows 
                    ? $"/C pwsh -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -RepoRoot \"{repoRoot}\" -ScanPaths \"{projectPath}\" -SettingsPath \"{finalSettingsPath}\""
                    : $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -RepoRoot \"{repoRoot}\" -ScanPaths \"{projectPath}\" -SettingsPath \"{finalSettingsPath}\"";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = projectPath
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
                    // For README validation failures, the detailed results are in the output stream
                    // The error stream typically contains PowerShell error messages like "LogError"
                    // We want to show the validation results, not just the generic error
                    var errorMessage = "";
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        // If output contains validation results, use that as the primary message
                        errorMessage = output;
                        
                        // If there are also errors, append them
                        if (!string.IsNullOrEmpty(error))
                        {
                            errorMessage += Environment.NewLine + "Additional errors:" + Environment.NewLine + error;
                        }
                    }
                    else if (!string.IsNullOrEmpty(error))
                    {
                        errorMessage = error;
                    }
                    else
                    {
                        errorMessage = $"PowerShell script exited with code {exitCode}";
                    }
                    
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

        /// <summary>
        /// Finds the README validation settings file in the repository
        /// </summary>
        /// <param name="repoRoot">Repository root path</param>
        /// <returns>Path to settings file or null if not found</returns>
        private string FindReadmeSettingsPath(string repoRoot)
        {
            // Common settings file names for README validation
            // Look in eng/scripts first (language repo pattern), then fallback to other locations
            var possibleSettingsFiles = new[]
            {
                "eng/scripts/.docsettings.yml",
                "eng/scripts/.docsettings.yaml",
                ".docsettings.yml",
                ".docsettings.yaml", 
                "eng/docsettings.yml",
                "eng/docsettings.yaml",
                "eng/common/docsettings.yml",
                "eng/common/docsettings.yaml"
            };

            foreach (var settingsFile in possibleSettingsFiles)
            {
                var fullPath = Path.Combine(repoRoot, settingsFile);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }
    }
}