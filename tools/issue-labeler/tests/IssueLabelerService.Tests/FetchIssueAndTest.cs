// using System.Globalization;
// using System.Net.Http.Headers;
// using Azure.AI.OpenAI;
// using Azure.Identity;
// using Azure.Search.Documents.Indexes;
// using Azure.Storage.Blobs;
// using IssueLabeler.Shared;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
// using Microsoft.VisualBasic;
// using Octokit;

// namespace IssueLabelerService.Tests
// {
//     public class FetchIssueAndTestProgram
//     {
//         public static async Task Main(string[] args)
//         {

//             var configuration = new ConfigurationBuilder()
//                 .SetBasePath(Directory.GetCurrentDirectory())
//                 .AddJsonFile("appsettings.json", optional: true)
//                 .AddEnvironmentVariables()
//                 .Build();

//             using var loggerFactory = LoggerFactory.Create(builder =>
//             {
//                 builder.AddConsole();
//                 builder.SetMinimumLevel(LogLevel.Information);
//             });
//             var logger = loggerFactory.CreateLogger<FetchIssueAndTestProgram>();

//             var issueNumber = 162;
//             var owner = "microsoft";
//             var repo = "mcp";

//             var issue = await FetchGitHubIssue(owner, repo, issueNumber); 

//             var issuePayload = new IssuePayload
//             {
//                 IssueNumber = issue.Number,
//                 Title = issue.Title,
//                 Body = issue.Body ?? string.Empty,
//                 RepositoryName = repo,
//                 RepositoryOwnerName = owner
//             };

//             logger.LogInformation("Issue Title: {Title}", issuePayload.Title);
//             logger.LogInformation("Issue Body: {Body}", issuePayload.Body?.Substring(0, Math.Min(100, issuePayload.Body.Length)) + "...");

//             logger.LogInformation("Initializing MCP labeler...");
//             var labeler = CreateLabeler(configuration, loggerFactory);

//             if(labeler == null)
//             {
//                 logger.LogError("Failed to initialize labeler. Check configuration.");
//                 return;
//             }

//             var labels = await labeler.PredictLabels(issuePayload);

//             Console.WriteLine("\n" + new string('=', 60));
//             Console.WriteLine($"Issue #{issueNumber}: {issuePayload.Title}");
//             Console.WriteLine(new string('=', 60));
//             Console.WriteLine($"Predicted Server: {labels.GetValueOrDefault("Server", "N/A")}");
//             Console.WriteLine($"Predicted Tool: {labels.GetValueOrDefault("Tool", "N/A")}");
//             Console.WriteLine(new string('=', 60));
//         }

//         static async Task<Issue> FetchGitHubIssue(string owner, string repo, int issueNumber)
//         {
//             var client = new GitHubClient(new Octokit.ProductHeaderValue("MCPTest"));
//             var issue = await client.Issue.Get(owner, repo, issueNumber);
//             return issue;
//         }

//         static McpOpenAiLabeler CreateLabeler(IConfiguration config, ILoggerFactory loggerFactory)
//         {
//             try
//             {
//                 var repository = "microsoft/mcp";
//                 var openAiEndpoint = config["OpenAIEndpoint"];
//                 var searchEndpoint = config["SearchServiceEndpoint"];
//                 var blobAccountUri = config["BlobAccountUri"];

//                 if (string.IsNullOrEmpty(openAiEndpoint) || string.IsNullOrEmpty(searchEndpoint))
//                 {
//                     Console.WriteLine("ERROR: Missing required configuration");
//                     return null;
//                 }

//                 var credential = new DefaultAzureCredential();
//                 var blobClient = new BlobServiceClient(new Uri(blobAccountUri), credential);
//                 var searchIndexClient = new SearchIndexClient(new Uri(searchEndpoint), credential);
//                 var openAIClient = new AzureOpenAIClient(new Uri(openAiEndpoint), credential);

//                 var triageRagLogger = loggerFactory.CreateLogger<TriageRag>();
//                 var triageRag = new TriageRag(triageRagLogger, openAIClient, searchIndexClient);
//                 var configWrapper = new IssueLabelerService.Configuration(config);
//                 var repoConfig = configWrapper.GetForRepository(repository);

//                 var labelerLogger = loggerFactory.CreateLogger<LabelerFactory>();
//                 var labeler = new McpOpenAiLabeler(labelerLogger, repoConfig, triageRag, blobClient);

//                 return labeler;
//             }
//             catch (Exception e)
//             {
//                 Console.WriteLine($"ERROR creating labeler: {e.Message}");
//                 return null;
//             }
//         }
        
//     }
// }