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
        public async Task Evaluate_GetModifiedTypespecProjects()
        {
            const string prompt = "List the TypeSpec projects modified in my repo. My setup has already been verified, do not run azsdk_verify_setup. Path to root: C:\\azure-rest-api-specs. Compare against main.";
            string[] expectedTools =
            [
                "azsdk_get_modified_typespec_projects"
            ];

            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);
            bool checkInputs = true;

            var result = await EvaluationHelper.RunToolInputScenarioAsync(
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
