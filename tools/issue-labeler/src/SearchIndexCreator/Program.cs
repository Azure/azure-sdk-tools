// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Octokit;
using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using AzureRAGService;
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
            Console.WriteLine("5. Test RAG");

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
                    case "5":
                        await TestRAG(config);
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


        //Retrieve documents from GitHub, upload to Azure Blob Storage, and create an Azure Search Index
        private static async Task ProcessDocs(IConfigurationSection config)
        {
            // 1. Retrieve all documents from a repository
            var docsRetrieval = new DocumentRetrieval(config["GithubKey"]);
            var readmeFiles = await docsRetrieval.GetDocuments("Azure", "azure-sdk-for-net");

            // 2. Upload the documents to Azure Blob Storage
            await docsRetrieval.UploadFiles(readmeFiles, config["DocumentStorageName"], config["DocumentBlobContainerName"]);

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
            var issues = await issueRetrieval.RetrieveIssueExamples("Azure", "azure-sdk-for-net", 7);
            issueRetrieval.DownloadIssue(issues);
        }


        //Uploads recent issues to my private repo for demo purposes.
        private static async Task ProcessDemo(IConfigurationSection config)
        {
            int days = 14;

            // Retrieve examples of issues for testing from a repository
            var issueRetrieval = new IssueRetrieval(new Credentials(config["GithubKey"]), config);
            var issues = await issueRetrieval.RetrieveIssueExamples("Azure", "azure-sdk-for-net", days);

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


        //Just to mess with my RAG methods in different ways
        private static async Task TestRAG(IConfigurationSection config)
        {
            // Configurations for correct access in search and OpenAI
            var credential = new DefaultAzureCredential();
            var searchEndpoint = new Uri(config["SearchEndpoint"]);
            var openAIEndpoint = new Uri(config["OpenAIEndpoint"]);
            string modelName = config["OpenAIModelName"];

            // Configuration for Issue specifics
            string issueIndexName = config["IssueIndexName"];
            string issueSemanticName = config["IssueSemanticName"];
            string issueFieldName = "text_vector";

            // Configuration for Document specifics
            string documentIndexName = config["DocumentIndexName"];
            string documentSemanticName = config["DocumentSemanticName"];
            string documentFieldName = "text_vector";

            // Search prompt
            string searchPrompt = "Azure.AI.OpenAI";

            // Initialize the RAG service
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<TriageRAG>();
            var ragService = new TriageRAG(logger);

            // Top X documents/issues
            int top = 5;

            var relevantIssues = ragService.AzureSearchQuery<AzureRAGService.Issue>(
                searchEndpoint, issueIndexName, issueSemanticName, issueFieldName, credential, searchPrompt, top
            );

            var relevantDocuments = ragService.AzureSearchQuery<Document>(
                searchEndpoint, documentIndexName, documentSemanticName, documentFieldName, credential, searchPrompt, top
            );

            var docs = relevantDocuments.Select(rd => new
            {
                Content = rd.Item1.ToString(),
                Score = rd.Item2
            });

            var issues = relevantIssues.Select(ri => new
            {
                Content = ri.Item1.ToString(),
                Score = ri.Item2
            });

            string docContent = JsonConvert.SerializeObject(docs);
            string issueContent = JsonConvert.SerializeObject(issues);

            string message = $"You are an AI assistant designed to generate realistic GitHub issues relevant to the documentation.\nDocumentation: {docContent}\nExample Issues: {issueContent}\nPlease generate potential questions that could be asked about the documentation as GitHub issue queries. Do not provide a response to the questions, only provide a list of detailed questions. Use the example issues on guidance on how to make an issue.";

            string result = ragService.SendMessageQna(openAIEndpoint, credential, modelName, message);

            // Replace escaped newlines
            result = result.Replace("\\n", "\n");

            Console.WriteLine($"Open AI Response:\n{result}");
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
