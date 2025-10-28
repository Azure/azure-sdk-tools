using AwesomeAssertions;
using Azure.Sdk.Tools.McpEvals.Evaluators;
using Azure.Sdk.Tools.McpEvals.Helpers;
using Azure.Sdk.Tools.McpEvals.Models;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using NUnit.Framework;

namespace Azure.Sdk.Tools.McpEvals.Scenarios
{
    public partial class Scenario
    {
        [Test]
        public async Task Evaluate_GetPullRequestLinkForCurrentBranch()
        {
            const string prompt = "My typespec changes are already validated lets check if the current branch has an open pull request and get the link to it. Path to my repository root: C:\\Users\\juanospina\\source\\repos\\azure-rest-api-specs";
            string[] expectedTools =
            [
                "azsdk_get_pull_request_link_for_current_branch",
            ];

            // 1. Load Scenario Data from prompt
            var scenarioData = await ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);
            var fullChat = scenarioData.ChatHistory.Append(scenarioData.NextMessage);

            // 2. Get chat response
            var expectedToolResults = ChatMessageHelper.GetExpectedToolsByName(scenarioData.ExpectedOutcome, s_toolNames);
            var response = await s_chatCompletion!.GetChatResponseWithExpectedResponseAsync(fullChat, expectedToolResults);

            // 3. Custom Evaluator to check tool inputs
            // Layers the reporting configuration on top of it for a nice html report. 
            // Could not make this static because each test will have to define what evaluators it wants to use.
            var reportingConfiguration = DiskBasedReportingConfiguration.Create(
                executionName: s_executionName,                     // Having a static execution name allows us to see all results in one report
                storageRootPath: ReportingPath,
                evaluators: [new ExpectedToolInputEvaluator()],     // In this test we only want to run the ExpectedToolInputEvaluator
                chatConfiguration: s_chatConfig,
                enableResponseCaching: true);
            await using ScenarioRun scenarioRun = await reportingConfiguration.CreateScenarioRunAsync(this.ScenarioName);

            // Pass the expected outcome through the additional context. 
            var checkInputs = false;
            var additionalContext = new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames, checkInputs);
            var result = await scenarioRun.EvaluateAsync(fullChat, response, additionalContext: [additionalContext]);

            // 4. Assert the results
            EvaluationHelper.ValidateToolInputsEvaluator(result);
        }
    }
}
