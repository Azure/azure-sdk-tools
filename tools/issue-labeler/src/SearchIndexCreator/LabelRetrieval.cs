// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Octokit;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs.Models;

namespace SearchIndexCreator
{
    public class LabelRetrieval
    {
        private Credentials _tokenAuth;
        private IConfiguration _config;

        public LabelRetrieval(Credentials tokenAuth, IConfiguration config)
        {
            _tokenAuth = tokenAuth;
            _config = config;
        }

        public async Task CreateOrRefreshLabels(string repoOwner)
        {
            var client = new GitHubClient(new ProductHeaderValue("Microsoft-ML-IssueBot"));
            client.Credentials = _tokenAuth;
            
            var repoNamesConfig = _config["RepositoryNamesForLabels"];
            if (string.IsNullOrEmpty(repoNamesConfig))
            {
                throw new InvalidOperationException("RepositoryNamesForLabels configuration is missing or empty.");
            }

            var repoNames = repoNamesConfig.Split(';', StringSplitOptions.RemoveEmptyEntries);

            // Initialize BlobServiceClient (replace with your connection string)
            var blobServiceClient = GetBlobServiceClient(_config["IssueStorageName"]);
            var containerClient = blobServiceClient.GetBlobContainerClient("labels");

            // Ensure the container exists
            await containerClient.CreateIfNotExistsAsync();

            foreach (var repo in repoNames)
            {
                // Fetch labels for the repository
                var labels = await client.Issue.Labels.GetAllForRepository(repoOwner, repo);
                
                // Upload JSON to blob storage
                var blobClient = containerClient.GetBlobClient(repo);
                IEnumerable<object> labelData;
                try
                {
                    if (repoOwner.Equals("microsoft", StringComparison.OrdinalIgnoreCase)) {
                        labelData = McpLabelFilter(labels);
                    }
                    else {
                        labelData = AzureSdkLabelFilter(labels);
                    }
                    // Serialize the custom object to JSON
                    var labelsJson = JsonSerializer.Serialize(labelData);

                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(labelsJson));
                    await blobClient.DeleteIfExistsAsync();

                    var uploadOptions = new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = "application/json"
                        }
                    };

                    await blobClient.UploadAsync(stream, uploadOptions);

                    Console.WriteLine($"Uploaded {repo} to {containerClient.Name} container.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error uploading labels for: {repo}", ex);
                }
            }
        }

        private BlobServiceClient GetBlobServiceClient(string accountName)
        {
            BlobServiceClient client = new(
                new Uri($"https://{accountName}.blob.core.windows.net/"),
                new DefaultAzureCredential());

            return client;
        }

        private static IEnumerable<object> AzureSdkLabelFilter(IReadOnlyList<Label> labels)
        {
            return labels
                .Where(label =>
                    string.Equals(label.Color, "ffeb77", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(label.Color, "e99695", StringComparison.OrdinalIgnoreCase))
                .Select(label => new
                {
                    Name = label.Name,
                    Color = label.Color
                });
        }

        private static IEnumerable<object> McpLabelFilter(IReadOnlyList<Label> labels)
        {
            return labels
                .Where(label =>
                    label.Name.StartsWith("server-", StringComparison.OrdinalIgnoreCase) ||
                    label.Name.StartsWith("tools-", StringComparison.OrdinalIgnoreCase) ||
                    label.Name.Equals("remote-mcp", StringComparison.OrdinalIgnoreCase))
                .Select(label => new
                {
                    Name = label.Name,
                    Type = label.Name.StartsWith("server-", StringComparison.OrdinalIgnoreCase) ? "Server" : "Tool",
                })
                .OrderBy(label => label.Name);
        }
    }
}
