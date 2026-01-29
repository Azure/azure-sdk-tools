using AwesomeAssertions;
using Azure.AI.OpenAI;
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

        public static void ValidatePromptToToolMatchEvaluator(EvaluationResult result)
        {
            ValidateBooleanMetricEvaluator(result, PromptToToolMatchEvaluator.MatchMetricName);
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
            CancellationToken cancellationToken = default,
            IEnumerable<string>? optionalToolNames = null)
        {
            evaluators ??= [new ExpectedToolInputEvaluator()];

            var fullChat = scenarioData.ChatHistory.Append(scenarioData.NextMessage);

            // Default optional tools to empty when not provided
            optionalToolNames ??= [];

            // Get expected tool names from the scenario data and optional tool names
            var expectedToolNames = ChatMessageHelper.GetExpectedToolsByName(scenarioData.ExpectedOutcome, toolNames).Keys;
            var filteredOptionalToolNames = GetOptionalToolNames(optionalToolNames, expectedToolNames);

            // We can use LoadScenarioPrompt with empty prompt to get optional tools
            // in the proper format. 
            var optionalTools = ChatMessageHelper.LoadScenarioFromPrompt("", filteredOptionalToolNames).ExpectedOutcome;

            // Include the optional tools along side the expected. 
            // Later we will then filter them out from the response.
            var toolChatMessages = optionalTools.Concat(scenarioData.ExpectedOutcome);
            var toolResults = ChatMessageHelper.GetExpectedToolsByName(toolChatMessages, toolNames);

            var response = await chatCompletion.GetChatResponseWithExpectedResponseAsync(
                fullChat,
                toolResults,
                filteredOptionalToolNames);

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

        private static IEnumerable<string> GetOptionalToolNames(
            IEnumerable<string> optionalToolNames,
            IEnumerable<string> expectedToolNames)
        {
            // Build optional list excluding any names that are expected
            // also make sure to always include verify setup
            var combinedOptional = optionalToolNames.Append(verifySetupToolName);
            return combinedOptional.Except(expectedToolNames);
        }

        /// <summary>
        /// Generate embeddings for a list of texts using Azure OpenAI.
        /// Shared helper for embedding-based evaluators.
        /// </summary>
        public static async Task<List<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
            AzureOpenAIClient openAIClient,
            string embeddingModelDeployment,
            List<string> texts,
            CancellationToken cancellationToken)
        {
            var embeddingClient = openAIClient.GetEmbeddingClient(embeddingModelDeployment);
            var embeddings = new List<ReadOnlyMemory<float>>();

            // Azure OpenAI has a limit on batch size, so process in chunks if needed
            const int batchSize = 100;
            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToList();
                var response = await embeddingClient.GenerateEmbeddingsAsync(batch, cancellationToken: cancellationToken);

                foreach (var embedding in response.Value)
                {
                    embeddings.Add(embedding.ToFloats());
                }
            }

            return embeddings;
        }

        /// <summary>
        /// Calculate cosine similarity between two embedding vectors.
        /// Shared helper for embedding-based evaluators.
        /// </summary>
        public static double CalculateCosineSimilarity(ReadOnlyMemory<float> vector1, ReadOnlyMemory<float> vector2)
        {
            var v1 = vector1.Span;
            var v2 = vector2.Span;

            if (v1.Length != v2.Length)
            {
                throw new ArgumentException("Vectors must have the same length");
            }

            double dotProduct = 0.0;
            double magnitude1 = 0.0;
            double magnitude2 = 0.0;

            for (int i = 0; i < v1.Length; i++)
            {
                dotProduct += v1[i] * v2[i];
                magnitude1 += v1[i] * v1[i];
                magnitude2 += v2[i] * v2[i];
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0.0 || magnitude2 == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }
    }
}
