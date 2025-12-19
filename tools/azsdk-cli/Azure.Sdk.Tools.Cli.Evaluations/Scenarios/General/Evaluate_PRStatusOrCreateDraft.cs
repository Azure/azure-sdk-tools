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
        public async Task Evaluate_PRStatusOrCreateDraft()
        {
            const string prompt =
                "Check if there is an open PR for my current branch in C:\\azure-rest-api-specs. " +
                "If no PR exists, create a draft PR for the spec changes. My setup has already been verified, do not run azsdk_verify_setup.";

            string[] expectedTools =
            [
                "azsdk_get_pull_request_link_for_current_branch",
                "azsdk_create_pull_request"
            ];

            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);
            bool checkInputs = false;

            var result = await EvaluationHelper.RunScenarioAsync(
                scenarioName: this.ScenarioName,
                scenarioData: scenarioData,
                chatCompletion: s_chatCompletion!,
                chatConfig: s_chatConfig!,
                executionName: s_executionName,
                reportingPath: ReportingPath,
                toolNames: s_toolNames!,
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
