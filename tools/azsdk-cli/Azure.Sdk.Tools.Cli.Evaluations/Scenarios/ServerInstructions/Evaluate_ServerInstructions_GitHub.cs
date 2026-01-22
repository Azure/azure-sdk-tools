using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI.Evaluation;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    /// <summary>
    /// Evaluation scenarios for server instructions related to GitHub and PR management.
    /// Tests: "When managing pull requests in Azure SDK repositories, use azsdk GitHub tools"
    /// </summary>
    public partial class Scenario
    {
        /// <summary>
        /// Tests GitHub user details retrieval
        /// Expected: Agent should use azsdk GitHub tools
        /// </summary>
        [Test]
        public async Task Evaluate_ServerInstructions_GitHub_GetUserDetails()
        {
            const string prompt = "Get my GitHub user details for Azure SDK work";
            
            string[] expectedTools =
            [
                "azsdk_get_github_user_details"
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
        /// Tests PR link retrieval for current branch
        /// Expected: Agent should use azsdk GitHub tools for PR management
        /// </summary>
        [Test]
        public async Task Evaluate_ServerInstructions_GitHub_GetPRLink()
        {
            const string prompt = "Get the pull request link for my current branch in azure-sdk-for-python";
            
            string[] expectedTools =
            [
                "azsdk_get_pull_request_link_for_current_branch"
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
