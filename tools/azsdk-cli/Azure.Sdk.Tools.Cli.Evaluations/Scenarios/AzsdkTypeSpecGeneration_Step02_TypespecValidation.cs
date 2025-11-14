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
        public async Task AzsdkTypeSpecGeneration_Step02_TypespecValidation()
        {
            var filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "example.json");

            // Load scenario data from JSON
            var scenarioData = await SerializationHelper.LoadScenarioFromChatMessagesAsync(filePath);
            var expectedToolResults = ChatMessageHelper.GetExpectedToolsByName(scenarioData.ExpectedOutcome, s_toolNames!);

            // This test enables deeper input checking
            bool checkInputs = true;
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
