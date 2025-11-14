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
        public async Task Evaluate_CreateReleasePlan()
        {
            // randomized service/product id
            const string prompt = "Create a release plan for the Contoso Widget Manager, no need to get it afterwards only create. Do not verify my setup, I've already verified it. Here is all the context you need: TypeSpec project located at \"c:\\Users\\juanospina\\source\\repos\\azure-rest-api-specs\\specification\\contosowidgetmanager\\Contoso.WidgetManager\". Use service tree ID \"a7f2b8e4-9c1d-4a3e-b6f9-2d8e5a7c3b1f\", product tree ID \"f1a8c5d2-6e4b-4f7a-9c2d-8b5e1f3a6c9e\", target release timeline \"December 2025\", API version \"2022-11-01-preview\", SDK release type \"beta\", and link it to the spec pull request \"https://github.com/Azure/azure-rest-api-specs/pull/38387\".";
            string[] expectedTools =
            [
                "azsdk_create_release_plan"
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
