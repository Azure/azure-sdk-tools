using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI.Evaluation;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    public partial class Scenario
    {
        [Test]
        public async Task Evaluate_GetPullRequestLinkForCurrentBranch()
        {
            const string prompt = "What's the status of the spec PR in my current branch? Only check the status once. Path to my repository root: C:\\Users\\juanospina\\source\\repos\\azure-rest-api-specs";
            string[] expectedTools =
            [
                "azsdk_get_pull_request_link_for_current_branch",
            ];

            // Build scenario data
            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);
            var expectedToolResults = ChatMessageHelper.GetExpectedToolsByName(scenarioData.ExpectedOutcome, s_toolNames!);

            // External construction of evaluation context
            bool checkInputs = false;
            var additionalContexts = new EvaluationContext[]
            {
                new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames!, checkInputs)
            };

            var result = await EvaluationHelper.RunScenarioAsync(
                scenarioName: this.ScenarioName,
                scenarioData: scenarioData,
                expectedToolResults: expectedToolResults,
                chatCompletion: s_chatCompletion!,
                chatConfig: s_chatConfig!,
                executionName: s_executionName,
                reportingPath: ReportingPath,
                evaluators: [new ExpectedToolInputEvaluator()],
                enableResponseCaching: true,
                additionalContexts: additionalContexts);

            EvaluationHelper.ValidateToolInputsEvaluator(result);
        }
    }
}
