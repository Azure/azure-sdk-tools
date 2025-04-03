// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Octokit;
using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using AzureRagService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
            Console.WriteLine("1. Process Issues");
            Console.WriteLine("2. Process Docs");
            Console.WriteLine("3. Process Issue Examples");
            Console.WriteLine("4. Process Demo");

            var input = Console.ReadLine();

            try
            {
                switch (input)
                {
                    case "1":
                        await ProcessIssues(config);
                        break;
                    case "2":
                        await ProcessDocs(config);
                        break;
                    case "3":
                        await ProcessIssueExamples(config);
                        break;
                    case "4":
                        await ProcessDemo(config);
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


        // Retrieve issues from GitHub, upload to Azure Blob Storage, and create an Azure Search Index
        private static async Task ProcessIssues(IConfigurationSection config)
        {
            // 1. Retrieve all issues from a repository
            var tokenAuth = new Credentials(config["GithubKey"]);
            var issueRetrieval = new IssueRetrieval(tokenAuth, config);
            var issues = await issueRetrieval.RetrieveAllIssues("Azure", config["repo"]);

            // 2. Upload the issues to Azure CosmosDB
            var defaultCredential = new DefaultAzureCredential();
            await issueRetrieval.UploadIssues(issues, config["IssueStorageName"], $"{config["IssueIndexName"]}-blob");

            // 3. Create an Azure Search Index
            var index = new IssueIndex(config);
            var openAIClient = new AzureOpenAIClient(new Uri(config["OpenAIEndpoint"]), defaultCredential);
            var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), defaultCredential);
            var indexerClient = new SearchIndexerClient(new Uri(config["SearchEndpoint"]), defaultCredential);

            await index.SetupAndRunIndexer(indexClient, indexerClient, openAIClient);
        }


        //Retrieve documents from GitHub, upload to Azure Blob Storage, and create an Azure Search Index
        private static async Task ProcessDocs(IConfigurationSection config)
        {
            // 1. Retrieve all documents from a repository
            var docsRetrieval = new DocumentRetrieval(config["GithubKey"]);
            var readmeFiles = await docsRetrieval.GetDocuments("Azure", config["repo"]);

            // 2. Upload the documents to Azure Blob Storage
            await docsRetrieval.UploadFiles(readmeFiles, config["DocumentStorageName"], $"{config["DocumentIndexName"]}-blob");

            // 3. Create an Azure Search Index
            var defaultCredential = new DefaultAzureCredential();
            var index = new DocumentIndex(config);
            var openAIClient = new AzureOpenAIClient(new Uri(config["OpenAIEndpoint"]), defaultCredential);
            var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), defaultCredential);
            var indexerClient = new SearchIndexerClient(new Uri(config["SearchEndpoint"]), defaultCredential);

            await index.SetupAndRunIndexer(indexClient, indexerClient, openAIClient);
        }


        private static async Task ProcessIssueExamples(IConfigurationSection config)
        {
            // Retrieve examples of issues for testing from a repository
            var issueRetrieval = new IssueRetrieval(new Credentials(config["GithubKey"]), config);
            var issues = await issueRetrieval.RetrieveIssueExamples("Azure", config["repo"], 7);
            issueRetrieval.DownloadIssue(issues);
        }


        //Uploads recent issues to my private repo for demo purposes.
        private static async Task ProcessDemo(IConfigurationSection config)
        {
            int days = 14;

            // Retrieve examples of issues for testing from a repository
            var issueRetrieval = new IssueRetrieval(new Credentials(config["GithubKey"]), config);
            var issues = await issueRetrieval.RetrieveIssueExamples("Azure", config["repo"], days);

            var client = new GitHubClient(new ProductHeaderValue("Microsoft-ML-IssueBot"))
            {
                Credentials = new Credentials(config["GithubKeyPrivate"])
            };

            // Upload recent issues into private repository
            foreach (var issue in issues)
            {
                var newIssue = new NewIssue(issue.Title)
                {
                    Body = issue.Body
                };

                await client.Issue.Create("jeo02", "issue-examples", newIssue);
            }
        }
    }
}
