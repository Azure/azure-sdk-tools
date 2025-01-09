using Azure.Search.Documents;
using Azure;
using Microsoft.Extensions.Configuration;
using Octokit;
using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;

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

            bool issuesBool = true; //Retrieve all issues from a repository + Azure CosmosDB + Azure Search Index
            bool docsBool = false; //Retrieve all documents from a repository + Azure Blob Storage + Azure Search Index
            bool issueExamples = false; // Issue Examples for testing

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
        }
    }
}
