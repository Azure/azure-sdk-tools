using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Evaluations.Evaluators
{
    /// <summary>
    /// Evaluates MCP tool descriptions to identify similar or duplicate descriptions
    /// that could confuse the AI agent.
    /// </summary>
    public class ToolDescriptionSimilarityEvaluator : IEvaluator
    {
        public const string SimilarityMetricName = "Tool Description Similarity";
        private const double SimilarityThreshold = 0.2; // 80% similarity threshold
        
        public IReadOnlyCollection<string> EvaluationMetricNames => [SimilarityMetricName];

        public ValueTask<EvaluationResult> EvaluateAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse modelResponse,
            ChatConfiguration? chatConfiguration = null,
            IEnumerable<EvaluationContext>? additionalContext = null,
            CancellationToken cancellationToken = default)
        {
            var metric = new BooleanMetric(SimilarityMetricName);

            // Get tools from additional context
            if (additionalContext?.OfType<ToolDescriptionSimilarityEvaluatorContext>().FirstOrDefault()
                is not ToolDescriptionSimilarityEvaluatorContext context)
            {
                MetricError($"A value of type {nameof(ToolDescriptionSimilarityEvaluatorContext)} was not found in the {nameof(additionalContext)} collection.", metric);
                return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
            }

            var tools = context.Tools;
            var similarityIssues = new List<string>();

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

                    var similarity = CalculateSimilarity(desc1, desc2);

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
            return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
        }

        /// <summary>
        /// Calculate similarity between two strings using Jaccard similarity on word tokens
        /// </summary>
        private static double CalculateSimilarity(string text1, string text2)
        {
            // Normalize: lowercase and extract words
            var words1 = ExtractWords(text1.ToLowerInvariant());
            var words2 = ExtractWords(text2.ToLowerInvariant());

            if (!words1.Any() || !words2.Any())
            {
                return 0.0;
            }

            // Calculate Jaccard similarity: |intersection| / |union|
            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            return union > 0 ? (double)intersection / union : 0.0;
        }

        private static HashSet<string> ExtractWords(string text)
        {
            // Extract words (alphanumeric sequences)
            var words = Regex.Matches(text, @"\b\w+\b")
                             .Cast<Match>()
                             .Select(m => m.Value)
                             .Where(w => w.Length > 2) // Skip very short words
                             .ToHashSet();
            return words;
        }

        private static void Interpret(BooleanMetric metric)
        {
            if (metric.Value == false)
            {
                metric.Interpretation = new EvaluationMetricInterpretation(
                    EvaluationRating.Poor,
                    failed: true,
                    reason: "Similar tool descriptions may confuse the AI agent. " + metric.Reason);
            }
            else
            {
                metric.Interpretation = new EvaluationMetricInterpretation(
                    EvaluationRating.Good,
                    reason: "Tool descriptions are sufficiently distinct. " + metric.Reason);
            }
        }

        private static void MetricError(string message, BooleanMetric metric)
        {
            metric.AddDiagnostics(EvaluationDiagnostic.Error(message));
            metric.Value = false;
            Interpret(metric);
        }
    }
}
