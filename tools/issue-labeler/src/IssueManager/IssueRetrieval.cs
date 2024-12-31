using Octokit;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Azure;

namespace IssueManager
{
    public class IssueRetrieval
    {
        private Credentials _tokenAuth;

        public IssueRetrieval(Credentials tokenAuth) 
        { 
            _tokenAuth = tokenAuth;
        }

        //Expect this method to take a while
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

            Console.WriteLine("Retrieving Issues....");

            var issues = await client.Issue.GetAllForRepository(repoOwner, repo, issueReq);

            Console.WriteLine("Done loading all issues");
            Console.WriteLine("\n======================================\n");
            Console.WriteLine("Retrieving issue comments....");

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
                    Id = $"{repoOwner}/{repo}:{issue.Id.ToString()}",
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

        public void DownloadIssue(List<Issue> issues)
        {
            string jsonString = JsonSerializer.Serialize(issues);
            File.WriteAllText("issues.json", jsonString);
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

    }

    internal class ManualRun
    {
        private static IConfigurationRoot config = new ConfigurationBuilder()
            .AddUserSecrets<ManualRun>()
            .Build();

        public static async Task Main(string[] args)
        {
            // Testing the IssueRetrieval class
            var tokenAuth = new Credentials(config["GithubKey"]);
            var issueRetrieval = new IssueRetrieval(tokenAuth);
            //var issues = await issueRetrieval.RetrieveAllIssues("Azure", "azure-sdk-for-net");

            //Console.WriteLine($"Retrieved {issues.Count} issues");

            string indexName = "gh-issue-index-test2";
            Uri searchEndpoint = new Uri(config["SearchEndpoint"]);
            Uri openAIEndpoint = new Uri(config["OpenAIEndpoint"]);
            AzureKeyCredential searchCredential = new AzureKeyCredential(config["AzureCredential"]);
            AzureKeyCredential openAICredential = new AzureKeyCredential(config["OpenAIKey"]);

            //issueRetrieval.DownloadIssue(issues);

            //var issueLabeler = new IssueLabelerAzureSearch(searchEndpoint, openAIEndpoint, searchCredential, openAICredential, indexName);

            //Console.WriteLine(issueLabeler.SendMessageQna("Library name and version\nAzure.Identity 1.13.1\n\nDescribe the bug\nWhen using ClientSecretCredential to authenticate using an SPN we get this error (.net 8).\n\nClientSecretCredential authentication failed: Method not found: 'Void System.Text.Json.Serialization.Metadata.JsonObjectInfoValues`1.set_ObjectCreator(System.Func`1<!0>)'.\n\nThis is mainly because the we updated the Microsoft related libraries to 9.0.0 (including Azure.Identity 1.13.1 which refers System.Text.Json 9.0.0) which should be .net 8 compatible.\n\nForcing to use 8.0.x libraries for all Microsoft libraries, fixes the issue.\n\nExpected behavior\nClientSecretCredential token is retrieved.\n\nActual behavior\nException: ClientSecretCredential authentication failed: Method not found: 'Void System.Text.Json.Serialization.Metadata.JsonObjectInfoValues`1.set_ObjectCreator(System.Func`1<!0>)'.\n\nReproduction Steps\nCreate an console app .net with Azure.Identity 1.13.1\nAuthenticate using Client Credential SPN for using Azure Batch:\n\nvar credential = new ClientSecretCredential(TenantId, ClientId, ClientSecret);\nvar token = await credential.GetTokenAsync(new TokenRequestContext([$\"https://batch.core.windows.net/.default\"])); <-- exception here\nEnvironment\n.net8 - docker image mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim"));

            string githubToken = config["GithubKey"];
            //string connectionString = configuration["AzureStorageConnectionString"];
            //string containerName = "markdown-files";
            string owner = "azure";
            string repo = "azure-sdk-for-net";

            //var crawlerLogger = loggerFactory.CreateLogger<GitHubCrawler>();
            //var uploaderLogger = loggerFactory.CreateLogger<BlobUploader>();

            var docs = new DocumentRetrieval(githubToken);
            var readmeFiles = await docs.GetReadmeFiles(owner, repo);

            await docs.UploadFiles(readmeFiles, "issuetest", "dotnet-documents");
        }
    }
}
