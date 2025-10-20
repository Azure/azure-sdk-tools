// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec
{
    [Description("This type contains tools to run various common tasks in specs repo")]
    [McpServerToolType]
    public class SpecCommonTools(IGitHelper gitHelper, ILogger<SpecCommonTools> logger) : MCPTool
    {
        // Even though it's only one command, creating a command group to keep it consistent and easier to add more tools in the future.
        public override CommandGroup[] CommandHierarchy { get; set; } = [new("spec-tool", "TypeSpec project tools for Azure REST API Specs")];

        // Options
        private readonly Option<string> repoRootOpt = new("--repo-root")
        {
            Description = "Path to azure-rest-api-spec repo root",
            Required = true,
        };

        private readonly Option<string> targetBranchOpt = new("--target-branch")
        {
            Description = "Target branch to compare the changes",
            Required = true,
            DefaultValueFactory = _ => "main",
        };

        static readonly string GET_CHANGED_TYPESPEC_PROJECT_SCRIPT = "eng/scripts/Get-TypeSpec-Folders.ps1";

        // Commands
        private const string getModifiedProjectsCommandName = "get-modified-projects";

        protected override Command GetCommand() =>
            new(getModifiedProjectsCommandName, "Get list of modified TypeSpec projects") { repoRootOpt, targetBranchOpt };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            await Task.CompletedTask;
            var command = parseResult.CommandResult.Command.Name;

            switch (command)
            {
                case getModifiedProjectsCommandName:
                    var repoRootPath = parseResult.GetValue(repoRootOpt);
                    var targetBranch = parseResult.GetValue(targetBranchOpt);
                    var modifiedProjects = GetModifiedTypeSpecProjects(repoRootPath, targetBranch);
                    modifiedProjects.Message = "Modified TypeSpec projects:";
                    return modifiedProjects;

                default:
                    return new() { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        [McpServerTool(Name = "azsdk_get_modified_typespec_projects"), Description("This tool returns list of TypeSpec projects modified in current branch")]
        public ObjectCommandResponse GetModifiedTypeSpecProjects(string repoRootPath, string targetBranch = "main")
        {
            try
            {
                var baseCommitSha = gitHelper.GetMergeBaseCommitSha(repoRootPath, targetBranch);
                if (string.IsNullOrEmpty(baseCommitSha))
                {
                    List<string> _out = [$"Failed to get merge base commit SHA for {repoRootPath}"];
                    return new() { Result = _out };
                }

                var scriptPath = Path.Combine(repoRootPath, GET_CHANGED_TYPESPEC_PROJECT_SCRIPT);
                if (!File.Exists(scriptPath))
                {
                    List<string> _out = [$"[{scriptPath}] path is not present"];
                    return new() { Result = _out };
                }

                logger.LogInformation("Getting changed files in current branch with diff against commit SHA {baseCommitSha}", baseCommitSha);
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "pwsh",
                        Arguments = $"{scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = repoRootPath
                    };
                    using var process = Process.Start(processInfo) ?? throw new Exception($"Failed to start the process: git diff {baseCommitSha}  --name-only");
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        var _err = $"Failed to execute 'pwsh {scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles' to get modified TypeSpec projects. Please make sure PowerShell Core is installed. Error {process.StandardError.ReadToEnd()}";
                        return new() { ExitCode = process.ExitCode, ResponseError = _err };
                    }
                    var stdout = process.StandardOutput.ReadToEnd();
                    var _out = stdout.Split(Environment.NewLine).Where(o => o.StartsWith("specification")).ToList();
                    return new() { Result = _out };
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to get modified typespec projects");
                    var err = $"Failed to execute 'pwsh {scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles' to get modified TypeSpec projects. Please make sure PowerShell Core is installed. Error {ex.Message}";
                    return new() { ResponseError = err };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get modified typespec projects due to unhandled exception");
                return new() { ResponseError = $"Failed to get modified TypeSpec projects due to unhandled exception: {ex.Message}" };
            }
        }
    }
}
