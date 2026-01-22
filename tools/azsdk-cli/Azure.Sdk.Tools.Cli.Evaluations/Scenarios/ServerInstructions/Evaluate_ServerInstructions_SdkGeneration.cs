using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI.Evaluation;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    /// <summary>
    /// Evaluation scenarios for server instructions related to SDK generation.
    /// These tests validate that the AI agent correctly selects azsdk tools when 
    /// working with Azure SDK repositories and TypeSpec specifications.
    /// </summary>
    public partial class Scenario
    {
        /// <summary>
        /// Tests: "When generating SDKs from TypeSpec, ask user preference for local vs pipeline-based generation"
        /// Expected: Agent should recognize this as SDK generation task and use azsdk tools
        /// </summary>
        [Test]
        [Category("azure-rest-api-specs")]
        public async Task Evaluate_ServerInstructions_SdkGeneration_FromTypeSpec()
        {
            const string prompt = "Generate a Python SDK from my TypeSpec spec at specification/contosowidgetmanager/Contoso.WidgetManager";
            
            // The agent should use one of these tools for SDK generation
            string[] expectedTools =
            [
                "azsdk_package_generate_code",
                "azsdk_run_generate_sdk"  // Pipeline-based generation
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
        /// Tests: "When working with Azure SDK repositories, TypeSpec API specifications, SDK code generation..."
        /// Expected: Agent should use azsdk tools for TypeSpec project work
        /// </summary>
        [Test]
        [Category("azure-rest-api-specs")]
        public async Task Evaluate_ServerInstructions_TypeSpecProject_ModifiedProjects()
        {
            const string prompt = "What TypeSpec projects have been modified in my current branch?";
            
            string[] expectedTools =
            [
                "azsdk_get_modified_typespec_projects"
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
