// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Octokit;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Azure.Identity;

namespace SearchIndexCreator
{
    public class IssueRetrieval
    {
        private Credentials _tokenAuth;
        private IConfiguration _config;

        public IssueRetrieval(Credentials tokenAuth, IConfiguration config) 
        { 
            _tokenAuth = tokenAuth;
            _config = config;
        }


        /// <summary>
        /// Retrieves all issues from the specified repository. Will take a while to run.
        /// </summary>
        /// <param name="repoOwner">The owner of the repository.</param>
        /// <param name="repo">The name of the repository.</param>
        /// <returns>A list of issues.</returns>
        public async Task<List<Issue>> RetrieveAllIssues(string repoOwner, string repo)
        {
            var client = new GitHubClient(new ProductHeaderValue("Microsoft-ML-IssueBot"));
            client.Credentials = _tokenAuth;

            var issueReq = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Closed,
                Filter = IssueFilter.All,
            };

            //Required Labels for issues
            issueReq.Labels.Add("customer-reported");
            issueReq.Labels.Add("issue-addressed");

            Console.WriteLine("Retrieving Issues....\n");

            var issues = await client.Issue.GetAllForRepository(repoOwner, repo, issueReq);

            Console.WriteLine("Done loading all issues");
            Console.WriteLine("\n======================================\n");
            Console.WriteLine("Retrieving issue comments....\n(This will take some time)");

            List<Issue> results = new List<Issue>();

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

                if (service is null || category is null )
                {
                    continue;
                }

                // You have to get the comments for the issue separately, only other way is using GraphQL
                var issue_comments = await client.Issue.Comment.GetAllForIssue(repoOwner, repo, issue.Number);
                
                
                string comments = string.Join(",", issue_comments.Select(x => x.Body));
                Issue newIssue = new Issue
                {
                    Id = $"{repoOwner}/{repo}:{issue.Number.ToString()}",
                    Title = issue.Title,
                    Body = $"Issue Body:\n{issue.Body}\nComments:\n{comments}",
                    Service = service.Name,
                    Category = category.Name,
                    Author = issue.User.Login,
                    Repository = repoOwner + "/" + repo,
                    CreatedAt = issue.CreatedAt,
                    Url = issue.HtmlUrl
                };

                results.Add(newIssue);
            }

            Console.WriteLine("Done loading all issue comments");

            return results;
        }


        private static bool IsServiceLabel(Label label) =>
            string.Equals(label.Color, "e99695", StringComparison.InvariantCultureIgnoreCase);

        private static bool IsCategoryLabel(Label label) =>
            string.Equals(label.Color, "ffeb77", StringComparison.InvariantCultureIgnoreCase);

        public void DownloadIssue<T>(List<T> issues)
        {
            string jsonString = JsonSerializer.Serialize(issues);
            File.WriteAllText("issues.json", jsonString);
        }

        /// <summary>
        /// Retrieves issue examples from the specified repository within the given number of days.
        /// </summary>
        /// <param name="repoOwner">The owner of the repository.</param>
        /// <param name="repo">The name of the repository.</param>
        /// <param name="days">The number of days to look back for issues.</param>
        /// <returns>A list of issues formatted as they would be given to the azure function.</returns>
        public async Task<List<IssuePayload>> RetrieveIssueExamples(string repoOwner, string repo, int days)
        {
            var client = new GitHubClient(new ProductHeaderValue("Microsoft-ML-IssueBot"));
            client.Credentials = _tokenAuth;

            var issueReq = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Open,
                Filter = IssueFilter.All,
                Since = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(days))
            };

            //Required Labels for issues
            issueReq.Labels.Add("customer-reported");
            issueReq.Labels.Add("issue-addressed");

            Console.WriteLine("Retrieving Issues....");

            var issues = await client.Issue.GetAllForRepository(repoOwner, repo, issueReq);

            List<IssuePayload> results = new List<IssuePayload>();
            foreach (var issue in issues)
            {
                IssuePayload newIssue = new IssuePayload
                {
                    IssueNumber = issue.Number,
                    Title = issue.Title,
                    Body = issue.Body,
                    IssueUserLogin = issue.User.Login,
                    RepositoryName = repoOwner + "/" + repo,
                };

                results.Add(newIssue);
            }

            return results;
        }

        /// <summary>
        /// Uploads the issues to an Azure Blob Storage container.
        /// </summary>
        /// <param name="issues">The list of issues to upload.</param>
        /// <param name="accountName">The name of the Azure Storage account.</param>
        /// <param name="containerName">The name of the container to upload to.</param>
        public async Task UploadIssues(List<Issue> issues, string accountName, string containerName)
        {
            var blobServiceClient = GetBlobServiceClient(accountName);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            foreach (var issue in issues)
            {
                var blobClient = containerClient.GetBlobClient(issue.Id);

                try
                {
                    var jsonContent = JsonSerializer.Serialize(issue);

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

                    Console.WriteLine($"Uploaded {issue.Id} to {containerClient.Name} container.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error uploading file: {issue.Id}", ex);
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


        public class Issue
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public string Service { get; set; }
            public string Category { get; set; }
            public string Author { get; set; }
            public string Repository { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public string Url { get; set; }
        }

        // Used to get examples for the function
        public class IssuePayload
        {
            public int IssueNumber { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public string IssueUserLogin { get; set; }
            public string RepositoryName { get; set; }
        }
    }
}
