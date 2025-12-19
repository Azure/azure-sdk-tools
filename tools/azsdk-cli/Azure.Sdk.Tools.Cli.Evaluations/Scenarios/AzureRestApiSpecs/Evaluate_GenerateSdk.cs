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
        public async Task Evaluate_GenerateSdk()
        {
            const string prompt = "Do every step necessary to generate my SDK for Dotnet. Proceed and don't ask me questions. Stop before running tests on the SDK. I'm in a public repo. My tspconfig is at: \"C:\\azure-rest-api-specs\\specification\\healthdataaiservices\\HealthDataAIServices.DeidServices\\tspconfig.yaml\", and the repo: \"C:\\azure-sdk-for-net\"";
            string[] expectedTools =
            [
                "azsdk_verify_setup", "azsdk_run_typespec_validation", "azsdk_package_generate_code","azsdk_package_build_code"
            ];

            // Build scenario data
            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);

            // External construction of evaluation context
            bool checkInputs = false;

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
