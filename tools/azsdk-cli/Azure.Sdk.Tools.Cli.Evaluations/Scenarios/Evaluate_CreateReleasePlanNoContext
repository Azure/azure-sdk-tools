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
        public async Task Evaluate_CreateReleasePlanNoContext()
        {
            // randomized service/product id
            const string prompt = "Create a release plan for the Contoso Widget Manager, no need to get it afterwords only create.";
            string[] expectedTools =
            [
                "azsdk_create_release_plan"
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
