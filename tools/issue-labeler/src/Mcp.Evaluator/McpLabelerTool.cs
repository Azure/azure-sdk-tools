using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using IssueLabeler.Shared;
using IssueLabelerService;
using Mcp.Evaluator.Evaluation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

/// <summary>
/// Standalone console application for MCP labeler - supports prediction and evaluation modes
/// Usage: 
///   dotnet run -- --issue=101,1422           # Predict labels for specific issues
///   dotnet run                               # Evaluate accuracy on real_mcp_issues.json
///   dotnet run -- --extract-real             # Extract labeled issues from Azure Search
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== MCP Labeler Accuracy Evaluation ===\n");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        var extractReal = args.Contains("--extract-real");
        var issueNumbers = args.Where(issue => issue.StartsWith("--issue="))
            .SelectMany(issue => issue.Split('=')[1].Split(','))
            .Select(issue => int.Parse(issue.Trim()))
            .ToList();
        var csvOutput = args.FirstOrDefault(a => a.StartsWith("--output="))?.Split('=')[1] 
            ?? "mcp_evaluation_results.csv";

        if (extractReal)
        {
            logger.LogInformation("Extracting real labeled issues from Azure Search index...");
            await ExtractRealIssuesAsync(configuration, logger);
            logger.LogInformation("Extraction complete: real_mcp_issues.json");
            return 0;
        }

        logger.LogInformation("Initializing MCP labeler");
        
        var labeler = await CreateLabeler(configuration, loggerFactory, logger);
        if (labeler == null)
        {
            logger.LogError("Failed to initialize labeler. Check configuration.");
            return 1;
        }

        List<McpTestCase> testCases;
        if (issueNumbers.Any()) 
        {
            logger.LogInformation("Fetching and predicting the labels of the specified issues: {Issues}", string.Join(", ", issueNumbers));
            var client = new GitHubClient(new ProductHeaderValue("MCPLabelerEvaluationRunner"));

            foreach (var num in issueNumbers) {
                var issue = await FetchGitHubIssue(client, "microsoft", "mcp", num);
                var issuePayload = new IssuePayload
                {
                    IssueNumber = issue.Number,
                    Title = issue.Title,
                    Body = issue.Body ?? string.Empty,
                    RepositoryName = "mcp",
                    RepositoryOwnerName = "microsoft"
                };
                var labels = await labeler.PredictLabels(issuePayload);
                Console.WriteLine($"Issue #{num}: {issue.Title}");
                Console.WriteLine($"Predicted Server: {labels.GetValueOrDefault("Server", "N/A")}");
                Console.WriteLine($"Predicted Tool: {labels.GetValueOrDefault("Tool", "N/A")}");
                Console.WriteLine($"URL: {issue.HtmlUrl}");
            }
            return 0;

        } else {
            logger.LogInformation("Loading real issues from real_mcp_issues.json...");
            testCases = LoadRealIssues("real_mcp_issues.json");
            logger.LogInformation("Loaded {Count} real issues", testCases.Count);
        }

        logger.LogInformation("Running evaluation with {Count} test cases...\n", testCases.Count);

        var evaluator = new McpLabelerEvaluator(labeler, logger);
        var (results, metrics) = await evaluator.EvaluateAsync(testCases, stopOnFirstError: false);

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine(metrics.ToString());
        Console.WriteLine(new string('=', 60));

        var failureReport = evaluator.GenerateFailureReport(results);
        Console.WriteLine("\n" + failureReport);

        evaluator.ExportToCsv(results, csvOutput);
        logger.LogInformation("\nResults exported to: {Path}", csvOutput);

        return 0;
    }

    static async Task<Issue> FetchGitHubIssue(GitHubClient client, string owner, string repo, int issueNumber)
    {
        var issue = await client.Issue.Get(owner, repo, issueNumber);
        return issue;
    }

    static Task<ILabeler?> CreateLabeler(IConfiguration configuration, ILoggerFactory loggerFactory, ILogger logger)
    {
        try
        {
            var repository = "microsoft/mcp";
            var openAiEndpoint = configuration["OpenAIEndpoint"];
            var searchEndpoint = configuration["SearchServiceEndpoint"];
            var blobAccountUri = configuration["BlobAccountUri"];

            if (string.IsNullOrEmpty(openAiEndpoint) || string.IsNullOrEmpty(searchEndpoint))
            {
                logger.LogError("Missing required configuration:");
                logger.LogError("  OpenAIEndpoint = {OpenAIEndpoint}", openAiEndpoint ?? "NOT SET");
                logger.LogError("  SearchServiceEndpoint = {SearchServiceEndpoint}", searchEndpoint ?? "NOT SET");
                logger.LogError("  BlobAccountUri = {BlobAccountUri}", blobAccountUri ?? "NOT SET");
                logger.LogError("Add these to your appsettings.json or environment variables.");
                return Task.FromResult<ILabeler?>(null);
            }

            var credential = new DefaultAzureCredential();
            var blobClient = new BlobServiceClient(new Uri(blobAccountUri!), credential);
            var searchIndexClient = new SearchIndexClient(new Uri(searchEndpoint!), credential);
            var openAIClient = new AzureOpenAIClient(new Uri(openAiEndpoint!), credential);

            var mcpTriageRagLogger = loggerFactory.CreateLogger<McpTriageRag>();
            var mcpTriageRag = new McpTriageRag(mcpTriageRagLogger, openAIClient, searchIndexClient);

            var configWrapper = new Configuration(configuration);
            var repoConfig = configWrapper.GetForRepository(repository);

            logger.LogInformation("Creating McpOpenAiLabeler with configuration:");
            logger.LogInformation("  IndexName: {IndexName}", repoConfig.IndexName);
            logger.LogInformation("  SemanticName: {SemanticName}", repoConfig.SemanticName);
            logger.LogInformation("  LabelModelName: {ModelName}", repoConfig.LabelModelName);
            logger.LogInformation("  SourceCount: {SourceCount}", repoConfig.SourceCount);

            var labelerLogger = loggerFactory.CreateLogger<LabelerFactory>();
            var labeler = new McpOpenAiLabeler(labelerLogger, repoConfig, mcpTriageRag, blobClient);

            return Task.FromResult<ILabeler?>(labeler);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating labeler: {Message}", ex.Message);
            return Task.FromResult<ILabeler?>(null);
        }
    }

    static async Task ExtractRealIssuesAsync(IConfiguration configuration, ILogger logger)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            logger.LogWarning("Extraction cancelled by user.");
        };
        var searchEndpoint = configuration["SearchServiceEndpoint"] 
            ?? throw new InvalidOperationException("SearchServiceEndpoint not configured");
        var indexName = configuration["microsoft/mcp:IndexName"] 
            ?? configuration["defaults:IndexName"]
            ?? throw new InvalidOperationException("IndexName not configured");

        logger.LogInformation("Connecting to Azure Search: {Endpoint}", searchEndpoint);
        logger.LogInformation("Index: {IndexName}", indexName);

        var credential = new DefaultAzureCredential();
        var searchClient = new Azure.Search.Documents.SearchClient(new Uri(searchEndpoint), indexName, credential);

        var filter = "DocumentType eq 'Issue' and Server ne null and Tool ne null";
        logger.LogInformation("Filter: {Filter}", filter);

        var searchOptions = new SearchOptions
        {
            Filter = filter,
            Size = 500,
            Select = { "Title", "Chunk", "Server", "Tool", "Url" },
            IncludeTotalCount = true
        };

        logger.LogInformation("Executing search query...");
        var searchResults = await searchClient.SearchAsync<SearchDocument>("*", searchOptions);

        var seenUrls = new HashSet<string>();
        var issues = new List<McpTestCase>();

        try{
            await foreach (var result in searchResults.Value.GetResultsAsync().WithCancellation(cts.Token))
            {
                var doc = result.Document;
                var url = doc["Url"]?.ToString();
                
                if (url == null || seenUrls.Contains(url))
                    continue;

                seenUrls.Add(url);

                var issueNumber = 0;
                if (!string.IsNullOrEmpty(url))
                {
                    var urlParts = url.Split('/');
                    if (urlParts.Length > 0 && int.TryParse(urlParts[^1], out var num))
                    {
                        issueNumber = num;
                    }
                }

                var issue = new McpTestCase
                {
                    IssueNumber = issueNumber,
                    Title = doc["Title"]?.ToString() ?? "",
                    Body = doc["Chunk"]?.ToString() ?? "",
                    ExpectedServerLabel = doc["Server"]?.ToString() ?? "",
                    ExpectedToolLabel = doc["Tool"]?.ToString() ?? "",
                    Notes = $"Real issue: {url}"
                };

                issues.Add(issue);

                if (issues.Count % 50 == 0)
                    logger.LogInformation("Extracted {Count} issues...", issues.Count);
            }
        } catch (OperationCanceledException)
        {
            logger.LogWarning("Extraction cancelled before completion. Extracted {Count} issues.", issues.Count);
        }

        logger.LogInformation("Total unique issues extracted: {Count}", issues.Count);
        logger.LogInformation("Total documents in index: {Total}", searchResults.Value.TotalCount);

        var json = JsonSerializer.Serialize(issues, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync("real_mcp_issues.json", json);
        logger.LogInformation("Saved to: real_mcp_issues.json");

        var serverCounts = issues.GroupBy(i => i.ExpectedServerLabel).ToDictionary(g => g.Key, g => g.Count());
        var toolCounts = issues.GroupBy(i => i.ExpectedToolLabel).ToDictionary(g => g.Key, g => g.Count());

        Console.WriteLine("\n=== Server Distribution ===");
        foreach (var (server, count) in serverCounts.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"  {server}: {count}");

        Console.WriteLine("\n=== Tool Distribution ===");
        foreach (var (tool, count) in toolCounts.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"  {tool}: {count}");
    }

    static List<McpTestCase> LoadRealIssues(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var testCases = JsonSerializer.Deserialize<List<McpTestCase>>(json)
            ?? throw new InvalidOperationException($"Failed to parse {filePath}");

        return testCases;
    }
}
