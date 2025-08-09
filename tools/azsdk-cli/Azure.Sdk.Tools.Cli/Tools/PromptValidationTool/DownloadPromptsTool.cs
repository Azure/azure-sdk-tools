// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools
{
    

    [McpServerToolType, Description("Downloads files from any GitHub repository to a local directory, or from provided file data.")]
    public class DownloadPromptsTool(ILogger<DownloadPromptsTool> logger, IOutputService output, IGitHubService gitHubService) : MCPTool
    {
        // Options
        private readonly Option<string> sourceRepoOwnerOpt = new(["--source-repo-owner"], () => "Azure", "Owner of the source repository") { IsRequired = false };
        private readonly Option<string> sourceRepoNameOpt = new(["--source-repo-name"], () => "azure-rest-api-specs", "Name of the source repository") { IsRequired = false };
        private readonly Option<string> sourcePathOpt = new(["--source-path"], () => ".github/prompts", "Path in the source repository to download from") { IsRequired = false };
        private readonly Option<string> destinationPathOpt = new(["--destination-path"], () => ".github/prompts", "Local directory path to download files to") { IsRequired = false };
        private readonly Option<string> missingFilesOpt = new(["--missing-files"], "Comma-delimited list of file paths to download (e.g., 'file1.md,file2.md')") { IsRequired = false };

        public override Command GetCommand()
        {
            Command command = new("download-prompts");
            command.AddOption(sourceRepoOwnerOpt);
            command.AddOption(sourceRepoNameOpt);
            command.AddOption(sourcePathOpt);
            command.AddOption(destinationPathOpt);
            command.AddOption(missingFilesOpt);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var sourceRepoOwner = ctx.ParseResult.GetValueForOption(sourceRepoOwnerOpt);
            var sourceRepoName = ctx.ParseResult.GetValueForOption(sourceRepoNameOpt);
            var sourcePath = ctx.ParseResult.GetValueForOption(sourcePathOpt);
            var destinationPath = ctx.ParseResult.GetValueForOption(destinationPathOpt);
            var missingFilesList = ctx.ParseResult.GetValueForOption(missingFilesOpt);

            var result = await DownloadPrompts(sourceRepoOwner, sourceRepoName, sourcePath, destinationPath, missingFilesList);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "azsdk-download-files-from-github"), Description("Downloads files from a GitHub repository to a local directory, or from provided file paths")]
        public async Task<DownloadResponse> DownloadPrompts(
            string sourceRepoOwner, 
            string sourceRepoName, 
            string sourcePath, 
            string destinationPath,
            string? missingFilesList = null)
        {
            try
            {
                // Check if we are already in the source repository
                var repoRoot = FindGitRepositoryRoot(destinationPath);
                if (string.Equals(Path.GetFileName(repoRoot), sourceRepoName, StringComparison.OrdinalIgnoreCase))
                {
                    return new DownloadResponse
                    {
                        Message = $"We are in source repository no need to download files. {sourceRepoName} = {repoRoot}",
                        Success = true,
                        DownloadedCount = 0,
                        TotalFiles = 0
                    };
                }

                logger.LogInformation("Starting download from {sourceRepoOwner}/{sourceRepoName}/{sourcePath} to {destinationPath}", 
                    sourceRepoOwner, sourceRepoName, sourcePath, destinationPath);

                // Create destination directory if it doesn't exist
                Directory.CreateDirectory(destinationPath);

                // Step 1: Get the list of files to download
                var filesToDownload = await GetFilesToDownloadAsync(sourceRepoOwner, sourceRepoName, sourcePath, missingFilesList);
                if (filesToDownload?.Count == 0)
                {
                    return new DownloadResponse
                    {
                        Message = $"No files found to download from {sourceRepoOwner}/{sourceRepoName}/{sourcePath}",
                        Success = false,
                        DownloadedCount = 0,
                        TotalFiles = 0
                    };
                }

                // Step 2: Download all the files and add to gitignore
                var downloadResponse = await DownloadFilesAsync(sourceRepoOwner, sourceRepoName, destinationPath, filesToDownload);

                // Build response message
                var sourceDescription = !string.IsNullOrEmpty(missingFilesList)
                    ? $"GitHub repository {sourceRepoOwner}/{sourceRepoName} (via provided file paths)"
                    : $"GitHub repository {sourceRepoOwner}/{sourceRepoName}/{sourcePath}";

                var message = $"Downloaded {downloadResponse.DownloadedCount} of {filesToDownload.Count} files from {sourceDescription}";

                downloadResponse.Message = message;
                return downloadResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download prompts");
                SetFailure();
                return new DownloadResponse
                {
                    ResponseError = $"Error downloading prompts: {ex.Message}",
                    Message = $"Failed to download files from {sourceRepoOwner}/{sourceRepoName}",
                    Success = false,
                    DownloadedCount = 0,
                    TotalFiles = 0
                };
            }
        }

        /// <summary>
        /// Gets the list of files to download, either from provided comma-delimited file paths or by listing the GitHub repository directory
        /// </summary>
        private async Task<List<string>> GetFilesToDownloadAsync(
            string sourceRepoOwner, 
            string sourceRepoName, 
            string sourcePath, 
            string? missingFilesList)
        {
            if (!string.IsNullOrEmpty(missingFilesList))
            {
                logger.LogInformation("Using provided missing files list: {filesList}", missingFilesList);
                
                var filePaths = missingFilesList
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(path =>
                    {
                        var trimmedPath = path.Trim();
                            
                        // Use the provided path, or construct it if it's just a filename
                        var fullPath = trimmedPath.Contains('/') || trimmedPath.Contains('\\') 
                            ? trimmedPath.Replace('\\', '/') // Normalize to forward slashes for GitHub
                            : $"{sourcePath}/{trimmedPath}"; // Prepend source path if just filename
                        
                        logger.LogInformation("Will download: {filePath}", fullPath);
                        return fullPath;
                    })
                    .ToList();
                
                logger.LogInformation("Parsed {fileCount} files from provided list", filePaths.Count);
                return filePaths;
            }

            // If no missing files provided, fetch all files from GitHub repository
            logger.LogInformation("No missing files provided, fetching all files from GitHub repository");
            
            var contents = await gitHubService.GetContentsAsync(sourceRepoOwner, sourceRepoName, sourcePath);
            var files = contents?.Where(item => item.Type == ContentType.File).ToList() ?? new List<RepositoryContent>();
            
            if (files.Count == 0)
            {
                logger.LogWarning("No files found in {sourceRepoOwner}/{sourceRepoName}/{sourcePath}", sourceRepoOwner, sourceRepoName, sourcePath);
                return new List<string>();
            }

            var githubFilesToDownload = files.Select(file => $"{sourcePath}/{file.Name}").ToList();

            logger.LogInformation("Found {fileCount} files to download from GitHub directory", githubFilesToDownload.Count);
            return githubFilesToDownload;
        }

        /// <summary>
        /// Downloads all files in the provided list from GitHub
        /// </summary>
        private async Task<DownloadResponse> DownloadFilesAsync(
            string sourceRepoOwner, 
            string sourceRepoName, 
            string destinationPath, 
            List<string> filesToDownload)
        {
            var downloadedFiles = new List<string>();

            logger.LogInformation("Starting download of {fileCount} files", filesToDownload.Count);

            foreach (var filePath in filesToDownload)
            {
                var fileName = Path.GetFileName(filePath);

                var localFilePath = Path.Combine(destinationPath, fileName);
                if (File.Exists(localFilePath)) 
                {
                    logger.LogInformation("File {fileName} already exists in {destinationPath}, skipping download", fileName, destinationPath);
                    continue;
                }
                
                // Fetch the file content from GitHub
                logger.LogInformation("Fetching content for {fileName} from {filePath}", fileName, filePath);
                var contentsResult = await gitHubService.GetContentsAsync(sourceRepoOwner, sourceRepoName, filePath);
                var fileContent = contentsResult?[0]; // since we expect a single file per path

                if (fileContent?.Content == null)
                {
                    var error = $"Failed to fetch content for file {fileName} from {filePath}";
                    logger.LogError(error);
                    throw new InvalidOperationException(error);
                }

                await File.WriteAllTextAsync(localFilePath, fileContent.Content);
                
                downloadedFiles.Add(fileName);
                logger.LogInformation("Downloaded {fileName} from {sourcePath} to {localPath}", fileName, filePath, localFilePath);
            }

            // Add downloaded files to .gitignore
            if (downloadedFiles.Count > 0)
            {
                await AddToGitignoreAsync(destinationPath, downloadedFiles);
            }

            logger.LogInformation("Download completed: {downloadedCount}/{totalCount} files successful", 
                downloadedFiles.Count, filesToDownload.Count);

            return new DownloadResponse
            {
                Success = true,
                DownloadedCount = downloadedFiles.Count,
                TotalFiles = filesToDownload.Count
            };
        }

        /// <summary>
        /// Adds downloaded files to .gitignore to prevent them from being committed
        /// </summary>
        private async Task AddToGitignoreAsync(string destinationPath, List<string> downloadedFiles)
        {
            try
            {
                // Find git repository root by walking up the directory tree
                var repoRoot = FindGitRepositoryRoot(destinationPath);
                if (repoRoot == null)
                {
                    logger.LogWarning("Not in a git repository, skipping .gitignore update");
                    return;
                }

                var gitignorePath = Path.Combine(repoRoot, ".gitignore");
                
                // Read existing .gitignore content
                var existingLines = File.Exists(gitignorePath) 
                    ? await File.ReadAllLinesAsync(gitignorePath) 
                    : Array.Empty<string>();

                var entriesToAdd = new List<string>();
                
                // Add header if not already present
                if (!existingLines.Any(line => line.Contains("Downloaded prompt files for Azure SDK MCP")))
                {
                    entriesToAdd.Add("");
                    entriesToAdd.Add("# Downloaded prompt files for Azure SDK MCP");
                }

                // Add each downloaded file if not already in .gitignore
                foreach (var fileName in downloadedFiles)
                {
                    var relativePath = Path.GetRelativePath(repoRoot, Path.Combine(destinationPath, fileName)).Replace('\\', '/');
                    
                    if (!existingLines.Any(line => line.Trim() == relativePath))
                    {
                        entriesToAdd.Add(relativePath);
                        logger.LogInformation("Adding {relativePath} to .gitignore", relativePath);
                    }
                }

                // Update .gitignore if we have new entries
                if (entriesToAdd.Count > 0)
                {
                    var allLines = existingLines.Concat(entriesToAdd);
                    await File.WriteAllLinesAsync(gitignorePath, allLines);
                    logger.LogInformation("Updated .gitignore with {count} new entries", entriesToAdd.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update .gitignore, but downloads were successful");
            }
        }

        /// <summary>
        /// Finds the git repository root by looking for .git directory
        /// </summary>
        private string? FindGitRepositoryRoot(string startPath)
        {
            var currentDir = new DirectoryInfo(startPath);
            while (currentDir != null)
            {
                if (Directory.Exists(Path.Combine(currentDir.FullName, ".git")))
                {
                    return currentDir.FullName;
                }
                currentDir = currentDir.Parent;
            }
            return null;
        }
    }
}
