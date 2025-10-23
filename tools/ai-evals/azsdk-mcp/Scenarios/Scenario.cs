using AwesomeAssertions.Specialized;
using Azure.Sdk.Tools.McpEvals.Evaluators;
using Azure.Sdk.Tools.McpEvals.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Html;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace Azure.Sdk.Tools.McpEvals.Scenarios
{
    [TestFixture]
    public partial class Scenario
    {
        // Static services shared across all tests
        protected static IChatClient? s_chatClient;
        protected static IMcpClient? s_mcpClient;
        protected static ChatCompletion? s_chatCompletion;
        protected static IEnumerable<string> s_toolNames;
        protected static ReportingConfiguration s_reportingConfiguration;
        protected static ChatConfiguration s_chatConfig;
        private static string s_executionName;
        private string ScenarioName => $"{TestContext.CurrentContext.Test.ClassName}.{TestContext.CurrentContext.Test.Name}";
        private string ReportingPath => Path.Combine(TestContext.CurrentContext.TestDirectory, "reports");


        [OneTimeSetUp]
        public async Task GlobalSetup()
        {
            // Initialize logger first
            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            s_logger = loggerFactory.CreateLogger<Scenario>();

            s_logger.LogInformation("Starting GlobalSetup...");
                
                s_logger.LogDebug("Getting chat client...");
                s_chatClient = TestSetup.GetChatClient(loggerFactory);
                s_logger.LogDebug("Chat client obtained successfully");
                
                s_logger.LogDebug("Getting MCP client async...");
                s_mcpClient = await TestSetup.GetMcpClientAsync(loggerFactory);
                s_logger.LogDebug("MCP client obtained successfully");
                
                s_logger.LogDebug("Creating chat configuration...");
                s_chatConfig = new ChatConfiguration(s_chatClient);
                s_logger.LogDebug("Chat configuration created successfully");
                
                s_logger.LogDebug("Getting chat completion...");
                s_chatCompletion = TestSetup.GetChatCompletion(s_chatClient, s_mcpClient);
                s_logger.LogDebug("Chat completion obtained successfully");
                
                s_logger.LogDebug("Listing tools from MCP client...");
                s_toolNames = (await s_mcpClient.ListToolsAsync()).Select(tool => tool.Name)!;
                s_logger.LogDebug($"Tools listed successfully. Found {s_toolNames.Count()} tools: {string.Join(", ", s_toolNames)}");
                
                s_logger.LogDebug("Creating execution name...");
                s_executionName = $"{DateTime.Now:yyyyMMddTHHmmss}";
                s_logger.LogDebug($"Execution name created: {s_executionName}");
                
                s_logger.LogInformation("GlobalSetup completed successfully");
        }


        [OneTimeTearDown]
        public async Task GlobalTearDown()
        {
            // Generate a HTML report for all the evaluations run
            IEvaluationResultStore resultStore = new DiskBasedResultStore(ReportingPath);
            var allResults = new List<ScenarioRunResult>();

            await foreach (string executionName in resultStore.GetLatestExecutionNamesAsync(count: 1))
            {
                await foreach (ScenarioRunResult scenarioResult in resultStore.ReadResultsAsync(executionName))
                {
                    allResults.Add(scenarioResult);
                }
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportFilePath = Path.Combine(ReportingPath, $"report-{timestamp}.html");
            IEvaluationReportWriter reportWriter = new HtmlReportWriter(reportFilePath);
            await reportWriter.WriteReportAsync(allResults);
        }
    }
}
