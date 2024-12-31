using Octokit;
using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

using Newtonsoft.Json;

namespace IssueManager
{
    public class DocumentRetrieval
    {
        private readonly GitHubClient _client;
        private readonly string _token;
        //private readonly ILogger<GitHubCrawler> _logger;

        public DocumentRetrieval(string token)//, ILogger<GitHubCrawler> logger)
        {
            _client = new GitHubClient(new Octokit.ProductHeaderValue("Microsoft-ML-IssueBot-test"));
            var tokenAuth = new Credentials(token);
            _client.Credentials = tokenAuth;
            _token = token;
            //_logger = logger;
        }

        public async Task<List<(string FilePath, string Url, string Content)>> GetReadmeFiles(string owner, string repo)
        {
            var readmePaths = new List<string>();
            await DirectoryTree(_token, readmePaths);
            var readmeFiles = new List<(string FilePath, string Url, string Content)>();
            await GetReadmeContents(owner, repo, readmeFiles, readmePaths);
            return readmeFiles;
        }

        private async Task GetReadmeContents(string owner, string repo, List<(string FilePath, string Url, string Content)> readmeFiles, List<string> readmePaths)
        {
            try
            {
                foreach (var path in readmePaths)
                {
                    var fileContent = await _client.Repository.Content.GetAllContents(owner, repo, path);
                    Console.WriteLine(fileContent[0].HtmlUrl);
                    readmeFiles.Add((fileContent[0].Path, fileContent[0].HtmlUrl, fileContent[0].Content));   
                }
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, $"Error getting repository contents for path: {path}");
                Console.WriteLine(ex.Message, $"Error getting repository contents for path readmes");
            }
        }

        public async Task UploadFiles(List<(string FilePath, string Url, string Content)> files, string accountName, string containerName)
        {
            var blobServiceClient = GetBlobServiceClient(accountName);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            foreach (var file in files)
            {
                var blobClient = containerClient.GetBlobClient(file.FilePath);

                try
                {
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(new { file.Url, file.Content });
                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent)))
                    {
                        await blobClient.UploadAsync(stream, true);
                    }

                    Console.WriteLine($"Uploaded {file.FilePath} to {containerClient.Name} container.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading file: {file.FilePath}\n" + ex);
                }
            }
        }

        public BlobServiceClient GetBlobServiceClient(string accountName)
        {
            BlobServiceClient client = new(
                new Uri($"https://{accountName}.blob.core.windows.net/"),
                new DefaultAzureCredential());

            return client;
        }

        public async Task DirectoryTree(string githubToken, List<string> readmePaths)
        {
            using (var client = new HttpClient())
            {
                // Set the base address and headers
                client.BaseAddress = new Uri("https://api.github.com/");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HttpClient", "1.0"));

                try
                {
                    // Make the GET request
                    var response = await client.GetAsync("repos/Azure/azure-sdk-for-net/git/trees/main?recursive=1");

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        var gitTree = JsonConvert.DeserializeObject<GitTree>(responseData);

                        // Filter paths that end with readme.md (case insensitive)
                        foreach (var item in gitTree.Tree)
                        {
                            if (item.Path.ToLower().EndsWith("readme.md"))
                            {
                                readmePaths.Add(item.Path);
                            }
                        }

                        // Print the list of readme.md paths
                        foreach (var readmePath in readmePaths)
                        {
                            Console.WriteLine(readmePath);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode}");
                    }
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Request error: {e.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unexpected error: {e.Message}");
                }
            }
        }

        private class TreeItem
        {
            public string Path { get; set; }
            public string Mode { get; set; }
            public string Type { get; set; }
            public string Sha { get; set; }
            public int Size { get; set; }
            public string Url { get; set; }
        }

        private class GitTree
        {
            public string Sha { get; set; }
            public string Url { get; set; }
            public List<TreeItem> Tree { get; set; }
            public bool Truncated { get; set; }
        }
    }
}
