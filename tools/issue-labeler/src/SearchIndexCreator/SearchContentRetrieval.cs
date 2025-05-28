// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Octokit;
using Newtonsoft.Json;
using SearchIndexCreator;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using System.Net.Http.Headers;

namespace SearchIndexCreator
{
    public class SearchContentRetrieval
    {

        private readonly IConfiguration _config;
        private readonly GitHubClient _client;
        private readonly string _token;
        private readonly string? _accountName;
        private readonly string? _containerName;


        public SearchContentRetrieval(IConfiguration config, string token)
        {
            _config = config;
            _token = token;
            _client = new GitHubClient(new Octokit.ProductHeaderValue("Microsoft-ML-IssueBot"));
            _client.Credentials = new Credentials(token);
            

            _accountName = config["DocumentStorageName"];
            _containerName = "search-content-blob";
        }

        public async Task<List<SearchContent>> RetrieveSearchContent(string repoOwner, string repo)
        {
            var codeownersContents = await _client.Repository.Content.GetAllContents(repoOwner, repo, ".github/CODEOWNERS");
            CodeOwnerUtils.codeOwnersFilePathOverride = codeownersContents[0].DownloadUrl;

            var issueReq = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Closed,
                Filter = IssueFilter.All,
            };
            issueReq.Labels.Add("customer-reported");
            issueReq.Labels.Add("issue-addressed");

            Console.WriteLine("Retrieving Issues....\n");

            var issues = await _client.Issue.GetAllForRepository(repoOwner, repo, issueReq);

            Console.WriteLine("Done loading all issues");
            Console.WriteLine("\n======================================\n");
            Console.WriteLine("Retrieving issue comments....\n(This will take some time)");

            List<SearchContent> results = new List<SearchContent>();

            foreach (var issue in issues)
            {
                Label service = null, category = null;

                foreach (var label in issue.Labels)
                {
                    if (IsCategoryLabel(label))
                    {
                        service = label;
                    }
                    else if (IsServiceLabel(label))
                    {
                        category = label;
                    }
                }
                if (service is null || category is null)
                {
                    continue;
                }
                SearchContent searchContent = new SearchContent
                {
                    Id = $"{repoOwner}/{repo}/{issue.Number.ToString()}/Issue",
                    Title = issue.Title,
                    Body = $"{issue.Body}",
                    Service = service.Name,
                    Category = category.Name,
                    Author = issue.User.Login,
                    Repository = $"{repoOwner}/{repo}",
                    CreatedAt = issue.CreatedAt,
                    Url = issue.HtmlUrl,
                    // Codeowner meaning the service owner is the author of this "comment"
                    // 0 -> false 1 -> true. Used numeric representation for magnitude scoring boost function. 
                    // Issue itself is false since it is not a comment
                    CodeOwner = 0,
                    DocumentType = "Issue"
                };
                List<string> labels = new List<string>
                {
                    service.Name,
                    category.Name
                };
                var codeowners = CodeOwnerUtils.GetCodeownersEntryForLabelList(labels).AzureSdkOwners;
                results.Add(searchContent);
                var issue_comments = await _client.Issue.Comment.GetAllForIssue(repoOwner, repo, issue.Number);
                issue_comments = issue_comments
                    .Where(c =>
                        !c.User.Login.Equals("github-actions[bot]") && // Filter out bot comments
                        c.Body.Length > 250 && // Filter out short comments
                        !c.User.Login.Equals("ghost") && // Filter out comments from deleted users (mostly because the older bot seems to have been deleted)
                        !c.AuthorAssociation.StringValue.Equals("NONE")) // Filter out non member comments
                    .ToList();
                foreach (var comment in issue_comments)
                {
                    SearchContent commentContent = new SearchContent
                    {
                        Id = $"{repoOwner}/{repo}/{issue.Number.ToString()}/{comment.Id.ToString()}",
                        Title = issue.Title,
                        Body = $"{comment.Body}",
                        Service = service.Name,
                        Category = category.Name,
                        Author = comment.User.Login,
                        Repository = repoOwner + "/" + repo,
                        CreatedAt = comment.CreatedAt,
                        Url = comment.HtmlUrl,
                        // If the author is in the codeowners list 1 (true) else 0 (false)
                        CodeOwner = codeowners.Contains(comment.User.Login) ? 1 : 0,
                        DocumentType = "Issue"
                    };
                    results.Add(commentContent);
                }
            }
            Console.WriteLine("Done loading all issue comments");


            Console.WriteLine("Retrieving Documents....\n");
            var documentPaths = new List<string>();

            await DirectoryTree(_token, documentPaths, repoOwner, repo);
            foreach (var path in documentPaths)
            {
                try
                {
                    var fileContent = await _client.Repository.Content.GetAllContents(repoOwner, repo, path);
                    if (fileContent[0].Content != "")
                    {
                        SearchContent documentSearchContent = new SearchContent
                        {
                            Id = $"{repoOwner}/{repo}/{fileContent[0].Path}",
                            Title = Path.GetFileName(fileContent[0].Path),
                            Body = fileContent[0].Content,
                            Service = null,
                            Category = null,
                            Author = null,
                            Repository = $"{repoOwner}/{repo}",
                            CreatedAt = null,
                            Url = fileContent[0].HtmlUrl,
                            CodeOwner = 0, // No code owner for documents
                            DocumentType = "Document"
                        };
                        results.Add(documentSearchContent);
                        Console.WriteLine($"Retrieved document: {fileContent[0].Path}");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error getting repository contents for path: {path}", ex);
                }
            }

            return results;

        }


        public async Task UploadSearchContent(List<SearchContent> searchContents)
        {
            var blobServiceClient = GetBlobServiceClient(_accountName);
            await EnsureBlobSoftDeleteEnabled(blobServiceClient);

            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();

            foreach (var content in searchContents)
            {
                var blobClient = containerClient.GetBlobClient(content.Id);
                try
                {
                    var jsonContent = JsonConvert.SerializeObject(content);
                    var blobHttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    };
                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent)))
                    {
                        await blobClient.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
                        {
                            HttpHeaders = blobHttpHeaders
                        });
                    }
                    Console.WriteLine($"Uploaded {content.Id} to {containerClient.Name} container.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error uploading file: {content.Id}", ex);
                }
            }

        }
        private async Task DirectoryTree(string githubToken, List<string> documentPaths, string repoOwner, string repoName)
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
        private static bool IsServiceLabel(Label label) =>
            string.Equals(label.Color, "e99695", StringComparison.InvariantCultureIgnoreCase);

        private static bool IsCategoryLabel(Label label) =>
            string.Equals(label.Color, "ffeb77", StringComparison.InvariantCultureIgnoreCase);

        private BlobServiceClient GetBlobServiceClient(string accountName)
        {
            BlobServiceClient client = new(
                new Uri($"https://{accountName}.blob.core.windows.net/"),
                new DefaultAzureCredential());

            return client;
        }

        private async Task EnsureBlobSoftDeleteEnabled(BlobServiceClient blobServiceClient)
        {
            var properties = await blobServiceClient.GetPropertiesAsync();

            if (!properties.Value.DeleteRetentionPolicy.Enabled)
            {
                properties.Value.DeleteRetentionPolicy = new Azure.Storage.Blobs.Models.BlobRetentionPolicy
                {
                    Enabled = true,
                    Days = 7 // Set the desired retention period
                };
                await blobServiceClient.SetPropertiesAsync(properties.Value);
                Console.WriteLine("Enabled native blob soft delete on the storage account.");
            }
            else
            {
                Console.WriteLine("Blob soft delete is already enabled on the storage account.");
            }
        }
        
    }
}
