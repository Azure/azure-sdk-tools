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
            const string prompt = "My typespec changes are already validated lets check if the current branch has an open pull request and get the link to it. Path to my repository root: C:\\Users\\juanospina\\source\\repos\\azure-rest-api-specs";
            string[] expectedTools =
            [
                "azsdk_get_pull_request_link_for_current_branch",
            ];

            // Build scenario data
            var scenarioData = await ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);
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
