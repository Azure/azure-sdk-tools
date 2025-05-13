// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;

namespace AzureSDKDSpecTools.Tools
{
    [Description("This type contains tools to run various common tasks in specs repo")]
    [McpServerToolType]
    public class SpecCommonTools(IGitHelper _gitHelper, ILogger<SpecCommonTools> _logger): MCPTool
    {
        private IGitHelper gitHelper = _gitHelper;
        private ILogger<SpecCommonTools> logger = _logger;

        static readonly string GET_CHANGED_TYPESPEC_PROJECT_SCRIPT = "eng/scripts/Get-TypeSpec-Folders.ps1";

        // Commands
        private const string getModifiedProjectsCommandName = "get-modified-projects";

        // Options
        private readonly Option<string> repoRootOpt = new(["-repo-root"], "Path to azure-rest-api-spec repo root") { IsRequired = true };
        private readonly Option<string> targetBranchOpt = new(["--target-branch"], () => "main", "Target branch to compare the changes") { IsRequired = true };

        [McpServerTool, Description("This tool returns list of TypeSpec projects modified in current branch")]
        public List<string> GetModifiedTypeSpecProjects(string repoRootPath, string targetBranch = "main")
        {
            var baseCommitSha = gitHelper.GetMergeBaseCommitSha(repoRootPath, targetBranch);
            if (string.IsNullOrEmpty(baseCommitSha))
            {
                return [$"Failed to get merge base commit SHA for {repoRootPath}"];
            }

            var scriptPath = Path.Combine(repoRootPath, GET_CHANGED_TYPESPEC_PROJECT_SCRIPT);
            if (!File.Exists(scriptPath))
            {
                return [$"[{scriptPath}] path is not present"];
            }

            logger.LogInformation($"Getting changed files in current branch with diff against commit SHA {baseCommitSha}");
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
                    return [$"Failed to execute 'pwsh {scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles' to get modified TypeSpec projects. Please make sure you have PowerShell core is installed. Error {process.StandardError.ReadToEnd()}"];
                }
                var output = process.StandardOutput.ReadToEnd();
                return output.Split(Environment.NewLine).Where(o => o.StartsWith("specification")).ToList();
            }
            catch (Exception ex)
            {
                return [$"Failed to execute 'pwsh {scriptPath}  -BaseCommitish {baseCommitSha} -IgnoreCoreFiles' to get modified TypeSpec projects. Please make sure you have PowerShell core is installed. Error {ex.Message}"];
            }            
        }

        public override Command GetCommand()
        {
            // Even though it's only one command, creating a command group to keep it consistent and easier to add more tools in the future.
            Command command = new Command("spec-tool");
            var getModifiedProjectsCommand = new Command(getModifiedProjectsCommandName, "Get list of modified typespec projects") { repoRootOpt, targetBranchOpt };
            getModifiedProjectsCommand.SetHandler(async ctx => { ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken()); });
            command.AddCommand(getModifiedProjectsCommand);
            return command;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<int> HandleCommand(System.CommandLine.Invocation.InvocationContext ctx, CancellationToken ct)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;

            switch (command)
            {
                case getModifiedProjectsCommandName:
                    var repoRootPath = ctx.ParseResult.GetValueForOption(repoRootOpt);
                    var targetBranch = ctx.ParseResult.GetValueForOption(targetBranchOpt);
                    var modifiedProjects = GetModifiedTypeSpecProjects(repoRootPath, targetBranch);
                    logger.LogInformation($"Modified typespec projects: [{modifiedProjects}]");
                    return 0;

                default:
                    logger.LogError($"Unknown command: {command}");
                    return 1;
            }
        }
    }
}
