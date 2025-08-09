// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// This tool is used to validate the TypeSpec specification for Azure SDK service.
    /// </summary>
    [Description("TypeSpec validation tools")]
    [McpServerToolType]
    public class SpecValidationTools(ITypeSpecHelper typeSpecHelper, ILogger<SpecValidationTools> logger) : MCPTool
    {
        // Commands
        private const string typespecValidationCommandName = "validate-typespec";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new(["--typespec-project"], "Path to typespec project") { IsRequired = true };

        /// <summary>
        /// Validates the TypeSpec API specification.
        /// </summary>
        /// <param name="typeSpecProjectRootPath">The root path of the TypeSpec project.</param>
        [McpServerTool(Name = "azsdk-run-typespec-validation"), Description("Run TypeSpec validation. Provide absolute path to TypeSpec project root as param. This tool runs TypeSpec validation and TypeSpec configuration validation.")]
        public IList<string> RunTypeSpecValidation(string typeSpecProjectRootPath)
        {
            try
            {
                logger.LogInformation($"TypeSpec project root path: {typeSpecProjectRootPath}");
                var validationResults = new List<string>();
                if (!typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectRootPath))
                {
                    validationResults.Add($"TypeSpec project is not found in {typeSpecProjectRootPath}. TypeSpec MCP tools can only be used for TypeSpec based spec projects.");
                    return validationResults;
                }

                try
                {
                    var specRepoRootPath = GetGitRepoRootPath(typeSpecProjectRootPath);
                    logger.LogInformation($"Repo root path: {specRepoRootPath}");

                    // Run npm ci only if "node_modules/.bin/tsv" is not present to improve validation performance
                    if (!IsTypeSpecValidationExecutablePresent(specRepoRootPath))
                    {
                        // Run npm ci
                        logger.LogInformation("Running npm ci");
                        RunNpmCi(specRepoRootPath);
                        logger.LogInformation("Completed running npm ci");
                    }

                    //Run TypeSpec validation
                    logger.LogInformation("Running npx tsv to run the validation");
                    ValidateTypeSpec(typeSpecProjectRootPath, specRepoRootPath, validationResults);
                    logger.LogInformation("Completed running TypeSpec validation");
                }
                catch (Exception ex)
                {
                    validationResults.Add($"Error: {ex.Message}");
                }
                return validationResults;
            }
            catch (Exception ex)
            {
                logger.LogError($"Unhandled exception: {ex}");
                SetFailure();
                return new List<string> { $"Unhandled exception: {ex.Message}" };
            }
        }

        private bool IsTypeSpecValidationExecutablePresent(string repoRoot)
        {
            var tsvExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tsv.cmd" : "tsv";
            return File.Exists(Path.Combine(repoRoot, "node_modules", ".bin", tsvExecutable));
        }

        public static void RunNpmCi(string repoRoot)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                _ = RunProcess("cmd.exe", "/C npm ci", repoRoot);
            }
            else
            {
                _ = RunProcess("npm", "ci", repoRoot);
            }
        }

        private static string ValidateTypeSpec(string typeSpecProjectRootPath, string specRepoRootPath, IList<string> validationResults)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                var output = RunProcess("cmd.exe", $"/C npx tsv {typeSpecProjectRootPath}", specRepoRootPath);
                validationResults.Add(output);
            }
            else
            {
                var output = RunProcess("npx", $"tsv {typeSpecProjectRootPath}", specRepoRootPath);
                validationResults.Add(output);
            }
            return "TypeSpec validation completed successfully";
        }


        private static string GetGitRepoRootPath(string typeSpecProjectRootPath)
        {
            var currentDirectory = new DirectoryInfo(typeSpecProjectRootPath);
            while (currentDirectory != null && !currentDirectory.Name.Equals("specification", StringComparison.OrdinalIgnoreCase))
            {
                currentDirectory = currentDirectory.Parent;
            }
            return currentDirectory?.Parent?.FullName ?? string.Empty;
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
                    {
                        output.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit(100000);
                if (process.ExitCode != 0)
                {
                    output.Append($"{Environment.NewLine}TypeSpec validation failed!!!");
                }
            }
            return output.ToString();
        }

        public override Command GetCommand()
        {
            Command command = new Command(typespecValidationCommandName, "Run typespec validation") { typeSpecProjectPathOpt };
            command.SetHandler(async ctx => { ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<int> HandleCommand(System.CommandLine.Invocation.InvocationContext ctx, CancellationToken ct)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;

            switch (command)
            {
                case typespecValidationCommandName:
                    var repoRootPath = ctx.ParseResult.GetValueForOption(typeSpecProjectPathOpt);
                    var validationResults = RunTypeSpecValidation(repoRootPath);
                    logger.LogInformation($"Validation results: [{validationResults}]");
                    return 0;

                default:
                    logger.LogError($"Unknown command: {command}");
                    return 1;
            }
        }
    }
}
