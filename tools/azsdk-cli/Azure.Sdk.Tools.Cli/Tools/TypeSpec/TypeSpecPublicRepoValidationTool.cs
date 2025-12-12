// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec
{
    /// <summary>
    /// This tool is used to check if a TypeSpec project is in a public spec repository.
    /// </summary>
    [Description("TypeSpec public repository validation tool")]
    [McpServerToolType]
    public class TypeSpecPublicRepoValidationTool(ITypeSpecHelper typeSpecHelper, ILogger<TypeSpecPublicRepoValidationTool> logger) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec];

        // Commands
        private const string checkPublicRepoCommandName = "check-public-repo";
        private const string CheckProjectInPublicRepoToolName = "azsdk_typespec_check_project_in_public_repo";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new("--typespec-project")
        {
            Description = "Path to typespec project",
            Required = true,
        };

        protected override Command GetCommand() =>
            new McpCommand(checkPublicRepoCommandName, "Check if TypeSpec project is in public spec repo", CheckProjectInPublicRepoToolName) { typeSpecProjectPathOpt };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            await Task.CompletedTask;
            var command = parseResult.CommandResult.Command.Name;

            switch (command)
            {
                case checkPublicRepoCommandName:
                    var typeSpecProjectPath = parseResult.GetValue(typeSpecProjectPathOpt);
                    var checkResult = CheckTypeSpecProjectInPublicRepo(typeSpecProjectPath);
                    checkResult.Message = "Public repo check result:";
                    return checkResult;

                default:
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        /// <summary>
        /// Checks if a TypeSpec project is in a public spec repository.
        /// </summary>
        /// <param name="typeSpecProjectPath">The path to the TypeSpec project.</param>
        [McpServerTool(Name = CheckProjectInPublicRepoToolName), Description("Check if TypeSpec project is in public spec repo. Provide absolute path to TypeSpec project root as param.")]
        public DefaultCommandResponse CheckTypeSpecProjectInPublicRepo(string typeSpecProjectPath)
        {
            try
            {
                var repoRootPath = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);
                var isPublicRepo = typeSpecHelper.IsRepoPathForPublicSpecRepo(repoRootPath);
                return new() { Result = isPublicRepo };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected failure occurred");
                return new DefaultCommandResponse
                {
                    ResponseError = $"Unexpected failure occurred. Error: {ex.Message}"
                };
            }
        }
    }
}
