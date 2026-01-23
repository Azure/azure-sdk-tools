using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.AI.OpenAI;

namespace Azure.Sdk.Tools.Cli.Evaluations.Evaluators
{
    /// <summary>
    /// Evaluates how well a user prompt matches expected tool descriptions using
    /// Azure OpenAI embeddings and cosine similarity.
    /// 
    /// Based on <a href="https://github.com/Azure/azure-mcp/tree/main/eng/tools/ToolDescriptionEvaluator">ToolDescriptionEvaluator</a> approach:
    /// - Generates embeddings for the prompt and all tool descriptions
    /// - Ranks tools by cosine similarity to the prompt
    /// - Passes if the expected tool ranks in top K with sufficient confidence
    /// </summary>
    public class PromptToToolMatchEvaluator : IEvaluator
    {
        public const string MatchMetricName = "Prompt To Tool Match";
        private readonly AzureOpenAIClient _openAIClient;
        private readonly string _embeddingModelDeployment = "text-embedding-3-large";

        public IReadOnlyCollection<string> EvaluationMetricNames => [MatchMetricName];

        public PromptToToolMatchEvaluator()
        {
            _openAIClient = TestSetup.GetAzureOpenAIClient();
        }

        public async ValueTask<EvaluationResult> EvaluateAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse modelResponse,
            ChatConfiguration? chatConfiguration = null,
            IEnumerable<EvaluationContext>? additionalContext = null,
            CancellationToken cancellationToken = default)
        {
            var metric = new BooleanMetric(MatchMetricName);

            try
            {
                // Get context
                if (additionalContext?.OfType<PromptToToolMatchEvaluatorContext>().FirstOrDefault()
                    is not PromptToToolMatchEvaluatorContext context)
                {
                    MetricError($"A value of type {nameof(PromptToToolMatchEvaluatorContext)} was not found in the {nameof(additionalContext)} collection.", metric);
                    return new EvaluationResult(metric);
                }

                var prompt = context.Prompt;
                var expectedToolNames = context.ExpectedToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var tools = context.AvailableTools;
                var minConfidence = context.MinConfidence;
                var topK = context.TopK;

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    MetricError("Prompt is empty or null.", metric);
                    return new EvaluationResult(metric);
                }

                if (!expectedToolNames.Any())
                {
                    MetricError("No expected tool names provided.", metric);
                    return new EvaluationResult(metric);
                }

                if (!tools.Any())
                {
                    MetricError("No tools available for comparison.", metric);
                    return new EvaluationResult(metric);
                }

                // Generate embeddings for prompt and all tool descriptions
                var textsToEmbed = new List<string> { prompt };
                textsToEmbed.AddRange(tools.Select(t => t.Description ?? t.Name));

                var embeddings = await GenerateEmbeddingsAsync(textsToEmbed, cancellationToken);
                var promptEmbedding = embeddings[0];

                // Calculate similarity scores for all tools
                var toolScores = new List<(string Name, double Score, string Description)>();
                for (int i = 0; i < tools.Count; i++)
                {
                    var tool = tools[i];
                    var toolEmbedding = embeddings[i + 1]; // +1 because prompt is at index 0
                    var similarity = CalculateCosineSimilarity(promptEmbedding, toolEmbedding);
                    toolScores.Add((tool.Name, similarity, tool.Description ?? ""));
                }

                // Sort by score descending
                var rankedTools = toolScores.OrderByDescending(t => t.Score).ToList();

                // Find the rank and score of the expected tool(s)
                int bestExpectedRank = int.MaxValue;
                double bestExpectedScore = 0;
                string? bestExpectedTool = null;

                for (int rank = 0; rank < rankedTools.Count; rank++)
                {
                    var tool = rankedTools[rank];
                    if (expectedToolNames.Contains(tool.Name))
                    {
                        if (rank < bestExpectedRank)
                        {
                            bestExpectedRank = rank;
                            bestExpectedScore = tool.Score;
                            bestExpectedTool = tool.Name;
                        }
                    }
                }

                // Add diagnostics showing top results
                metric.AddDiagnostics(EvaluationDiagnostic.Informational($"Prompt: \"{TruncateString(prompt, 100)}\""));
                metric.AddDiagnostics(EvaluationDiagnostic.Informational($"Expected tool(s): {string.Join(", ", expectedToolNames)}"));
                metric.AddDiagnostics(EvaluationDiagnostic.Informational($"Top {Math.Min(5, rankedTools.Count)} matches:"));

                for (int i = 0; i < Math.Min(5, rankedTools.Count); i++)
                {
                    var tool = rankedTools[i];
                    var isExpected = expectedToolNames.Contains(tool.Name) ? " ✓" : "";
                    metric.AddDiagnostics(EvaluationDiagnostic.Informational(
                        $"  #{i + 1}: {tool.Name} ({tool.Score:P0}){isExpected}"));
                }

                // Evaluate pass/fail
                if (bestExpectedTool == null)
                {
                    MetricError($"Expected tool(s) [{string.Join(", ", expectedToolNames)}] not found in available tools.", metric);
                    return new EvaluationResult(metric);
                }

                var displayRank = bestExpectedRank + 1; // 1-indexed for display
                var passesRankCheck = displayRank <= topK;
                var passesConfidenceCheck = bestExpectedScore >= minConfidence;

                if (!passesRankCheck)
                {
                    metric.AddDiagnostics(EvaluationDiagnostic.Warning(
                        $"Expected tool '{bestExpectedTool}' ranked #{displayRank}, but needs to be in top {topK}"));
                }

                if (!passesConfidenceCheck)
                {
                    metric.AddDiagnostics(EvaluationDiagnostic.Warning(
                        $"Expected tool '{bestExpectedTool}' has {bestExpectedScore:P0} confidence, but needs ≥{minConfidence:P0}"));
                }

                metric.Value = passesRankCheck && passesConfidenceCheck;
                metric.Reason = metric.Value == true
                    ? $"Tool '{bestExpectedTool}' ranked #{displayRank} with {bestExpectedScore:P0} confidence"
                    : $"Tool '{bestExpectedTool}' ranked #{displayRank} with {bestExpectedScore:P0} confidence (required: top {topK}, ≥{minConfidence:P0})";

                Interpret(metric);
                return new EvaluationResult(metric);
            }
            catch (Exception ex)
            {
                MetricError($"Error during prompt-to-tool match evaluation: {ex.Message}", metric);
                return new EvaluationResult(metric);
            }
        }

        /// <summary>
        /// Generate embeddings for a list of texts using Azure OpenAI
        /// </summary>
        private async Task<List<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
            List<string> texts,
            CancellationToken cancellationToken)
        {
            var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingModelDeployment);
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
        /// Calculate cosine similarity between two embedding vectors
        /// </summary>
        private static double CalculateCosineSimilarity(ReadOnlyMemory<float> vector1, ReadOnlyMemory<float> vector2)
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

        private static string TruncateString(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLength)
                return s;
            return s.Substring(0, maxLength) + "...";
        }

        private static void Interpret(BooleanMetric metric)
        {
            metric.Interpretation = metric.Value == false
                ? new EvaluationMetricInterpretation(
                    EvaluationRating.Unacceptable,
                    failed: true,
                    reason: "Prompt did not match expected tool. " + metric.Reason)
                : new EvaluationMetricInterpretation(
                    EvaluationRating.Exceptional,
                    reason: "Prompt matched expected tool. " + metric.Reason);
        }

        private static void MetricError(string message, BooleanMetric metric)
        {
            metric.AddDiagnostics(EvaluationDiagnostic.Error(message));
            metric.Value = false;
            Interpret(metric);
        }
    }
}
