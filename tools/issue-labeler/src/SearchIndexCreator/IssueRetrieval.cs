// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Octokit;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;

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

            // Get the CODEOWNERS file from the repository and override default file with URL path from octokit.
            var codeownersContents = await client.Repository.Content.GetAllContents(repoOwner, repo, ".github/CODEOWNERS");
            CodeOwnerUtils.codeOwnersFilePathOverride = codeownersContents[0].DownloadUrl;

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
                // Extract the service and category labels from this issue.
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

                // Ignore issues that don't have a service or category label.
                if (service is null || category is null )
                {
                    continue;
                }
                
                // Seperation of comments and issue body. We will create a "Issue" Object per comment and issue. 
                // Benefits of seperation:
                // New CodeOwner field that can be used to boost if the current comment is from a Code Owner 
                // meaning we can boost said comments score. 
                // Chunks with better impact, if we place it all in one big text it will chunk at random.
                Issue newIssue = new Issue
                {
                    Id = $"{repoOwner}/{repo}/{issue.Number.ToString()}/Issue",
                    Title = issue.Title,
                    Body = $"{issue.Body}",
                    Service = service.Name,
                    Category = category.Name,
                    Author = issue.User.Login,
                    Repository = repoOwner + "/" + repo,
                    CreatedAt = issue.CreatedAt,
                    Url = issue.HtmlUrl,
                    // Codeowner meaning the service owner is the author of this "comment"
                    // 0 -> false 1 -> true. Used numeric representation for magnitude scoring boost function. 
                    // Issue itself is false since it is not a comment
                    CodeOwner = 0
                };

                List<string> labels = new List<string>
                {
                    service.Name,
                    category.Name
                };

                // Method to get associated codeowners used by the current issue labeling bot. 
                var codeowners = CodeOwnerUtils.GetCodeownersEntryForLabelList(labels).AzureSdkOwners;

                results.Add(newIssue);

                // You have to get the comments for the issue separately, only other way is using GraphQL
                var issue_comments = await client.Issue.Comment.GetAllForIssue(repoOwner, repo, issue.Number);

                // Filter out comments from bots. Filtering out short comments because they
                // don't add much value, may just be a question or sayings thanks for reaching out etc.
                // Testing out 200 could change from 150 - 250.
                issue_comments = issue_comments
                    .Where(c => 
                        !c.User.Login.Equals("github-actions[bot]") && // Filter out bot comments
                        c.Body.Length > 250 && // Filter out short comments
                        !c.User.Login.Equals("ghost") && // Filter out comments from deleted users (mostly because the older bot seems to have been deleted)
                        !c.AuthorAssociation.StringValue.Equals("NONE")) // Filter out non member comments
                    .ToList();

                // Add comments in as there own "Issue" object
                foreach (var comment in issue_comments)
                {
                    Issue newComment = new Issue
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
                        CodeOwner = codeowners.Contains(comment.User.Login) ? 1 : 0
                    };
                    results.Add(newComment);
                }
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

            // Enable Soft delete on the Storage Blob
            await EnsureBlobSoftDeleteEnabled(blobServiceClient);

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
        /// Ensures that native blob soft delete is enabled on the storage account.
        /// </summary>
        /// <param name="blobServiceClient">The BlobServiceClient instance.</param>
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
            public int CodeOwner { get; set; }
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
