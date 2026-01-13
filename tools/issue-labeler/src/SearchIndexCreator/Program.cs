// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Octokit;
using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using System.Text.Json;

namespace SearchIndexCreator
{
    public class SearchIndexCreator
    {
        public static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "sync-labels")
            {
                await RunLabelSync(args);
                return;
            }

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
            var repoOwner = repository?.Equals("mcp", StringComparison.OrdinalIgnoreCase) == true 
                ? "microsoft" 
                : "Azure";
            List<IssueLabeler.Shared.IssueTriageContent> issueTriageContent;
            if (repoOwner.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
            {
                issueTriageContent = await issueTriageContentRetrieval.RetrieveContentForMcp("microsoft");
            }
            else
            {
                issueTriageContent = await issueTriageContentRetrieval.RetrieveContent("Azure");
            }
            Console.WriteLine($"Retrieved {issueTriageContent.Count} search contents from the repository.");

            // 2. Upload the search contents to Azure Blob Storage
            await issueTriageContentRetrieval.UploadSearchContent(issueTriageContent);
            Console.WriteLine("Search contents uploaded to Azure Blob Storage.");

            //  3. Create an Azure Search Index
            var index = new IssueTriageContentIndex(config);
            var credential = new AzureCliCredential();
            var openAIClient = new AzureOpenAIClient(new Uri(config["OpenAIEndpoint"]), credential);
            var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), credential);
            var indexerClient = new SearchIndexerClient(new Uri(config["SearchEndpoint"]), credential);
            await index.SetupAndRunIndexer(indexClient, indexerClient, openAIClient);
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
            var repoOwner = repo?.Equals("mcp", StringComparison.OrdinalIgnoreCase) == true 
                ? "microsoft" 
                : "Azure";

            if (repoOwner.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
            {
                await labelRetrieval.CreateOrRefreshMcpLabels(repoOwner);
            }
            else
            {
                await labelRetrieval.CreateOrRefreshLabels(repoOwner);
            }
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
            var defaultCredential = new DefaultAzureCredential();
            var indexClient = new SearchIndexClient(new Uri(config["SearchEndpoint"]), defaultCredential);
            var issueKnowledgeAgent = new IssueKnowledgeAgent(indexClient, config);
            await issueKnowledgeAgent.DeleteAsync();
        }

        private static async Task RunLabelSync(string[] args)
        {
            Console.WriteLine("Starting label sync in CLI mode...");

            // Parse arguments: --repo-owner <owner> --repo-name <name>
            string? repoOwner = null;
            string? repoName = null;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--repo-owner" && i + 1 < args.Length)
                    repoOwner = args[++i];
                else if (args[i] == "--repo-name" && i + 1 < args.Length)
                    repoName = args[++i];
            }

            if (string.IsNullOrEmpty(repoOwner))
            {
                Console.WriteLine("Error: --repo-owner is required");
                Environment.Exit(1);
            }

            if (string.IsNullOrEmpty(repoName))
            {
                Console.WriteLine("Error: --repo-name is required");
                Environment.Exit(1);
            }

            var configDict = new Dictionary<string, string?>
            {
                ["GithubKey"] = Environment.GetEnvironmentVariable("GithubKey"),
                ["IssueStorageName"] = Environment.GetEnvironmentVariable("IssueStorageName"),
                ["RepositoryNamesForLabels"] = repoName,
                ["McpRepositoryForLabels"] = repoName
            };

            if (string.IsNullOrEmpty(configDict["GithubKey"]))
            {
                Console.WriteLine("Error: GithubKey environment variable is required");
                Environment.Exit(1);
            }

            if (string.IsNullOrEmpty(configDict["IssueStorageName"]))
            {
                Console.WriteLine("Error: IssueStorageName environment variable is required");
                Environment.Exit(1);
            }

            Console.WriteLine($"Syncing labels for repository: {repoOwner}/{repoName}");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict)
                .Build();

            try
            {
                var tokenAuth = new Credentials(configDict["GithubKey"]);
                var labelRetrieval = new LabelRetrieval(tokenAuth, config);
                
                if (repoOwner.Equals("microsoft", StringComparison.OrdinalIgnoreCase) && 
                    repoName.Equals("mcp", StringComparison.OrdinalIgnoreCase))
                {
                    await labelRetrieval.CreateOrRefreshMcpLabels(repoOwner);
                }
                else
                {
                    await labelRetrieval.CreateOrRefreshLabels(repoOwner);
                }
                
                Console.WriteLine("Label sync completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during label sync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
