using IssueLabeler.Shared;
using IssueLabelerService;
using IssueLabelerService.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.Storage.Blobs;

/// <summary>
/// Standalone console application for evaluating MCP labeler accuracy
/// Usage: dotnet run --project McpLabelerEvaluationRunner
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== MCP Labeler Accuracy Evaluation ===\n");

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        // Parse command line arguments
        var runFull = args.Contains("--full");
        var runReal = args.Contains("--real");
        var exportOnly = args.Contains("--export-only");
        var extractReal = args.Contains("--extract-real");
        var csvOutput = args.FirstOrDefault(a => a.StartsWith("--output="))?.Split('=')[1] 
            ?? "mcp_evaluation_results.csv";

        if (exportOnly)
        {
            logger.LogInformation("Exporting ground truth dataset to JSON...");
            McpGroundTruthDataset.ExportToJson("mcp_ground_truth.json");
            logger.LogInformation("Export complete: mcp_ground_truth.json");
            return;
        }

        if (extractReal)
        {
            logger.LogInformation("Extracting real labeled issues from Azure Search index...");
            await ExtractRealIssuesAsync(configuration, logger);
            logger.LogInformation("Extraction complete: real_mcp_issues.json");
            return;
        }

        // Initialize McpOpenAiLabeler
        logger.LogInformation("Initializing MCP labeler...");
        
        var labeler = CreateLabeler(configuration, loggerFactory);
        if (labeler == null)
        {
            logger.LogError("Failed to initialize labeler. Check configuration.");
            return;
        }

        // Get test cases
        List<McpTestCase> testCases;
        if (runReal)
        {
            logger.LogInformation("Loading real issues from real_mcp_issues.json...");
            testCases = LoadRealIssues("real_mcp_issues.json");
            logger.LogInformation("Loaded {Count} real issues", testCases.Count);
        }
        else
        {
            testCases = runFull
                ? McpGroundTruthDataset.GetTestCases()
                : McpGroundTruthDataset.GetSmokeTestCases();
        }

        logger.LogInformation("Running evaluation with {Count} test cases...\n", testCases.Count);

        // Run evaluation
        var evaluator = new McpLabelerEvaluator(labeler, logger);
        var (results, metrics) = await evaluator.EvaluateAsync(testCases, stopOnFirstError: false);

        // // Quick summary using static Evaluate method
        // var summary = McpLabelerEvaluator.Evaluate(results);
        // Console.WriteLine(summary.ToString());

        // Print detailed results
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine(metrics.ToString());
        Console.WriteLine(new string('=', 60));

        // Print failure report
        var failureReport = evaluator.GenerateFailureReport(results);
        Console.WriteLine("\n" + failureReport);

        // Export to CSV
        evaluator.ExportToCsv(results, csvOutput);
        logger.LogInformation("\nResults exported to: {Path}", csvOutput);

        // Exit code based on thresholds
        var exitCode = metrics.CombinedAccuracy >= 0.80 ? 0 : 1;
        Environment.Exit(exitCode);
    }

    static ILabeler? CreateLabeler(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        try
        {
            var logger = loggerFactory.CreateLogger<Program>();

            // Get configuration values
            var repository = "microsoft/mcp";
            var openAiEndpoint = configuration["OpenAIEndpoint"];
            var searchEndpoint = configuration["SearchServiceEndpoint"];
            var blobAccountUri = configuration["BlobAccountUri"];

            if (string.IsNullOrEmpty(openAiEndpoint) || string.IsNullOrEmpty(searchEndpoint))
            {
                Console.WriteLine("ERROR: Missing required configuration:");
                Console.WriteLine($"  OpenAIEndpoint = {openAiEndpoint ?? "NOT SET"}");
                Console.WriteLine($"  SearchServiceEndpoint = {searchEndpoint ?? "NOT SET"}");
                Console.WriteLine($"  BlobAccountUri = {blobAccountUri ?? "NOT SET"}");
                Console.WriteLine("\nAdd these to your appsettings.json or environment variables.");
                return null;
            }

            // Create Azure credential
            var credential = new DefaultAzureCredential();

            // Create BlobServiceClient
            var blobClient = new BlobServiceClient(new Uri(blobAccountUri!), credential);

            // Create SearchIndexClient
            var searchIndexClient = new SearchIndexClient(new Uri(searchEndpoint!), credential);

            // Create AzureOpenAIClient
            var openAIClient = new AzureOpenAIClient(new Uri(openAiEndpoint!), credential);

            // Create TriageRag service
            var triageRagLogger = loggerFactory.CreateLogger<TriageRag>();
            var triageRag = new TriageRag(triageRagLogger, openAIClient, searchIndexClient);

            // Create RepositoryConfiguration for microsoft/mcp
            // Use the Configuration wrapper class to create RepositoryConfiguration
            var configWrapper = new Configuration(configuration);
            var repoConfig = configWrapper.GetForRepository(repository);

            logger.LogInformation("Creating McpOpenAiLabeler with configuration:");
            logger.LogInformation("  IndexName: {IndexName}", repoConfig.IndexName);
            logger.LogInformation("  SemanticName: {SemanticName}", repoConfig.SemanticName);
            logger.LogInformation("  LabelModelName: {ModelName}", repoConfig.LabelModelName);
            logger.LogInformation("  SourceCount: {SourceCount}", repoConfig.SourceCount);

            // Create the labeler
            var labelerLogger = loggerFactory.CreateLogger<LabelerFactory>();
            var labeler = new McpOpenAiLabeler(labelerLogger, repoConfig, triageRag, blobClient);

            return labeler;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR creating labeler: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    static async Task ExtractRealIssuesAsync(IConfiguration configuration, ILogger logger)
    {
        // Get Azure Search credentials
        var searchEndpoint = configuration["SearchServiceEndpoint"] 
            ?? throw new InvalidOperationException("SearchServiceEndpoint not configured");
        var indexName = configuration["microsoft/mcp:IndexName"] 
            ?? configuration["defaults:IndexName"]
            ?? throw new InvalidOperationException("IndexName not configured");

        logger.LogInformation("Connecting to Azure Search: {Endpoint}", searchEndpoint);
        logger.LogInformation("Index: {IndexName}", indexName);

        // Create search client
        var credential = new DefaultAzureCredential();
        var searchClient = new SearchClient(new Uri(searchEndpoint), indexName, credential);

        // Query for labeled issues
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

        // Collect unique issues (skip chunked duplicates)
        var seenUrls = new HashSet<string>();
        var issues = new List<RealMcpIssue>();

        await foreach (var result in searchResults.Value.GetResultsAsync())
        {
            var doc = result.Document;
            var url = doc["Url"]?.ToString();
            
            if (url == null || seenUrls.Contains(url))
                continue;

            seenUrls.Add(url);

            var issue = new RealMcpIssue
            {
                Title = doc["Title"]?.ToString() ?? "",
                Body = doc["Chunk"]?.ToString() ?? "",
                Server = doc["Server"]?.ToString() ?? "",
                Tool = doc["Tool"]?.ToString() ?? "",
                Url = url
            };

            issues.Add(issue);

            if (issues.Count % 50 == 0)
                logger.LogInformation("Extracted {Count} issues...", issues.Count);
        }

        logger.LogInformation("Total unique issues extracted: {Count}", issues.Count);
        logger.LogInformation("Total documents in index: {Total}", searchResults.Value.TotalCount);

        // Export to JSON
        var json = System.Text.Json.JsonSerializer.Serialize(issues, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync("real_mcp_issues.json", json);
        logger.LogInformation("Saved to: real_mcp_issues.json");

        // Print summary statistics
        var serverCounts = issues.GroupBy(i => i.Server).ToDictionary(g => g.Key, g => g.Count());
        var toolCounts = issues.GroupBy(i => i.Tool).ToDictionary(g => g.Key, g => g.Count());

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
        var realIssues = System.Text.Json.JsonSerializer.Deserialize<List<RealMcpIssue>>(json)
            ?? throw new InvalidOperationException($"Failed to parse {filePath}");

        return realIssues.Select((issue, index) => new McpTestCase
        {
            IssueNumber = index + 1000, // Start from 1000 to distinguish from synthetic
            Title = issue.Title,
            Body = issue.Body,
            ExpectedServerLabel = issue.Server,
            ExpectedToolLabel = issue.Tool,
            Notes = $"Real issue: {issue.Url}"
        }).ToList();
    }

    class RealMcpIssue
    {
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string Server { get; set; } = "";
        public string Tool { get; set; } = "";
        public string Url { get; set; } = "";
    }
}
