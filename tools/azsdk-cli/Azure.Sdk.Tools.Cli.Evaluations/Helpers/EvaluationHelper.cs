using AwesomeAssertions;
using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
{
    public class EvaluationHelper
    {
        private const string verifySetupToolName = "azsdk_verify_setup";
        
        public static void ValidateBooleanMetricEvaluator(EvaluationResult result, string metricName)
        {
            EvaluationRating[] expectedRatings = [EvaluationRating.Good, EvaluationRating.Exceptional];
            BooleanMetric metric = result.Get<BooleanMetric>(metricName);
            metric.Interpretation!.Failed.Should().BeFalse(because: metric.Interpretation.Reason);
            metric.Interpretation.Rating.Should().BeOneOf(expectedRatings, because: metric.Reason);
            metric.ContainsDiagnostics(d => d.Severity >= EvaluationDiagnosticSeverity.Warning).Should().BeFalse();
        }

        public static void ValidateToolInputsEvaluator(EvaluationResult result)
        {
            ValidateBooleanMetricEvaluator(result, ExpectedToolInputEvaluator.ExpectedToolInputMetricName);
        }

        public static void ValidateToolDescriptionSimilarityEvaluator(EvaluationResult result)
        {
            ValidateBooleanMetricEvaluator(result, ToolDescriptionSimilarityEvaluator.SimilarityMetricName);
        }

        public static async Task<EvaluationResult> RunScenarioAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse response,
            string scenarioName,
            ChatConfiguration chatConfig,
            string executionName,
            string reportingPath,
            IEnumerable<IEvaluator> evaluators,
            bool enableResponseCaching = true,
            IEnumerable<EvaluationContext>? additionalContexts = null,
            CancellationToken cancellationToken = default,
            IEnumerable<string>? optionalToolNames = null)
        {
            evaluators ??= [new ExpectedToolInputEvaluator()];

            var fullChat = scenarioData.ChatHistory.Append(scenarioData.NextMessage);

            // Default optional tools to empty when not provided
            optionalToolNames ??= Enumerable.Empty<string>();

            // We can use LoadScenarioPrompt with empty prompt to get optional tools
            // in the proper format. 
            optionalToolNames = optionalToolNames.Append(verifySetupToolName);
            var optionalTools = ChatMessageHelper.LoadScenarioFromPrompt("", optionalToolNames).ExpectedOutcome;

            // Include the optional tools along side the expected. 
            // Later we will then filter them out from the response.
            var toolChatMessages = optionalTools.Concat(scenarioData.ExpectedOutcome);
            var toolResults = ChatMessageHelper.GetExpectedToolsByName(toolChatMessages, toolNames);

            var reportingConfiguration = DiskBasedReportingConfiguration.Create(
                executionName: executionName,
                storageRootPath: reportingPath,
                evaluators: evaluators,
                chatConfiguration: chatConfig,
                enableResponseCaching: enableResponseCaching);

            await using ScenarioRun scenarioRun = await reportingConfiguration.CreateScenarioRunAsync(scenarioName, cancellationToken: cancellationToken);

            var response = await chatCompletion.GetChatResponseWithExpectedResponseAsync(
                fullChat,
                toolResults,
                optionalToolNames);

            var result = await scenarioRun.EvaluateAsync(
                messages,
                response,
                additionalContext: additionalContexts ?? [],
                cancellationToken: cancellationToken);

            return result;
        }

        private static IEnumerable<ChatMessage> GetOptionalTools(
            IEnumerable<string> optionalToolNames,
            IEnumerable<string> expectedToolNames)
        {
            // Build optional list excluding any names that are expected
            // also make sure to always include verify setup
            var combinedOptional = optionalToolNames.Append(verifySetupToolName);
            var filteredOptional = combinedOptional.Except(expectedToolNames);

            var optionalTools = ChatMessageHelper.LoadScenarioFromPrompt("", filteredOptional).ExpectedOutcome;
            return optionalTools;
        }
      
        public static async Task<EvaluationResult> RunToolInputScenarioAsync(
            string scenarioName,
            ScenarioData scenarioData,
            ChatCompletion chatCompletion,
            ChatConfiguration chatConfig,
            string executionName,
            string reportingPath,
            IEnumerable<string> toolNames,
            IEnumerable<IEvaluator>? evaluators = null,
            bool enableResponseCaching = true,
            IEnumerable<EvaluationContext>? additionalContexts = null,
            CancellationToken cancellationToken = default)
        {
            evaluators ??= [new ExpectedToolInputEvaluator()];

            var fullChat = scenarioData.ChatHistory.Append(scenarioData.NextMessage);
            var expectedToolResults = ChatMessageHelper.GetExpectedToolsByName(scenarioData.ExpectedOutcome, toolNames);

            var response = await chatCompletion.GetChatResponseWithExpectedResponseAsync(
                fullChat,
                expectedToolResults);

            return await RunScenarioAsync(
                fullChat,
                response,
                scenarioName,
                chatConfig,
                executionName,
                reportingPath,
                evaluators,
                enableResponseCaching,
                additionalContexts,
                cancellationToken);
        }
    }
}
