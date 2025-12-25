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
        public async Task Evaluate_CheckPublicRepoThenValidate()
        {
            const string prompt =
                "Confirm the TypeSpec project is in the public repo, then run TypeSpec validation. " +
                "Project path: C\\:\\azure-rest-api-specs\\specification\\contosowidgetmanager\\Contoso.WidgetManager. " +
                "My setup has already been verified, do not run azsdk_verify_setup.";

            string[] expectedTools =
            [
                "azsdk_typespec_check_project_in_public_repo",
                "azsdk_run_typespec_validation"
            ];

            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);
            bool checkInputs = true;

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
