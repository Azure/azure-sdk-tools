using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    public partial class Scenario
    {
        [Test]
        public async Task Evaluate_ToolDescriptionSimilarity()
        {
            // This test validates that MCP tool descriptions are sufficiently distinct
            // to avoid confusion for the AI agent

            // Create a simple prompt - the actual content doesn't matter much
            // since we're evaluating the tools themselves, not the agent's response
            const string prompt = "List all available tools";

            // Build scenario data
            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, []);

            // Get all tools from MCP server
            var tools = (await s_mcpClient!.ListToolsAsync()).ToList();

            var result = await EvaluationHelper.RunScenarioAsync(
                scenarioName: this.ScenarioName,
                scenarioData: scenarioData,
                chatCompletion: s_chatCompletion!,
                chatConfig: s_chatConfig!,
                executionName: s_executionName,
                reportingPath: ReportingPath,
                toolNames: s_toolNames!,
                evaluators: [new ToolDescriptionSimilarityEvaluator()],
                enableResponseCaching: false,
                additionalContexts: new EvaluationContext[]
                {
                    new ToolDescriptionSimilarityEvaluatorContext(tools)
                });

            // Validate the similarity check
            var similarityMetric = result.Get<BooleanMetric>(ToolDescriptionSimilarityEvaluator.SimilarityMetricName);
            
            // Assert that tool descriptions are distinct
            Assert.That(similarityMetric.Value, Is.True, 
                $"Tool descriptions should be sufficiently distinct to avoid agent confusion. {similarityMetric.Reason}");
            
            // Log any warnings for review even if test passes
            var warnings = similarityMetric.Diagnostics?
                .Where(d => d.Severity == EvaluationDiagnosticSeverity.Warning)
                .ToList();
            
            if (warnings?.Any() == true)
            {
                TestContext.WriteLine($"⚠️  Found {warnings.Count} tool description similarity warnings:");
                foreach (var warning in warnings)
                {
                    TestContext.WriteLine($"   {warning.Message}");
                }
            }
        }
    }
}
