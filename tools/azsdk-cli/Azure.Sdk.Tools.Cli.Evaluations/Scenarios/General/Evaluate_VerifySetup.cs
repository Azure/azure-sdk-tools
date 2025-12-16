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
        public async Task Evaluate_VerifySetup()
        {
            const string prompt = "Verify my setup.";
            string[] expectedTools =
            [
                "azsdk_verify_setup"
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
                toolNames: s_toolNames!,
                evaluators: [new ExpectedToolInputEvaluator()],
                enableResponseCaching: true,
                additionalContexts: new EvaluationContext[]
                {
                    new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames!, checkInputs)
                });

            EvaluationHelper.ValidateToolInputsEvaluator(result);
        }
        
        // [Test]
        // public async Task Evaluate_VerifySetupBeforeGenerateSDK()
        // {
        //     const string prompt = "Generate my SDK.";
        //     string[] expectedTools =
        //     [
        //         "azsdk_verify_setup", "azsdk_generate_sdk"
        //     ];

        //     // Build scenario data
        //     var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);

        //     // External construction of evaluation context
        //     bool checkInputs = false;

        //     var result = await EvaluationHelper.RunScenarioAsync(
        //         scenarioName: this.ScenarioName,
        //         scenarioData: scenarioData,
        //         chatCompletion: s_chatCompletion!,
        //         chatConfig: s_chatConfig!,
        //         executionName: s_executionName,
        //         reportingPath: ReportingPath,
        //         toolNames: s_toolNames!,
        //         evaluators: [new ExpectedToolInputEvaluator()],
        //         enableResponseCaching: true,
        //         additionalContexts: new EvaluationContext[]
        //         {
        //             new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames!, checkInputs)
        //         });

        //     EvaluationHelper.ValidateToolInputsEvaluator(result);
        // }
    }
}
