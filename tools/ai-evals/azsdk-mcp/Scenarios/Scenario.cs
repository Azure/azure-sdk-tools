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
            s_chatClient = TestSetup.GetChatClient();
            s_mcpClient = await TestSetup.GetMcpClientAsync();
            s_chatConfig = new ChatConfiguration(s_chatClient);
            s_chatCompletion = TestSetup.GetChatCompletion(s_chatClient, s_mcpClient);
            s_toolNames = (await s_mcpClient.ListToolsAsync()).Select(tool => tool.Name)!;
            s_executionName = $"{DateTime.Now:yyyyMMddTHHmmss}";
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
