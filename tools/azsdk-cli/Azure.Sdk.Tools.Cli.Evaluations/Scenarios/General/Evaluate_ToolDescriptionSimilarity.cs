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

            // Get Azure OpenAI configuration from environment variables
            var azureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            // var embeddingDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT");

            // Create evaluator with optional configuration (will use environment variables if not provided)
            var evaluator = new ToolDescriptionSimilarityEvaluator(azureOpenAIEndpoint);

            var result = await EvaluationHelper.RunScenarioAsync(
                messages: [],
                response: new ChatResponse(),
                scenarioName: this.ScenarioName,
                chatConfig: s_chatConfig!,
                executionName: s_executionName,
                reportingPath: ReportingPath,
                evaluators: [evaluator],
                enableResponseCaching: false,
                additionalContexts: new EvaluationContext[]
                {
                    new ToolDescriptionSimilarityEvaluatorContext(tools)
                });

            EvaluationHelper.ValidateToolDescriptionSimilarityEvaluator(result);
        }
    }
}
