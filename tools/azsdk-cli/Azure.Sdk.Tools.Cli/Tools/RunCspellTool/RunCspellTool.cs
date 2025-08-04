// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [McpServerToolType, Description("Run cspell to check for typos in code")]
    public class RunCspellTool : MCPTool
    {
        private readonly ILogger<RunCspellTool> logger;
        private readonly IOutputService output;

        private Argument<string> _packagePathArg = new Argument<string>(
            name: "packagePath",
            description: "The path to the package to check for spelling"
        )
        {
            Arity = ArgumentArity.ExactlyOne
        };

        public RunCspellTool(ILogger<RunCspellTool> logger, IOutputService output) : base()
        {
            this.logger = logger;
            this.output = output;
            
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("cspell", "Run cspell to check for typos in the specified package");
            command.AddArgument(_packagePathArg);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            string packagePath = ctx.ParseResult.GetValueForArgument(_packagePathArg);
            var result = await RunCspellCheck(packagePath);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "run-cspell"), Description("Run cspell to check for typos in the specified package path")]
        public async Task<DefaultCommandResponse> RunCspellCheck(string packagePath)
        {
            try
            {
                logger.LogInformation("Running cspell check for package path: {packagePath}", packagePath);

                if (string.IsNullOrWhiteSpace(packagePath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = "Package path is required"
                    };
                }

                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Package path does not exist: {packagePath}"
                    };
                }

                // Get the absolute path to the Invoke-Cspell.ps1 script
                var scriptPath = GetInvokeCspellScriptPath();
                if (string.IsNullOrEmpty(scriptPath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = "Could not locate Invoke-Cspell.ps1 script"
                    };
                }

                var startTime = DateTime.UtcNow;
                var output = await RunPowerShellScript(scriptPath, packagePath);
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                // Parse the cspell output to determine success/failure and extract typos
                var result = ParseCspellOutput(output);

                return new DefaultCommandResponse
                {
                    Message = result.Success ? "Cspell check completed successfully" : "Cspell check found spelling errors",
                    Result = new
                    {
                        Success = result.Success,
                        TyposFound = result.TyposFound,
                        Output = result.Output,
                        Guidance = GetGuidanceMessage(result)
                    },
                    Duration = (long)duration
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running cspell check: {packagePath}", packagePath);
                SetFailure(1);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Error occurred while running cspell check: {ex.Message}"
                };
            }
        }

        private string GetInvokeCspellScriptPath()
        {
            // Try to find the script relative to the current working directory
            var possiblePaths = new[]
            {
                "eng/common/spelling/Invoke-Cspell.ps1",
                "../eng/common/spelling/Invoke-Cspell.ps1",
                "../../eng/common/spelling/Invoke-Cspell.ps1",
                "../../../eng/common/spelling/Invoke-Cspell.ps1",
                "../../../../eng/common/spelling/Invoke-Cspell.ps1"
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return string.Empty;
        }

        private async Task<string> RunPowerShellScript(string scriptPath, string packagePath)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var powerShellExecutable = isWindows ? "powershell.exe" : "pwsh";
            
            // First need to load the logging functions, then run the cspell script
            var loggingScriptPath = GetLoggingScriptPath();
            var command = string.IsNullOrEmpty(loggingScriptPath) 
                ? $"-File \"{scriptPath}\" -ScanGlobs \"{packagePath}/**\" -SpellCheckRoot \"{packagePath}\""
                : $"-Command \". '{loggingScriptPath}'; & '{scriptPath}' -ScanGlobs '{packagePath}/**' -SpellCheckRoot '{packagePath}'\"";

            logger.LogInformation("Executing: {executable} {args}", powerShellExecutable, command);

            var processInfo = new ProcessStartInfo
            {
                FileName = powerShellExecutable,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            var outputBuilder = new StringBuilder();
            using var process = new Process();
            process.StartInfo = processInfo;
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await process.WaitForExitAsync();

            return outputBuilder.ToString();
        }

        private string GetLoggingScriptPath()
        {
            var possiblePaths = new[]
            {
                "eng/common/scripts/logging.ps1",
                "../eng/common/scripts/logging.ps1",
                "../../eng/common/scripts/logging.ps1",
                "../../../eng/common/scripts/logging.ps1",
                "../../../../eng/common/scripts/logging.ps1"
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return string.Empty;
        }

        private CspellResult ParseCspellOutput(string output)
        {
            var typos = new List<string>();
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Look for cspell error patterns
            foreach (var line in lines)
            {
                // cspell typically outputs errors in format like: "filename:line:col - Unknown word (typo)"
                if (line.Contains("- Unknown word") && !line.Contains("Issues found: 0"))
                {
                    typos.Add(line.Trim());
                }
            }

            // Check for success indicator from cspell
            var hasSuccessMessage = output.Contains("Issues found: 0 in 0 files");
            var hasErrorMessages = typos.Count > 0;

            var success = hasSuccessMessage && !hasErrorMessages;

            return new CspellResult
            {
                Success = success,
                TyposFound = typos,
                Output = output
            };
        }

        private string GetGuidanceMessage(CspellResult result)
        {
            if (result.Success)
            {
                return "No spelling errors found. Your code looks good!";
            }

            var guidance = new StringBuilder();
            guidance.AppendLine("Spelling errors were found. Please review the following:");
            guidance.AppendLine();
            
            if (result.TyposFound.Any())
            {
                guidance.AppendLine("Typos found:");
                foreach (var typo in result.TyposFound.Take(10)) // Limit to first 10 typos
                {
                    guidance.AppendLine($"  - {typo}");
                }
                
                if (result.TyposFound.Count > 10)
                {
                    guidance.AppendLine($"  ... and {result.TyposFound.Count - 10} more typos");
                }
            }

            guidance.AppendLine();
            guidance.AppendLine("Next steps:");
            guidance.AppendLine("1. Review each typo to determine if it's a genuine spelling error");
            guidance.AppendLine("2. For legitimate words (technical terms, proper nouns), add them to the cspell dictionary");
            guidance.AppendLine("3. For actual typos, fix the spelling in your code");
            guidance.AppendLine("4. Consider using FixCspell tool to help address these issues");

            return guidance.ToString();
        }

        private class CspellResult
        {
            public bool Success { get; set; }
            public List<string> TyposFound { get; set; } = new();
            public string Output { get; set; } = string.Empty;
        }
    }
}