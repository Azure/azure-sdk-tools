// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec
{
    /// <summary>
    /// This tool is used to validate the TypeSpec specification for Azure SDK service.
    /// </summary>
    [Description("TypeSpec validation tools")]
    [McpServerToolType]
    public class SpecValidationTools(ITypeSpecHelper typeSpecHelper, ILogger<SpecValidationTools> logger) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec];

        // Commands
        private const string TypespecValidationCommandName = "validate";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new("--typespec-project")
        {
            Description = "Path to typespec project",
            Required = true,
        };

        protected override Command GetCommand() =>
            new(TypespecValidationCommandName, "Run typespec validation") { typeSpecProjectPathOpt };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            await Task.CompletedTask;
            var command = parseResult.CommandResult.Command.Name;

            switch (command)
            {
                case TypespecValidationCommandName:
                    var repoRootPath = parseResult.GetValue(typeSpecProjectPathOpt);
                    var validationResults = RunTypeSpecValidation(repoRootPath);
                    validationResults.Message = "Validation results:";
                    return validationResults;

                default:
                    logger.LogError("Unknown command: {command}", command);
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        /// <summary>
        /// Validates the TypeSpec API specification.
        /// </summary>
        /// <param name="typeSpecProjectRootPath">The root path of the TypeSpec project.</param>
        [McpServerTool(Name = "azsdk_run_typespec_validation"), Description("Run TypeSpec validation. Provide absolute path to TypeSpec project root as param. This tool runs TypeSpec validation and TypeSpec configuration validation.")]
        public TypeSpecValidationResponse RunTypeSpecValidation(string typeSpecProjectRootPath)
        {
            try
            {
                var response = new TypeSpecValidationResponse()
                {
                    TypeSpecProject = typeSpecProjectRootPath
                };
                logger.LogInformation("TypeSpec project root path: {typeSpecProjectRootPath}", typeSpecProjectRootPath);
                if (!typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectRootPath))
                {
                    response.ResponseError = $"TypeSpec project is not found in {typeSpecProjectRootPath}. TypeSpec MCP tools can only be used for TypeSpec based spec projects.";
                    return response;
                }

                response.TypeSpecProject = typeSpecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectRootPath);
                response.PackageType = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRootPath) ? SdkType.Management : SdkType.Dataplane;

                try
                {
                    var specRepoRootPath = GetGitRepoRootPath(typeSpecProjectRootPath);
                    logger.LogInformation("Repo root path: {specRepoRootPath}", specRepoRootPath);

                    // Run npm ci only if "node_modules/.bin/tsv" is not present to improve validation performance
                    if (!IsTypeSpecValidationExecutablePresent(specRepoRootPath))
                    {
                        // Run npm ci
                        logger.LogInformation("Installing dependencies with npm ci");
                        RunNpmCi(specRepoRootPath);
                        logger.LogInformation("Dependency installation completed");
                    }

                    //Run TypeSpec validation
                    logger.LogInformation("Starting TypeSpec validation");
                    ValidateTypeSpec(typeSpecProjectRootPath, specRepoRootPath, response.validationResults);
                    logger.LogInformation("TypeSpec validation completed");
                }
                catch (Exception ex)
                {
                    response.ResponseError = $"Error: {ex.Message}";
                }
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in TypeSpec validation");
                return new()
                {
                    ResponseError = $"Unhandled exception: {ex.Message}",
                    TypeSpecProject = typeSpecProjectRootPath
                };
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
    }
}
