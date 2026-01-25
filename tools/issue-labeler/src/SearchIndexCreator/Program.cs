// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Octokit;
using OpenAI;

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

            Console.WriteLine("Select an option:");
            Console.WriteLine("1. Process Search Content");
            Console.WriteLine("2. Process Issue Examples");
            Console.WriteLine("3. Process Demo");
            Console.WriteLine("4. Create or Refresh Labels");
            Console.WriteLine("5. Create or Update Knowledge Agent");
            Console.WriteLine("6. Delete Knowledge Agent");

            var input = Console.ReadLine();

            try
            {
                switch (input)
                {
                    case "1":
                        await ProcessSearchContent(config);
                        break;
                    case "2":
                        await ProcessIssueExamples(config);
                        break;
                    case "3":
                        await ProcessDemo(config);
                        break;
                    case "4":
                        await ProcessLabels(config);
                        break;
                    case "5":
                        await ProcessKnowledgeAgent(config);
                        break;
                    case "6":
                        await DeleteKnowledgeAgent(config);
                        break;
                    default:
                        Console.WriteLine("Invalid option selected.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task ProcessSearchContent(IConfigurationSection config)
        {
            // 1. Retrieve all issues from a repository
            var issueTriageContentRetrieval = new IssueTriageContentRetrieval(config);
            var repository = config["repo"];
            var repoOwner = GetRepositoryOwner(repository);
            var issueTriageContent = await issueTriageContentRetrieval.RetrieveContent(repoOwner);
            Console.WriteLine($"Retrieved {issueTriageContent.Count} search contents from the repository.");

            // 2. Upload the search contents to Azure Blob Storage
            await issueTriageContentRetrieval.UploadSearchContent(issueTriageContent);
            Console.WriteLine("Search contents uploaded to Azure Blob Storage.");

            //  3. Create an Azure Search Index
            var index = new IssueTriageContentIndex(config);
            var credential = new AzureCliCredential();
            var openAIClient = new OpenAIClient(
                new BearerTokenPolicy(credential, "https://cognitiveservices.azure.com/.default"),
                new OpenAIClientOptions {Endpoint = new Uri($"{config["OpenAIEndpoint"].TrimEnd('/')}/openai/v1/")});
            var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), credential);
            var indexerClient = new SearchIndexerClient(new Uri(config["SearchEndpoint"]), credential);
            await index.SetupAndRunIndexer(indexClient, indexerClient, openAIClient);
        }

        private static string GetRepositoryOwner(string repository)
        {
            return repository?.Equals("mcp", StringComparison.OrdinalIgnoreCase) == true
                ? "microsoft"
                : "Azure";
        }

        private static async Task ProcessIssueExamples(IConfigurationSection config)
        {
            // Retrieve examples of issues for testing from a repository
            var issueTriageContentRetrieval = new IssueTriageContentRetrieval(config);
            var issues = await issueTriageContentRetrieval.RetrieveIssueExamples("Azure", config["repo"], 7);
            string jsonString = JsonSerializer.Serialize(issues);
            File.WriteAllText("issues.json", jsonString);
        }


        //Uploads recent issues to my private repo for demo purposes.
        private static async Task ProcessDemo(IConfigurationSection config)
        {
            int days = 14;
            var issueTriageContentRetrieval = new IssueTriageContentRetrieval(config);
            var issues = await issueTriageContentRetrieval.RetrieveIssueExamples("Azure", config["repo"], days);
            var client = new GitHubClient(new ProductHeaderValue("Microsoft-ML-IssueBot"))
            {
                Credentials = new Credentials(config["GithubKeyPrivate"])
            };
            foreach (var issue in issues)
            {
                var newIssue = new NewIssue(issue.Title)
                {
                    Body = issue.Body
                };

                await client.Issue.Create("jeo02", "issue-examples", newIssue);
            }
        }

        private static async Task ProcessLabels(IConfigurationSection config)
        {
            var tokenAuth = new Credentials(config["GithubKey"]);
            var labelRetrieval = new LabelRetrieval(tokenAuth, config);
            var repo = config["repo"];
            var repoOwner = GetRepositoryOwner(repo);
            await labelRetrieval.CreateOrRefreshLabels(repoOwner);
        }

        private static async Task ProcessKnowledgeAgent(IConfigurationSection config)
        {
            var credential = new AzureCliCredential();
            var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), credential);
            var issueKnowledgeAgent = new IssueKnowledgeAgent(indexClient, config);
            await issueKnowledgeAgent.CreateOrUpdateAsync();
        }

        private static async Task DeleteKnowledgeAgent(IConfigurationSection config)
        {
            var defaultCredential = new AzureCliCredential();
            var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), defaultCredential);
            var issueKnowledgeAgent = new IssueKnowledgeAgent(indexClient, config);
            await issueKnowledgeAgent.DeleteAsync();
        }
    }
}
