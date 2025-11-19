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
        [Category(RepositoryCategories.AzureRestApiSpecs)]
        public async Task Evaluate_ValidateTypespec()
        {
            const string prompt = "Validate my typespec project. It is already confirmed we are in a public repository. The path to my typespec is C:\\azure-rest-api-specs\\specification\\contosowidgetmanager\\Contoso.WidgetManager\\main.tsp.";
            string[] expectedTools =
            [
                "azsdk_verify_setup",
                "azsdk_run_typespec_validation",
            ];

            // Build scenario data from prompt
            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);

            // External contexts (no deep input checking for this one)
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
