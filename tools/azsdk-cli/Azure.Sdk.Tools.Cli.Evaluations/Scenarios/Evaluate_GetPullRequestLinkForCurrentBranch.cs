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
            const string prompt = "What's the status of the spec PR in my current branch? Only check the status once. Path to my repository root: c:\\azure-rest-api-specs";
            string[] expectedTools =
            [
                "azsdk_get_pull_request_link_for_current_branch",
            ];

            // Build scenario data
            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);

            // External construction of evaluation context
            bool checkInputs = false;

            var result = await EvaluationHelper.RunScenarioAsync(
                scenarioName: this.ScenarioName,
                scenarioData: scenarioData,
                chatCompletion: s_chatCompletion!,
                chatConfig: s_chatConfig!,
                executionName: s_executionName,
                reportingPath: ReportingPath,
                toolNames: s_toolNames,
                evaluators: [new ExpectedToolInputEvaluator()],
                enableResponseCaching: true,
                additionalContexts: new EvaluationContext[]
                {
                    new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames!, checkInputs)
                });

            EvaluationHelper.ValidateToolInputsEvaluator(result);
        }
    }
}
