// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

if (Args.Parse(args) is not Args argsData) return 1;

var success = true;

// Connect to Azure App Configuration
Console.WriteLine("Connecting to Azure App Configuration...");
var credential = new DefaultAzureCredential();
var configBuilder = new ConfigurationBuilder();
configBuilder.AddAzureAppConfiguration(options =>
{
    options.Connect(new Uri(argsData.AppConfigEndpoint!), credential);
});
var configuration = configBuilder.Build();

// Helper function to get config value with repository override or default
string? GetConfigValue(string repository, string key)
{
    // Try repository-specific key first (e.g., "Azure/azure-sdk-for-net:IssueModelForCategoryLabels")
    var repoKey = $"{repository}:{key}";
    var value = configuration[repoKey];
    if (!string.IsNullOrEmpty(value))
    {
        Console.WriteLine($"Using repository-specific config: {repoKey}");
        return value;
    }

    // Fall back to defaults (e.g., "defaults:IssueModelForCategoryLabels")
    var defaultKey = $"defaults:{key}";
    value = configuration[defaultKey];
    if (!string.IsNullOrEmpty(value))
    {
        Console.WriteLine($"Using default config: {defaultKey}");
        return value;
    }

    return null;
}

// Create temp directory for downloaded data and models
var tempDir = Path.Combine(Path.GetTempPath(), $"model-retrainer-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempDir);

try
{
    // Process each repository
    foreach (var repo in argsData.Repos)
    {
        var fullRepoName = $"{argsData.Org}/{repo}";
        Console.WriteLine($"Processing repository: {fullRepoName}");

        // Process issues models
        if (argsData.RetrainIssues)
        {
            var categoryIssuesModelBlobName = GetConfigValue(fullRepoName, "IssueModelForCategoryLabels");
            var serviceIssuesModelBlobName = GetConfigValue(fullRepoName, "IssueModelForServiceLabels");

            if (categoryIssuesModelBlobName is null || serviceIssuesModelBlobName is null)
            {
                Console.WriteLine($"WARNING: Skipping issues models for {fullRepoName}: Missing configuration for IssueModelForCategoryLabels or IssueModelForServiceLabels");
            }
            else
            {
                var issuesDataPath = Path.Combine(tempDir, $"{repo}-issues.tsv");
                var categoryIssuesModelPath = Path.Combine(tempDir, $"{repo}-category-issues-model.zip");
                var serviceIssuesModelPath = Path.Combine(tempDir, $"{repo}-service-issues-model.zip");

                try
                {
                    // Download issues data from GitHub
                    await GitHubDataDownloader.DownloadIssuesAsync(
                        argsData.GitHubToken,
                        argsData.Org,
                        [repo],
                        issuesDataPath,
                        argsData.IssuesLimit,
                        argsData.PageSize,
                        argsData.PageLimit,
                        argsData.Retries,
                        argsData.ExcludedAuthors,
                        argsData.Verbose,
                        requireBothLabels: true);

                    // Train models
                    ModelTrainer.CreateModel(issuesDataPath, categoryIssuesModelPath, ModelType.Issue, LabelType.Category);
                    ModelTrainer.CreateModel(issuesDataPath, serviceIssuesModelPath, ModelType.Issue, LabelType.Service);

                    // Upload models to blob storage
                    if (!argsData.DryRun)
                    {
                        Console.WriteLine("Connecting to Azure Blob Storage...");
                        var blobServiceClient = new BlobServiceClient(new Uri(argsData.BlobStorageUri!), credential);
                        var containerClient = blobServiceClient.GetBlobContainerClient(argsData.BlobContainerName);
                        await containerClient.CreateIfNotExistsAsync();
                        await UploadModelToBlob(containerClient, categoryIssuesModelPath, categoryIssuesModelBlobName);
                        await UploadModelToBlob(containerClient, serviceIssuesModelPath, serviceIssuesModelBlobName);
                    }
                    else
                    {
                        Console.WriteLine($"Dry run mode: Would upload to {categoryIssuesModelBlobName} and {serviceIssuesModelBlobName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Error retraining issues models for {fullRepoName}: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    success = false;
                }
            }
        }

        // Process pull requests models
        if (argsData.RetrainPulls)
        {
            var categoryPullsModelBlobName = GetConfigValue(fullRepoName, "PrModelForCategoryLabels");
            var servicePullsModelBlobName = GetConfigValue(fullRepoName, "PrModelForServiceLabels");

            if (categoryPullsModelBlobName is null || servicePullsModelBlobName is null)
            {
                Console.WriteLine($"WARNING: Skipping pulls models for {fullRepoName}: Missing configuration for PrModelForCategoryLabels or PrModelForServiceLabels");
            }
            else
            {
                var pullsDataPath = Path.Combine(tempDir, $"{repo}-pulls.tsv");
                var categoryPullsModelPath = Path.Combine(tempDir, $"{repo}-category-pulls-model.zip");
                var servicePullsModelPath = Path.Combine(tempDir, $"{repo}-service-pulls-model.zip");

                try
                {
                    // Download pull requests data from GitHub
                    await GitHubDataDownloader.DownloadPullRequestsAsync(
                        argsData.GitHubToken,
                        argsData.Org,
                        [repo],
                        pullsDataPath,
                        argsData.PullsLimit,
                        argsData.PageSize,
                        argsData.PageLimit,
                        argsData.Retries,
                        argsData.ExcludedAuthors,
                        argsData.Verbose,
                        requireBothLabels: true);

                    // Train models
                    ModelTrainer.CreateModel(pullsDataPath, categoryPullsModelPath, ModelType.PullRequest, LabelType.Category);
                    ModelTrainer.CreateModel(pullsDataPath, servicePullsModelPath, ModelType.PullRequest, LabelType.Service);

                    // Upload models to blob storage
                    if (!argsData.DryRun)
                    {
                        Console.WriteLine("Connecting to Azure Blob Storage...");
                        var blobServiceClient = new BlobServiceClient(new Uri(argsData.BlobStorageUri!), credential);
                        var containerClient = blobServiceClient.GetBlobContainerClient(argsData.BlobContainerName);
                        await containerClient.CreateIfNotExistsAsync();
                        await UploadModelToBlob(containerClient!, categoryPullsModelPath, categoryPullsModelBlobName);
                        await UploadModelToBlob(containerClient!, servicePullsModelPath, servicePullsModelBlobName);
                    }
                    else
                    {
                        Console.WriteLine($"Dry run mode: Would upload to {categoryPullsModelBlobName} and {servicePullsModelBlobName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Error retraining pull requests models for {fullRepoName}: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    success = false;
                }
            }
        }
    }
}
finally
{
    // Cleanup temp directory
    try
    {
        Directory.Delete(tempDir, recursive: true);
    }
    catch
    {
        Console.WriteLine($"WARNING: Failed to cleanup temp directory: {tempDir}");
    }
}

return success ? 0 : 1;

async Task UploadModelToBlob(BlobContainerClient containerClient, string localPath, string blobName)
{
    Console.WriteLine($"Uploading model to blob: {blobName}...");

    var blobClient = containerClient.GetBlobClient(blobName);
    await blobClient.UploadAsync(localPath, overwrite: true);

    Console.WriteLine($"Successfully uploaded model to blob: {blobName}");
}
