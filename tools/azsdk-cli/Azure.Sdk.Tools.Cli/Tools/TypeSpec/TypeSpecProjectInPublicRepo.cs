// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// This tool is used to check if a TypeSpec project is in a public spec repository.
    /// </summary>
    [Description("TypeSpec public repository validation tool")]
    [McpServerToolType]
    public class TypeSpecProjectInPublicRepo(ITypeSpecHelper typeSpecHelper, ILogger<TypeSpecProjectInPublicRepo> logger) : MCPTool
    {
        // Commands
        private const string checkPublicRepoCommandName = "check-public-repo";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new(["--typespec-project"], "Path to typespec project") { IsRequired = true };

        /// <summary>
        /// Checks if a TypeSpec project is in a public spec repository.
        /// </summary>
        /// <param name="typeSpecProjectPath">The path to the TypeSpec project.</param>
        [McpServerTool(Name = "azsdk_check_typespec_project_in_public_repo"), Description("Check if TypeSpec project is in public spec repo. Provide absolute path to TypeSpec project root as param.")]
        public string CheckTypeSpecProjectInPublicRepo(string typeSpecProjectPath)
        {
            try
            {
                var repoRootPath = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);
                var isPublicRepo = typeSpecHelper.IsRepoPathForPublicSpecRepo(repoRootPath);
                return isPublicRepo.ToString();
            }
            catch (Exception ex)
            {
                SetFailure();
                return $"Unexpected failure occurred. Error: {ex.Message}";
            }
        }

        public override Command GetCommand()
        {
            Command command = new Command(checkPublicRepoCommandName, "Check if TypeSpec project is in public spec repo") { typeSpecProjectPathOpt };
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
                case checkPublicRepoCommandName:
                    var typeSpecProjectPath = ctx.ParseResult.GetValueForOption(typeSpecProjectPathOpt);
                    var checkResult = CheckTypeSpecProjectInPublicRepo(typeSpecProjectPath);
                    logger.LogInformation($"Public repo check result: {checkResult}");
                    return 0;
                default:
                    logger.LogError($"Unknown command: {command}");
                    return 1;
            }
        }
    }
}