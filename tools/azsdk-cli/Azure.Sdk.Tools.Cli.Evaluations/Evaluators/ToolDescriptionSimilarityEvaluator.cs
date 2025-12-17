using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Azure.AI.OpenAI;
using Azure;
using Azure.Identity;

namespace Azure.Sdk.Tools.Cli.Evaluations.Evaluators
{
    /// <summary>
    /// Evaluates MCP tool descriptions to identify similar or duplicate descriptions
    /// that could confuse the AI agent using Azure OpenAI embeddings and cosine similarity.
    /// </summary>
    public class ToolDescriptionSimilarityEvaluator : IEvaluator
    {
        public const string SimilarityMetricName = "Tool Description Similarity";
        private const double SimilarityThreshold = 0.80; // 80% cosine similarity threshold
        private readonly AzureOpenAIClient _openAIClient;
        private readonly string _embeddingModelDeployment = "text-embedding-3-large";
        
        public IReadOnlyCollection<string> EvaluationMetricNames => [SimilarityMetricName];

        public ToolDescriptionSimilarityEvaluator(string? azureOpenAIEndpoint = null)
        {
            // Use environment variables or defaults
            var endpoint = azureOpenAIEndpoint ?? 
                Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? 
                throw new InvalidOperationException("Azure OpenAI endpoint not configured. Set AZURE_OPENAI_ENDPOINT environment variable.");
            
            _openAIClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        }

        public async ValueTask<EvaluationResult> EvaluateAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse modelResponse,
            ChatConfiguration? chatConfiguration = null,
            IEnumerable<EvaluationContext>? additionalContext = null,
            CancellationToken cancellationToken = default)
        {
            var metric = new BooleanMetric(SimilarityMetricName);

            try
            {
                // Get tools from additional context
                if (additionalContext?.OfType<ToolDescriptionSimilarityEvaluatorContext>().FirstOrDefault()
                    is not ToolDescriptionSimilarityEvaluatorContext context)
                {
                    MetricError($"A value of type {nameof(ToolDescriptionSimilarityEvaluatorContext)} was not found in the {nameof(additionalContext)} collection.", metric);
                    return new EvaluationResult(metric);
                }

                var tools = context.Tools;
                var similarityIssues = new List<string>();

                // Generate embeddings for all tool descriptions
                var descriptions = tools.Select(t => t.Description ?? string.Empty).ToList();
                var embeddings = await GenerateEmbeddingsAsync(descriptions, cancellationToken);

                // Compare each tool description with every other tool
                for (int i = 0; i < tools.Count; i++)
                {
                    for (int j = i + 1; j < tools.Count; j++)
                    {
                        var tool1 = tools[i];
                        var tool2 = tools[j];

                        var desc1 = tool1.Description ?? string.Empty;
                        var desc2 = tool2.Description ?? string.Empty;

                        // Skip empty descriptions
                        if (string.IsNullOrWhiteSpace(desc1) || string.IsNullOrWhiteSpace(desc2))
                        {
                            continue;
                        }

                        var similarity = CalculateCosineSimilarity(embeddings[i], embeddings[j]);

                        if (similarity >= SimilarityThreshold)
                        {
                            var issue = $"Tools '{tool1.Name}' and '{tool2.Name}' have {similarity:P0} similar descriptions:\n" +
                                    $"  - {tool1.Name}: {desc1}\n" +
                                    $"  - {tool2.Name}: {desc2}";
                            similarityIssues.Add(issue);
                            
                            metric.AddDiagnostics(EvaluationDiagnostic.Warning(issue));
                        }
                        else
                        {
                            metric.AddDiagnostics(
                                EvaluationDiagnostic.Informational(
                                    $"Tools '{tool1.Name}' and '{tool2.Name}' have acceptable description difference ({similarity:P0} similar)"));
                        }
                    }
                }

                if (similarityIssues.Any())
                {
                    metric.Value = false;
                    metric.Reason = $"Found {similarityIssues.Count} pair(s) of tools with overly similar descriptions";
                }
                else
                {
                    metric.Value = true;
                    metric.Reason = $"All {tools.Count} tool descriptions are sufficiently distinct";
                }

                Interpret(metric);
                return new EvaluationResult(metric);
            }
            catch (Exception ex)
            {
                MetricError($"Error during similarity evaluation: {ex.Message}", metric);
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

        private static void Interpret(BooleanMetric metric)
        {
            metric.Interpretation = metric.Value == false
            ? new EvaluationMetricInterpretation(
                EvaluationRating.Unacceptable,
                failed: true,
                reason: "Similar tool descriptions may confuse the AI agent. " + metric.Reason)
            : new EvaluationMetricInterpretation(
                EvaluationRating.Exceptional,
                reason: "Tool descriptions are sufficiently distinct. " + metric.Reason);
        }

        private static void MetricError(string message, BooleanMetric metric)
        {
            metric.AddDiagnostics(EvaluationDiagnostic.Error(message));
            metric.Value = false;
            Interpret(metric);
        }
    }
}
