using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace IssueLabelerService.Tests
{
    /// <summary>
    /// Integration tests for MCP labeler evaluation
    /// Requires live Azure AI Search index and OpenAI endpoint
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class McpLabelerAccuracyTests
    {
        private IConfiguration? _configuration;
        private ILogger<McpLabelerAccuracyTests>? _logger;
        private McpOpenAiLabeler? _labeler;

        [SetUp]
        public void Setup()
        {
            // Load configuration from local.settings.json or environment variables
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            // Create logger
            var loggerFactory = LoggerFactory.Create(logBuilder =>
            {
                logBuilder.AddConsole();
                logBuilder.SetMinimumLevel(LogLevel.Information);
            });
            _logger = loggerFactory.CreateLogger<McpLabelerAccuracyTests>();

            // Initialize labeler (this would need actual dependencies)
            // For now, this is a placeholder - you'd need to properly construct McpOpenAiLabeler
            // with real Azure Search client, OpenAI client, etc.
            //_labeler = new McpOpenAiLabeler(...);
        }

        [Test]
        [Explicit("Requires live Azure resources")]
        public async Task EvaluateFullDataset()
        {
            Assert.That(_labeler, Is.Not.Null, "Labeler must be initialized");

            var testCases = McpGroundTruthDataset.GetTestCases();
            var evaluator = new McpLabelerEvaluator(_labeler!, _logger!);

            var (results, metrics) = await evaluator.EvaluateAsync(testCases, stopOnFirstError: false);

            // Print metrics
            _logger!.LogInformation(metrics.ToString());

            // Print failure report
            var failureReport = evaluator.GenerateFailureReport(results);
            _logger.LogInformation(failureReport);

            // Export to CSV
            var csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "mcp_evaluation_results.csv");
            evaluator.ExportToCsv(results, csvPath);

            // Assertions - adjust thresholds based on your requirements
            Assert.That(metrics.SuccessRate, Is.GreaterThanOrEqualTo(0.95), 
                "Success rate should be at least 95%");
            
            Assert.That(metrics.ServerAccuracy, Is.GreaterThanOrEqualTo(0.90), 
                "Server label accuracy should be at least 90%");
            
            Assert.That(metrics.ToolAccuracy, Is.GreaterThanOrEqualTo(0.85), 
                "Tool label accuracy should be at least 85%");
            
            Assert.That(metrics.CombinedAccuracy, Is.GreaterThanOrEqualTo(0.80), 
                "Combined accuracy (both labels correct) should be at least 80%");
        }

        [Test]
        [Explicit("Requires live Azure resources")]
        public async Task EvaluateSmokeTests()
        {
            Assert.That(_labeler, Is.Not.Null, "Labeler must be initialized");

            var testCases = McpGroundTruthDataset.GetSmokeTestCases();
            var evaluator = new McpLabelerEvaluator(_labeler!, _logger!);

            var (results, metrics) = await evaluator.EvaluateAsync(testCases, stopOnFirstError: false);

            _logger!.LogInformation(metrics.ToString());

            // For smoke tests, expect 100% accuracy
            Assert.That(metrics.BothCorrect, Is.EqualTo(testCases.Count), 
                "All smoke test cases should be predicted correctly");
        }

        [Test]
        public void VerifyDatasetCoverage()
        {
            var testCases = McpGroundTruthDataset.GetTestCases();

            // Verify we have good coverage of different tools
            var toolDistribution = testCases
                .GroupBy(tc => tc.ExpectedToolLabel)
                .Select(g => new { Tool = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            _logger!.LogInformation("Tool Label Distribution:");
            foreach (var item in toolDistribution)
            {
                _logger.LogInformation("  {Tool}: {Count}", item.Tool, item.Count);
            }

            // Should have at least 5 different tools represented
            Assert.That(toolDistribution.Count, Is.GreaterThanOrEqualTo(5), 
                "Dataset should cover at least 5 different tools");

            // Should have some UNKNOWN cases (general server issues)
            var unknownCount = testCases.Count(tc => tc.ExpectedToolLabel == "UNKNOWN");
            Assert.That(unknownCount, Is.GreaterThan(0), 
                "Dataset should include cases with UNKNOWN tool labels");
        }

        [Test]
        public void ExportDatasetToJson()
        {
            var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "mcp_ground_truth.json");
            McpGroundTruthDataset.ExportToJson(outputPath);

            Assert.That(File.Exists(outputPath), Is.True);
            _logger!.LogInformation("Ground truth dataset exported to: {Path}", outputPath);
        }
    }
}
