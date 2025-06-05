// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Octokit;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using IssueLabeler.Shared;

namespace SearchIndexCreator
{
    public class IssueTriageContentRetrieval
    {
        private readonly GitHubClient _client;
        private readonly string? _token;
        private readonly string? _accountName;
        private readonly string? _containerName;
        private readonly string? _repo;

        public IssueTriageContentRetrieval(IConfiguration config)
        {
            _token = config["GithubKey"];
            _client = new GitHubClient(new Octokit.ProductHeaderValue("Microsoft-ML-IssueBot"));
            _client.Credentials = new Credentials(_token);
            _accountName = config["StorageName"];
            _containerName = $@"{config["ContainerName"]}-blob";
            _repo = config["repo"];
        }

        public async Task<List<IssueTriageContent>> RetrieveContent(string repoOwner)
        {
            var codeownersContents = await _client.Repository.Content.GetAllContents(repoOwner, _repo, ".github/CODEOWNERS");
            CodeOwnerUtils.codeOwnersFilePathOverride = codeownersContents[0].DownloadUrl;

            var issues = await RetrieveAllIssuesAsync(repoOwner);
            var documents = await RetrieveAllDocumentsAsync(repoOwner);

            var results = new List<IssueTriageContent>();
            results.AddRange(issues);
            results.AddRange(documents);

            return results;
        }

        public async Task UploadSearchContent(List<IssueTriageContent> IssueTriageContents)
        {
            if (string.IsNullOrEmpty(_accountName))
            {
                throw new ArgumentNullException(nameof(_accountName), "Storage account name cannot be null or empty.");
            }

            var blobServiceClient = GetBlobServiceClient(_accountName);
            await EnsureBlobSoftDeleteEnabled(blobServiceClient);

            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();

            foreach (var content in IssueTriageContents)
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

        public async Task<List<IssuePayload>> RetrieveIssueExamples(string repoOwner, string repoName, int days)
        {
            var issueReq = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Closed,
                Filter = IssueFilter.All,
                Since = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(days))
            };

            issueReq.Labels.Add("customer-reported");

            issueReq.Labels.Add("issue-addressed");

            var issues = await _client.Issue.GetAllForRepository(repoOwner, repoName, issueReq);

            var results = new List<IssuePayload>();

            foreach (var issue in issues)
            {
                if (issue.PullRequest != null)
                {
                    continue; // Skip pull requests
                }

                var payload = new IssuePayload
                {
                    IssueNumber = issue.Number,
                    Title = issue.Title,
                    Body = issue.Body ?? string.Empty,
                    IssueUserLogin = issue.User.Login,
                    RepositoryName = repoName,
                    RepositoryOwnerName = repoOwner,
                };

                results.Add(payload);
            }

            return results;
        }

        private async Task<List<IssueTriageContent>> RetrieveAllDocumentsAsync(string repoOwner)
        {
            Console.WriteLine("Retrieving Documents....\n");
            var results = new List<IssueTriageContent>();
            var documentPaths = new List<string>();

            await DirectoryTree(_token, documentPaths, repoOwner, _repo);

            foreach (var path in documentPaths)
            {
                try
                {
                    var fileContent = await _client.Repository.Content.GetAllContents(repoOwner, _repo, path);

                    if (!String.IsNullOrEmpty(fileContent[0].Content))
                    {
                        IssueTriageContent documentSearchContent = new IssueTriageContent(
                            $"{repoOwner}/{_repo}/{fileContent[0].Path}",
                            $"{repoOwner}/{_repo}",
                            fileContent[0].HtmlUrl,
                            DocumentTypes.Document.ToString()
                        )
                        {
                            Title = Path.GetFileName(fileContent[0].Path),
                            Body = fileContent[0].Content,
                            Service = null,
                            Category = null,
                            Author = null,
                            CreatedAt = null,
                            CodeOwner = 0 // No code owner for documents
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

        private async Task<List<IssueTriageContent>> RetrieveAllIssuesAsync(string repoOwner)
        {
            List<IssueTriageContent> results = new List<IssueTriageContent>();

            var issueReq = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Closed,
                Filter = IssueFilter.All,
            };
            issueReq.Labels.Add("customer-reported");
            issueReq.Labels.Add("issue-addressed");

            var issues = await _client.Issue.GetAllForRepository(repoOwner, _repo, issueReq);

            foreach (var issue in issues)
            {
                var (service, category) = GetServiceAndCategoryLabels(issue.Labels);

                if (service is null || category is null)
                {
                    continue;
                }

                List<string> labels = new List<string>
                {
                    service.Name,
                    category.Name
                };

                var codeowners = CodeOwnerUtils.GetCodeownersEntryForLabelList(labels).AzureSdkOwners;

                IssueTriageContent searchContent = new IssueTriageContent(
                    $"{repoOwner}/{_repo}/{issue.Number.ToString()}/Issue",
                    repoOwner + "/" + _repo,
                    issue.HtmlUrl,
                    DocumentTypes.Issue.ToString()
                )
                {
                    Title = issue.Title,
                    Body = $"{issue.Body}",
                    Service = service.Name,
                    Category = category.Name,
                    Author = issue.User.Login,
                    CreatedAt = issue.CreatedAt,
                    CodeOwner = 0
                };

                results.Add(searchContent);

                var issue_comments = await GetIssueCommentsAsync(repoOwner, _repo, issue.Number);

                foreach (var comment in issue_comments)
                {

                    IssueTriageContent commentContent = new IssueTriageContent(
                        $"{repoOwner}/{_repo}/{issue.Number.ToString()}/{comment.Id.ToString()}",
                        repoOwner + "/" + _repo,
                        comment.HtmlUrl,
                        DocumentTypes.Issue.ToString()
                    )
                    {
                        Title = issue.Title,
                        Body = $"{comment.Body}",
                        Service = service.Name,
                        Category = category.Name,
                        Author = comment.User.Login,
                        CreatedAt = comment.CreatedAt,
                        CodeOwner = codeowners.Contains(comment.User.Login) ? 1 : 0,
                    };

                    results.Add(commentContent);

                }
            }

            return results;
        }

        private static bool IsServiceLabel(Octokit.Label label) =>
            string.Equals(label.Color, "e99695", StringComparison.InvariantCultureIgnoreCase);

        private static bool IsCategoryLabel(Octokit.Label label) =>
            string.Equals(label.Color, "ffeb77", StringComparison.InvariantCultureIgnoreCase);

        private (Octokit.Label service, Octokit.Label category) GetServiceAndCategoryLabels(IReadOnlyList<Octokit.Label> labels)
        {
            Octokit.Label service = null, category = null;

            foreach (var label in labels)
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
            return (service, category);
        }

        private async Task<List<IssueComment>> GetIssueCommentsAsync(string repoOwner, string repo, int issueNumber)
        {
            var issue_comments = await _client.Issue.Comment.GetAllForIssue(repoOwner, repo, issueNumber);

            return issue_comments
                .Where(c =>
                    !c.User.Login.Equals("github-actions[bot]") && // Filter out bot comments
                    c.Body.Length > 250 && // Filter out short comments
                    !c.User.Login.Equals("ghost") && // Filter out comments from deleted users (mostly because the older bot seems to have been deleted)
                    !c.AuthorAssociation.StringValue.Equals("NONE")) // Filter out non member comments
                .ToList();
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
