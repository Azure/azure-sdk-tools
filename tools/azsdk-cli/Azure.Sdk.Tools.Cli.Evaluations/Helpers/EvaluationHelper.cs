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
            CancellationToken cancellationToken = default)
        {
            var reportingConfiguration = DiskBasedReportingConfiguration.Create(
                executionName: executionName,
                storageRootPath: reportingPath,
                evaluators: evaluators,
                chatConfiguration: chatConfig,
                enableResponseCaching: enableResponseCaching);

            await using ScenarioRun scenarioRun = await reportingConfiguration.CreateScenarioRunAsync(scenarioName, cancellationToken: cancellationToken);

            var result = await scenarioRun.EvaluateAsync(
                messages,
                response,
                additionalContext: additionalContexts ?? [],
                cancellationToken: cancellationToken);

            return result;
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
