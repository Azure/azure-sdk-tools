using Microsoft.Extensions.Configuration;
using Octokit;
using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using System.Net.Http.Json;

namespace SearchIndexCreator
{
    public class SearchIndexCreator
    {
        public static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json")
                .Build();
            var config = configuration.GetSection("Values");

            bool issuesBool = false; //Retrieve all issues from a repository + Azure CosmosDB + Azure Search Index
            bool docsBool = false; //Retrieve all documents from a repository + Azure Blob Storage + Azure Search Index
            bool issueExamples = false; // Issue Examples for testing
            bool demo = true;  //Demo for testing

            if (issuesBool)
            {
                try
                {
                    // 1. Retrieve all issues from a repository
                    var tokenAuth = new Credentials(config["GithubKey"]);
                    var issueRetrieval = new IssueRetrieval(tokenAuth, config);
                    var issues = await issueRetrieval.RetrieveAllIssues("Azure", "azure-sdk-for-net");
                    
                    // 2. Upload the issues to Azure CosmosDB
                    var defaultCredential = new DefaultAzureCredential();
                    await issueRetrieval.UploadIssues(issues, config["IssueStorageName"], config["IssueBlobContainerName"]);

                    // 3. Create an Azure Search Index
                    var index = new IssueIndex(config);
                    var openAIClient = new AzureOpenAIClient(new Uri(config["OpenAIEndpoint"]), defaultCredential);
                    var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), defaultCredential);
                    var indexerClient = new SearchIndexerClient(new Uri(config["SearchEndpoint"]), defaultCredential);

                    await index.SetupAndRunIndexer(indexClient, indexerClient, openAIClient);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            else if (docsBool)
            {
                try
                {
                    // 1. Retrieve all documents from a repository
                    var docs = new DocumentRetrieval(config["GithubKey"]);
                    var readmeFiles = await docs.GetDocuments("Azure", "azure-sdk-for-net");

                    // 2. Upload the documents to Azure Blob Storage
                    await docs.UploadFiles(readmeFiles, config["DocumentStorageName"], config["DocumentBlobContainerName"]);

                    // 3. Create an Azure Search Index
                    var defaultCredential = new DefaultAzureCredential();
                    var index = new DocumentIndex(config);
                    var openAIClient = new AzureOpenAIClient(new Uri(config["OpenAIEndpoint"]), defaultCredential);
                    var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), defaultCredential);
                    var indexerClient = new SearchIndexerClient(new Uri(config["SearchEndpoint"]), defaultCredential);

                    await index.SetupAndRunIndexer(indexClient, indexerClient, openAIClient);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            else if (issueExamples)
            {
                try
                {
                    // Retrieve examples of issues for testing from a repository
                    var issueRetrieval = new IssueRetrieval(new Credentials(config["GithubKey"]), config);
                    var issues = await issueRetrieval.RetrieveIssueExamples("Azure", "azure-sdk-for-net", 7);
                    issueRetrieval.DownloadIssue(issues);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            //Get recent issues and put them into my own private repository to see it in "action"
            else if(demo)
            {
                // Retrieve examples of issues for testing from a repository
                var issueRetrieval = new IssueRetrieval(new Credentials(config["GithubKey"]), config);
                var issues = await issueRetrieval.RetrieveIssueExamples("Azure", "azure-sdk-for-net", 7);

                var client = new GitHubClient(new ProductHeaderValue("Microsoft-ML-IssueBot"));
                client.Credentials = new Credentials(config["GithubKeyPrivate"]);

                List<(int, string)> comments = new List<(int, string)>();
                // Upload recent issues into private repository
                foreach (var issue in issues)
                {
                    var createIssue = new NewIssue(issue.Title)
                    {
                        Body = issue.Body
                    };

                    var newIssue = await client.Issue.Create("jeo02", "issue-examples", createIssue);

                    string requestUrl = config["AzureFunctionLink"];

                    var payload = new
                    {
                        IssueNumber = newIssue.Number,
                        newIssue.Title,
                        newIssue.Body,
                        IssueUserLogin = newIssue.User.Login,
                        RepositoryName = "azure-sdk-for-net",
                        RepositoryOwnerName = "Azure"
                    };

                    using var httpClient = new HttpClient();

                    var response = await httpClient.PostAsJsonAsync(requestUrl, payload).ConfigureAwait(false);

                    var suggestions = await response.Content.ReadFromJsonAsync<IssueOutput>().ConfigureAwait(false);
                    comments.Add((newIssue.Number, suggestions.Suggestions));
                }

                foreach((int num, string comment) in comments)
                {
                    if(comment != "")
                    {
                        await client.Issue.Comment.Create("jeo02", "issue-examples", num, comment);
                    }
                }
            }
        }

        private class IssueOutput
        {
            public string Category { get; set; }
            public string Service { get; set; }
            public string Suggestions { get; set; }
            public bool Solution { get; set; }
        }
    }
}
