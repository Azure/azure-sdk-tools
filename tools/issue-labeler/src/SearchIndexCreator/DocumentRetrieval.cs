// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Octokit;
using Azure.Storage.Blobs;
using Azure.Identity;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace SearchIndexCreator
{
    public class DocumentRetrieval
    {
        private readonly GitHubClient _client;
        private readonly string _token;

        public DocumentRetrieval(string token)
        {
            _client = new GitHubClient(new Octokit.ProductHeaderValue("Microsoft-ML-IssueBot-test"));
            var tokenAuth = new Credentials(token);
            _client.Credentials = tokenAuth;
            _token = token;
        }


        /// <summary>
        /// Retrieves documents from the specified repository.
        /// </summary>
        /// <param name="owner">The owner of the repository.</param>
        /// <param name="repo">The name of the repository.</param>
        /// <returns>A list of tuples containing file path, URL, and content of the documents.</returns>
        public async Task<List<(string FilePath, string Url, string Content)>> GetDocuments(string owner, string repo)
        {
            var documentPaths = new List<string>();
            await DirectoryTree(_token, documentPaths, owner, repo);
            var documentFiles = new List<(string FilePath, string Url, string Content)>();
            await GetDocumentContents(owner, repo, documentFiles, documentPaths);
            return documentFiles;
        }


        /// <summary>
        /// Uploads files to an Azure Blob Storage container.
        /// </summary>
        /// <param name="files">The list of files to upload.</param>
        /// <param name="accountName">The name of the Azure Storage account.</param>
        /// <param name="containerName">The name of the container to upload to.</param>
        private async Task GetDocumentContents(string owner, string repo, List<(string FilePath, string Url, string Content)> readmeFiles, List<string> documentPaths)
        {
            foreach (var path in documentPaths)
            {
                try
                {
                    var fileContent = await _client.Repository.Content.GetAllContents(owner, repo, path);
                    if (fileContent[0].Content != "")
                    {
                        readmeFiles.Add((fileContent[0].Path, fileContent[0].HtmlUrl, fileContent[0].Content));
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error getting repository contents for path: {path}", ex);
                }
            }
        }

        /// <summary>
        /// Uploads files to an Azure Blob Storage container.
        /// </summary>
        /// <param name="files">The list of files to upload.</param>
        /// <param name="accountName">The name of the Azure Storage account.</param>
        /// <param name="containerName">The name of the container to upload to.</param>
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
                    var blobHttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = "application/json" // Specify the content type
                    };

                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent)))
                    {
                        await blobClient.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
                        {
                            HttpHeaders = blobHttpHeaders
                        });
                    }

                    Console.WriteLine($"Uploaded {file.FilePath} to {containerClient.Name} container.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error uploading file: {file.FilePath}", ex);
                }
            }
        }


        /// <summary>
        /// Gets the BlobServiceClient for the specified account name.
        /// </summary>
        /// <param name="accountName">The name of the Azure Storage account.</param>
        /// <returns>The BlobServiceClient instance.</returns>
        public BlobServiceClient GetBlobServiceClient(string accountName)
        {
            BlobServiceClient client = new(
                new Uri($"https://{accountName}.blob.core.windows.net/"),
                new DefaultAzureCredential());

            return client;
        }


        /// <summary>
        /// Retrieves all file paths in the directory tree that have to do with documentation.
        /// </summary>
        /// <param name="githubToken">The GitHub token for authentication.</param>
        /// <param name="documentPaths">The list to store the document paths.</param>
        /// <param name="repoOwner">The owner of the repository.</param>
        /// <param name="repoName">The name of the repository.</param>
        public async Task DirectoryTree(string githubToken, List<string> documentPaths, string repoOwner, string repoName)
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
                    var response = await client.GetAsync($"repos/{repoOwner}/{repoName}/git/trees/main?recursive=1");

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        var gitTree = JsonConvert.DeserializeObject<GitTree>(responseData);

                        // Filter paths that end with readme.md (case insensitive)
                        foreach (var item in gitTree.Tree)
                        {
                            if (item.Path.ToLower().EndsWith("readme.md"))
                            {
                                documentPaths.Add(item.Path);
                            }
                            // Filter for samples. Typically samples are in a folder named "sample" and have a .md extension excluding readme files. 
                            else if (System.Text.RegularExpressions.Regex.IsMatch(item.Path.ToLower(), @"^.*sample\/*[^\/]+\.md$") && !item.Path.ToLower().EndsWith("readme.md"))
                            {
                                documentPaths.Add(item.Path);
                            }
                        }

                        // Print the list of readme.md paths
                        foreach (var readmePath in documentPaths)
                        {
                            Console.WriteLine(readmePath);
                        }
                    }
                    else
                    {
                        throw new Exception($"GitHub API request failed with status code: {response.StatusCode}");
                    }
                }
                catch (HttpRequestException e)
                {
                    throw new Exception("Request error while retrieving directory tree.", e);
                }
                catch (Exception e)
                {
                    throw new Exception("Unexpected error occurred while retrieving directory tree.", e);
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
