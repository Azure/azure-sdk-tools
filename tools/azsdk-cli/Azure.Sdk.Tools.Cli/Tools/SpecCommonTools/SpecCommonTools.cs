// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("This type contains tools to run various common tasks in specs repo")]
    [McpServerToolType]
    public class SpecCommonTools(IGitHelper gitHelper, ILogger<SpecCommonTools> logger, IOutputHelper output) : MCPTool
    {

        static readonly string GET_CHANGED_TYPESPEC_PROJECT_SCRIPT = "eng/scripts/Get-TypeSpec-Folders.ps1";

        // Commands
        private const string getModifiedProjectsCommandName = "get-modified-projects";

        // Options
        private readonly Option<string> repoRootOpt = new(["--repo-root"], "Path to azure-rest-api-spec repo root") { IsRequired = true };
        private readonly Option<string> targetBranchOpt = new(["--target-branch"], () => "main", "Target branch to compare the changes") { IsRequired = true };

        [McpServerTool(Name = "azsdk_get_modified_typespec_projects"), Description("This tool returns list of TypeSpec projects modified in current branch")]
        public string GetModifiedTypeSpecProjects(string repoRootPath, string targetBranch = "main")
        {
            try
            {
                var baseCommitSha = gitHelper.GetMergeBaseCommitSha(repoRootPath, targetBranch);
                if (string.IsNullOrEmpty(baseCommitSha))
                {
                    List<string> _out = [$"Failed to get merge base commit SHA for {repoRootPath}"];
                    return output.Format(_out);
                }

                var scriptPath = Path.Combine(repoRootPath, GET_CHANGED_TYPESPEC_PROJECT_SCRIPT);
                if (!File.Exists(scriptPath))
                {
                    List<string> _out = [$"[{scriptPath}] path is not present"];
                    return output.Format(_out);
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
                        SetFailure(process.ExitCode);
                        List<string> _err = [$"Failed to execute 'pwsh {scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles' to get modified TypeSpec projects. Please make sure PowerShell Core is installed. Error {process.StandardError.ReadToEnd()}"];
                        return output.Format(_err);
                    }
                    var stdout = process.StandardOutput.ReadToEnd();
                    var _out = stdout.Split(Environment.NewLine).Where(o => o.StartsWith("specification")).ToList();
                    return output.Format(_out);
                }
                catch (Exception ex)
                {
                    SetFailure();
                    return $"Failed to execute 'pwsh {scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles' to get modified TypeSpec projects. Please make sure PowerShell Core is installed. Error {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                SetFailure();
                return $"Failed to get modified TypeSpec projects due to unhandled exception: {ex.Message}";
            }
        }

        public override Command GetCommand()
        {
            // Even though it's only one command, creating a command group to keep it consistent and easier to add more tools in the future.
            Command command = new("spec-tool", "TypeSpec project tools for Azure REST API Specs");
            var getModifiedProjectsCommand = new Command(getModifiedProjectsCommandName, "Get list of modified typespec projects") { repoRootOpt, targetBranchOpt };
            getModifiedProjectsCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            command.AddCommand(getModifiedProjectsCommand);
            return command;
        }

        public override async Task HandleCommand(System.CommandLine.Invocation.InvocationContext ctx, CancellationToken ct)
        {
            await Task.CompletedTask;
            var command = ctx.ParseResult.CommandResult.Command.Name;

            switch (command)
            {
                case getModifiedProjectsCommandName:
                    var repoRootPath = ctx.ParseResult.GetValueForOption(repoRootOpt);
                    var targetBranch = ctx.ParseResult.GetValueForOption(targetBranchOpt);
                    var modifiedProjects = GetModifiedTypeSpecProjects(repoRootPath, targetBranch);
                    output.Output($"Modified typespec projects: [{modifiedProjects}]");
                    return;

                default:
                    SetFailure();
                    logger.LogError("Unknown command: {command}", command);
                    return;
            }
        }
    }
}
