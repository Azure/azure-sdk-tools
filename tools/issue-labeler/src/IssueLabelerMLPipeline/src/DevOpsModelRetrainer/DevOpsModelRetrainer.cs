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
            success &= await RetrainModelAsync(
                repo,
                fullRepoName,
                ModelType.Issue,
                categoryConfigKey: "IssueModelForCategoryLabels",
                serviceConfigKey: "IssueModelForServiceLabels",
                downloadLimit: argsData.IssuesLimit);
        }

        // Process pull requests models
        if (argsData.RetrainPulls)
        {
            success &= await RetrainModelAsync(
                repo,
                fullRepoName,
                ModelType.PullRequest,
                categoryConfigKey: "PrModelForCategoryLabels",
                serviceConfigKey: "PrModelForServiceLabels",
                downloadLimit: argsData.PullsLimit);
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

async Task<bool> RetrainModelAsync(
    string repo,
    string fullRepoName,
    ModelType modelType,
    string categoryConfigKey,
    string serviceConfigKey,
    int? downloadLimit)
{
    var typeName = modelType == ModelType.Issue ? "issues" : "pulls";
    var categoryModelBlobName = GetConfigValue(fullRepoName, categoryConfigKey);
    var serviceModelBlobName = GetConfigValue(fullRepoName, serviceConfigKey);

    if (categoryModelBlobName is null || serviceModelBlobName is null)
    {
        Console.WriteLine($"WARNING: Skipping {typeName} models for {fullRepoName}: Missing configuration for {categoryConfigKey} or {serviceConfigKey}");
        return true; // Not a failure, just skipped
    }

    var dataPath = Path.Combine(tempDir, $"{repo}-{typeName}.tsv");
    var categoryModelPath = Path.Combine(tempDir, $"{repo}-category-{typeName}-model.zip");
    var serviceModelPath = Path.Combine(tempDir, $"{repo}-service-{typeName}-model.zip");

    try
    {
        // Download data from GitHub
        if (modelType == ModelType.Issue)
        {
            await GitHubDataDownloader.DownloadIssuesAsync(
                argsData.GitHubToken,
                argsData.Org,
                [repo],
                dataPath,
                downloadLimit,
                argsData.PageSize,
                argsData.PageLimit,
                argsData.Retries,
                argsData.ExcludedAuthors,
                argsData.Verbose,
                requireBothLabels: true);
        }
        else
        {
            await GitHubDataDownloader.DownloadPullRequestsAsync(
                argsData.GitHubToken,
                argsData.Org,
                [repo],
                dataPath,
                downloadLimit,
                argsData.PageSize,
                argsData.PageLimit,
                argsData.Retries,
                argsData.ExcludedAuthors,
                argsData.Verbose,
                requireBothLabels: true);
        }

        string[]? syntheticDataPaths = null;
        if (argsData.TrainWithSyntheticData)
        {
            syntheticDataPaths = await DownloadSyntheticData(repo, tempDir);
        }

        // Train models
        ModelTrainer.CreateModel(dataPath, categoryModelPath, modelType, LabelType.Category, syntheticDataPaths);
        ModelTrainer.CreateModel(dataPath, serviceModelPath, modelType, LabelType.Service, syntheticDataPaths);

        // Upload models to blob storage
        if (!argsData.DryRun)
        {
            Console.WriteLine("Connecting to Azure Blob Storage...");
            var blobServiceClient = new BlobServiceClient(new Uri(GetConfigValue(fullRepoName, "BlobAccountUri")!), credential);
            var containerClient = blobServiceClient.GetBlobContainerClient(GetConfigValue(fullRepoName, "BlobContainerName")!);
            await containerClient.CreateIfNotExistsAsync();
            await UploadModelToBlob(containerClient, categoryModelPath, categoryModelBlobName);
            await UploadModelToBlob(containerClient, serviceModelPath, serviceModelBlobName);
        }
        else
        {
            Console.WriteLine($"Dry run mode: Skip upload to {categoryModelBlobName} and {serviceModelBlobName}");
        }

        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: Error retraining {typeName} models for {fullRepoName}: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return false;
    }
}

async Task UploadModelToBlob(BlobContainerClient containerClient, string localPath, string blobName)
{
    Console.WriteLine($"Uploading model to blob: {blobName}...");

    var blobClient = containerClient.GetBlobClient(blobName);
    await blobClient.UploadAsync(localPath, overwrite: true);

    Console.WriteLine($"Successfully uploaded model to blob: {blobName}");
}

async Task<string[]> DownloadSyntheticData(string repo, string tempDir)
{
    Console.WriteLine("Downloading synthetic data from blob storage...");
    
    var blobServiceClient = new BlobServiceClient(new Uri(GetConfigValue($"{argsData.Org}/{repo}", "BlobAccountUri")!), credential);
    // Container name cannot contain slashes - use "generated-issues" as container and repo as prefix
    var containerClient = blobServiceClient.GetBlobContainerClient("generated-issues");

    if (!await containerClient.ExistsAsync())
    {
        Console.WriteLine($"No synthetic data container 'generated-issues' found");
        return [];
    }

    var localFilePaths = new List<string>();
    var prefix = $"{repo}/";

    await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
    {
        var blobClient = containerClient.GetBlobClient(blobItem.Name);
        // Remove the repo prefix from the local file path
        var localFileName = blobItem.Name.StartsWith(prefix) ? blobItem.Name[prefix.Length..] : blobItem.Name;
        var localFilePath = Path.Combine(tempDir, localFileName);
        
        // Ensure directory exists for nested blob paths
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        Console.WriteLine($"Downloading: {blobItem.Name}");
        await blobClient.DownloadToAsync(localFilePath);
        localFilePaths.Add(localFilePath);
    }
    
    if (localFilePaths.Count == 0)
    {
        Console.WriteLine($"No synthetic data found with prefix '{prefix}'");
    }
    else
    {
        Console.WriteLine($"Synthetic data download complete. Downloaded {localFilePaths.Count} file(s).");
    }
    
    return [.. localFilePaths];
}
