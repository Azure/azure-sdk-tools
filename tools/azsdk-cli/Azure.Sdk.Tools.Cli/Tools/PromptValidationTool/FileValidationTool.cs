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

namespace Azure.Sdk.Tools.Cli.Tools
{
    [McpServerToolType, Description("Compares files in a local directory against files in a GitHub repository to identify missing files.")]
    public class FileValidationTool(ILogger<FileValidationTool> logger, IOutputService output, IGitHubService gitHubService) : MCPTool
    {
        // Options
        private readonly Option<string> sourceRepoOwnerOpt = new(["--source-repo-owner"], () => "Azure", "Owner of the source repository") { IsRequired = false };
        private readonly Option<string> sourceRepoNameOpt = new(["--source-repo-name"], () => "azure-rest-api-specs", "Name of the source repository") { IsRequired = false };
        private readonly Option<string> sourceFilesPathOpt = new(["--source-files-path"], () => ".github/prompts", "Path in the source repository to compare against") { IsRequired = false };
        private readonly Option<string> localFilesPathOpt = new(["--local-files-path"], () => ".github/prompts", "Local directory path to validate") { IsRequired = false };
    
        public override Command GetCommand()
        {
            Command command = new("validate-workspace-files");
            command.AddOption(sourceRepoOwnerOpt);
            command.AddOption(sourceRepoNameOpt);
            command.AddOption(sourceFilesPathOpt);
            command.AddOption(localFilesPathOpt);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var sourceRepoOwner = ctx.ParseResult.GetValueForOption(sourceRepoOwnerOpt);
            var sourceRepoName = ctx.ParseResult.GetValueForOption(sourceRepoNameOpt);
            var sourceFilesPath = ctx.ParseResult.GetValueForOption(sourceFilesPathOpt);
            var localFilesPath = ctx.ParseResult.GetValueForOption(localFilesPathOpt);
            
            var result = await ValidateWorkspaceFiles(sourceRepoOwner, sourceRepoName, sourceFilesPath, localFilesPath);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "azsdk-validate-files"), Description("Validates whether the current workspace has all the files from a source repository (defaults to Azure/azure-rest-api-specs)")]
        public async Task<ValidationResponse> ValidateWorkspaceFiles(
            string sourceRepoOwner,
            string sourceRepoName, 
            string sourceFilesPath,
            string localFilesPath
        )
        {
            try
            {
                logger.LogInformation("Starting validation of workspace files against {sourceRepoOwner}/{sourceRepoName}/{sourceFilesPath}", 
                    sourceRepoOwner, sourceRepoName, sourceFilesPath);

                // Get files from source repository
                var sourceContents = await gitHubService.GetContentsAsync(sourceRepoOwner, sourceRepoName, sourceFilesPath);

                // Filter for files only
                var sourceFiles = sourceContents?.Where(item => item.Type == ContentType.File).ToList() ?? new List<RepositoryContent>();

                // Get local files from workspace
                var workingDir = Directory.GetCurrentDirectory();
                var localFilesDir = Path.Combine(workingDir, localFilesPath);

                var localFiles = new List<string>();
                if (Directory.Exists(localFilesDir))
                {
                    localFiles = Directory.GetFiles(localFilesDir)
                        .Select(f => Path.GetFileName(f))
                        .ToList();
                }
                else
                {
                    logger.LogWarning("Local files directory does not exist: {localFilesDir}", localFilesDir);
                }

                // Find missing files
                var sourceFileNames = sourceFiles.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var localFileNames = localFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missingFiles = sourceFileNames.Except(localFileNames).ToList();

                var message = missingFiles.Any()
                    ? $"Missing {missingFiles.Count} files from {sourceRepoOwner}/{sourceRepoName}: {string.Join(", ", missingFiles)}"
                    : $"All {sourceFiles.Count} files from {sourceRepoOwner}/{sourceRepoName} are present in local workspace";

                return new ValidationResponse
                {
                    Message = message,
                    IsValid = !missingFiles.Any(),
                    MissingCount = missingFiles.Count,
                    TotalSourceFiles = sourceFiles.Count,
                    MissingFiles = missingFiles.Any() ? missingFiles : null
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to validate workspace files");
                SetFailure();
                return new ValidationResponse
                {
                    ResponseError = $"Error: {ex.Message}",
                    Message = "Failed to validate workspace files",
                    IsValid = false,
                    MissingCount = 0,
                    TotalSourceFiles = 0
                };
            }
        }
    }
}