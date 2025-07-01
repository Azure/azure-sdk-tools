// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools.PromptValidationTool
{
    [McpServerToolType, Description("Validates whether the current workspace has all the prompts from the azure-rest-api-specs repository.")]
    public class PromptValidationTool(ILogger<PromptValidationTool> logger, IOutputService output, IGitHubService gitHubService) : MCPTool
    {
        // Options
        private readonly Option<string> sourceRepoOwnerOpt = new(["--source-repo-owner"], () => "Azure", "Owner of the source repository") { IsRequired = false };
        private readonly Option<string> sourceRepoNameOpt = new(["--source-repo-name"], () => "azure-rest-api-specs", "Name of the source repository") { IsRequired = false };
        private readonly Option<string> sourcePromptsPathOpt = new(["--source-prompts-path"], () => ".github/prompts", "Path in the source repository to compare against") { IsRequired = false };
        private readonly Option<string> localPromptsPathOpt = new(["--local-prompts-path"], () => ".github/prompts", "Local directory path to validate") { IsRequired = false };

        public override Command GetCommand()
        {
            Command command = new("validate-workspace-prompts");
            command.AddOption(sourceRepoOwnerOpt);
            command.AddOption(sourceRepoNameOpt);
            command.AddOption(sourcePromptsPathOpt);
            command.AddOption(localPromptsPathOpt);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var sourceRepoOwner = ctx.ParseResult.GetValueForOption(sourceRepoOwnerOpt);
            var sourceRepoName = ctx.ParseResult.GetValueForOption(sourceRepoNameOpt);
            var sourcePromptsPath = ctx.ParseResult.GetValueForOption(sourcePromptsPathOpt);
            var localPromptsPath = ctx.ParseResult.GetValueForOption(localPromptsPathOpt);
            
            var result = await ValidateWorkspacePrompts(sourceRepoOwner, sourceRepoName, sourcePromptsPath, localPromptsPath);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool, Description("Validates whether the current workspace has all the prompts from a source repository (defaults to Azure/azure-rest-api-specs)")]
        public async Task<ValidationResponse> ValidateWorkspacePrompts(
            string sourceRepoOwner,
            string sourceRepoName, 
            string sourcePromptsPath,
            string localPromptsPath
        )
        {
            try
            {
                logger.LogInformation("Starting validation of workspace prompts against {sourceRepoOwner}/{sourceRepoName}/{sourcePromptsPath}", 
                    sourceRepoOwner, sourceRepoName, sourcePromptsPath);

                // Get prompts from source repository
                var sourcePrompts = await gitHubService.GetContentsAsync(sourceRepoOwner, sourceRepoName, sourcePromptsPath);
                
                // Get source files
                var sourceFiles = sourcePrompts?.Where(item => item.Type == ContentType.File).ToList() ?? new List<RepositoryContent>();
                
                // Get local prompts from workspace
                var workingDir = Directory.GetCurrentDirectory();
                var localPromptsDir = Path.Combine(workingDir, localPromptsPath);
                
                var localFiles = new List<string>();
                if (Directory.Exists(localPromptsDir))
                {
                    localFiles = Directory.GetFiles(localPromptsDir)
                        .Select(f => Path.GetFileName(f))
                        .ToList();
                }
                else
                {
                    logger.LogWarning("Local prompts directory does not exist: {localPromptsDir}", localPromptsDir);
                }

                // Find missing prompts
                var sourceFileNames = sourceFiles.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var localFileNames = localFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missingPrompts = sourceFileNames.Except(localFileNames).ToList();

                var message = missingPrompts.Any() 
                    ? $"Missing {missingPrompts.Count} prompt files from {sourceRepoOwner}/{sourceRepoName}: {string.Join(", ", missingPrompts)}"
                    : $"All {sourceFiles.Count} prompts from {sourceRepoOwner}/{sourceRepoName} are present in local workspace";

                return new ValidationResponse
                {
                    Message = message,
                    IsValid = !missingPrompts.Any(),
                    MissingCount = missingPrompts.Count,
                    TotalSourceFiles = sourceFiles.Count,
                    MissingFiles = missingPrompts.Any() ? missingPrompts : null
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to validate workspace prompts");
                SetFailure();
                return new ValidationResponse
                {
                    ResponseError = $"Error: {ex.Message}",
                    Message = "Failed to validate workspace prompts",
                    IsValid = false,
                    MissingCount = 0,
                    TotalSourceFiles = 0
                };
            }
        }
    }
}
