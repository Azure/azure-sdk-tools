using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI.Evaluation;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    /// <summary>
    /// Evaluation scenarios for server instructions related to pipeline analysis.
    /// Tests: "When analyzing Azure Pipeline failures or CI/CD issues, use azsdk pipeline analysis tools"
    /// </summary>
    public partial class Scenario
    {
        /// <summary>
        /// Tests pipeline failure analysis scenario
        /// Expected: Agent should use azsdk pipeline tools to analyze build failures
        /// </summary>
        [Test]
        public async Task Evaluate_ServerInstructions_PipelineAnalysis_GetStatus()
        {
            const string prompt = "Check the status of my Azure pipeline build 12345678";
            
            string[] expectedTools =
            [
                "azsdk_get_pipeline_status"
            ];

            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);

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
                    new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames!, checkInputs: false)
                });

            EvaluationHelper.ValidateToolInputsEvaluator(result);
        }

        /// <summary>
        /// Tests CI/CD failure analysis - getting test results
        /// Expected: Agent should use azsdk tools to get failed test information
        /// </summary>
        [Test]
        public async Task Evaluate_ServerInstructions_PipelineAnalysis_FailedTests()
        {
            const string prompt = "My Azure pipeline failed. Can you analyze the test failures and get the failed test cases from the pipeline artifacts?";
            
            string[] expectedTools =
            [
                "azsdk_get_pipeline_llm_artifacts",
                "azsdk_get_failed_test_cases"
            ];

            var scenarioData = ChatMessageHelper.LoadScenarioFromPrompt(prompt, expectedTools);

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
                    new ExpectedToolInputEvaluatorContext(scenarioData.ExpectedOutcome, s_toolNames!, checkInputs: false)
                });

            EvaluationHelper.ValidateToolInputsEvaluator(result);
        }
    }
}
