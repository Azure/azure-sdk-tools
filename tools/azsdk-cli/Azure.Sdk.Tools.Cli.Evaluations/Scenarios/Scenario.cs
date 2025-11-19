using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Html;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    [TestFixture]
    public partial class Scenario
    {
        // Static services shared across all tests
        protected static IChatClient? s_chatClient;
        protected static IMcpClient? s_mcpClient;
        protected static ChatCompletion? s_chatCompletion;
        protected static IEnumerable<string>? s_toolNames;
        protected static ChatConfiguration? s_chatConfig;
        private static readonly string s_executionName = $"{DateTime.Now:yyyyMMddTHHmmss}";
        private string ScenarioName => $"{TestContext.CurrentContext.Test.ClassName}.{TestContext.CurrentContext.Test.Name}";
        private string ReportingPath => Path.Combine(TestContext.CurrentContext.TestDirectory, "reports");


        [OneTimeSetUp]
        public async Task GlobalSetup()
        {
            s_chatClient = TestSetup.GetChatClient();
            s_mcpClient = await TestSetup.GetMcpClientAsync();
            s_chatConfig = new ChatConfiguration(s_chatClient);
            s_chatCompletion = TestSetup.GetChatCompletion(s_chatClient, s_mcpClient);
            s_toolNames = (await s_mcpClient.ListToolsAsync()).Select(tool => tool.Name)!;
            TestSetup.ValidateCopilotEnvironmentConfiguration();
        }

        [SetUp]
        public void CheckRepositoryCategory()
        {
            var repositoryName = TestSetup.RepositoryName;
            if (string.IsNullOrEmpty(repositoryName))
                return;

            // Extract repo name from "Owner/Repo" format and normalize
            var repoName = repositoryName.Split('/').Last().ToLowerInvariant();

            // Get test categories
            var categories = TestContext.CurrentContext.Test.Properties["Category"]
                .Cast<string>()
                .Select(c => c.ToLowerInvariant())
                .ToList();

            // No categories means test runs everywhere
            if (!categories.Any())
                return;

            // Skip if repository doesn't match any category
            if (!categories.Contains(repoName))
            {
                Assert.Ignore($"Skipping test. Indicated repos are [{string.Join(", ", categories)}] but currently in '{repoName}'.");
            }
        }


        [OneTimeTearDown]
        public async Task GlobalTearDown()
        {
            // Generate a HTML report for all the evaluations run
            var resultStore = new DiskBasedResultStore(ReportingPath);
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
            var reportWriter = new HtmlReportWriter(reportFilePath);
            await reportWriter.WriteReportAsync(allResults);
        }
    }
}
