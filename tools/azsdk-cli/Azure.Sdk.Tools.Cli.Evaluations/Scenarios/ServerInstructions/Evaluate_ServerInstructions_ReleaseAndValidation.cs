using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI.Evaluation;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    /// <summary>
    /// Evaluation scenarios for server instructions related to release workflows.
    /// Tests: 
    /// - "When working with SDK release workflows or release plans, use azsdk release tools"
    /// - "When checking package release readiness, use azsdk validation tools"
    /// </summary>
    public partial class Scenario
    {
        /// <summary>
        /// Tests release plan creation scenario
        /// Expected: Agent should use azsdk release tools
        /// </summary>
        [Test]
        [Category("azure-rest-api-specs")]
        public async Task Evaluate_ServerInstructions_Release_CreatePlan()
        {
            const string prompt = "Create a release plan for my Azure SDK package with TypeSpec project at specification/contososervice/Contoso.Service";
            
            string[] expectedTools =
            [
                "azsdk_create_release_plan"
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
        /// Tests package validation scenario
        /// Expected: Agent should use azsdk validation tools for package checks
        /// </summary>
        [Test]
        public async Task Evaluate_ServerInstructions_Validation_PackageCheck()
        {
            const string prompt = "Run validation checks on my SDK package at sdk/storage/azure-storage-blob";
            
            string[] expectedTools =
            [
                "azsdk_package_run_check"
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
        /// Tests environment setup verification
        /// Expected: Agent should use azsdk verification tools
        /// </summary>
        [Test]
        public async Task Evaluate_ServerInstructions_Validation_VerifySetup()
        {
            const string prompt = "Verify my development environment is set up correctly for Python SDK development";
            
            string[] expectedTools =
            [
                "azsdk_verify_setup"
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
