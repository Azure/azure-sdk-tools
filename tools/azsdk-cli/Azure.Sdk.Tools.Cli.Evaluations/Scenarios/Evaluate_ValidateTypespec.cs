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
        public async Task Evaluate_ValidateTypespec()
        {
            const string prompt = "Validate my typespec project. The path to my typespec is C:\\Users\\juanospina\\source\\repos\\azure-rest-api-specs\\specification\\contosowidgetmanager\\Contoso.WidgetManager\\main.tsp.";
            string[] expectedTools =
            [
                "azsdk_run_typespec_validation",
            ];

            // Build scenario data from prompt
            var scenarioData = await ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);
            var expectedToolResults = ChatMessageHelper.GetExpectedToolsByName(scenarioData.ExpectedOutcome, s_toolNames!);

            // External contexts (no deep input checking for this one)
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
