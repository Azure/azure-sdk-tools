using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;

namespace Azure.Sdk.Tools.Cli.Evaluations.Evaluators
{
    /// <summary>
    /// Evaluates how well a user prompt matches expected tool descriptions using
    /// keyword overlap matching, modeled after GHCP4A's TriggerMatcher.
    ///
    /// Approach:
    /// - Extracts keywords from the prompt and all tool names/descriptions
    /// - Ranks tools by keyword match count and overlap ratio
    /// - Passes if the expected tool ranks in top K with sufficient keyword matches
    ///
    /// This is deterministic, requires zero API calls, and scales linearly with tool count.
    /// </summary>
    public class PromptToToolMatchEvaluator : IEvaluator
    {
        public const string MatchMetricName = "Prompt To Tool Match";

        public IReadOnlyCollection<string> EvaluationMetricNames => [MatchMetricName];

        public ValueTask<EvaluationResult> EvaluateAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse modelResponse,
            ChatConfiguration? chatConfiguration = null,
            IEnumerable<EvaluationContext>? additionalContext = null,
            CancellationToken cancellationToken = default)
        {
            var metric = new BooleanMetric(MatchMetricName);

            try
            {
                if (additionalContext?.OfType<PromptToToolMatchEvaluatorContext>().FirstOrDefault()
                    is not PromptToToolMatchEvaluatorContext context)
                {
                    MetricError($"A value of type {nameof(PromptToToolMatchEvaluatorContext)} was not found in the {nameof(additionalContext)} collection.", metric);
                    return ValueTask.FromResult(new EvaluationResult(metric));
                }

                var prompt = context.Prompt;
                var expectedToolNames = context.ExpectedToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var tools = context.AvailableTools;
                var topK = context.TopK;

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    MetricError("Prompt is empty or null.", metric);
                    return ValueTask.FromResult(new EvaluationResult(metric));
                }

                if (!expectedToolNames.Any())
                {
                    MetricError("No expected tool names provided.", metric);
                    return ValueTask.FromResult(new EvaluationResult(metric));
                }

                if (!tools.Any())
                {
                    MetricError("No tools available for comparison.", metric);
                    return ValueTask.FromResult(new EvaluationResult(metric));
                }

                var rankedTools = KeywordMatcher.RankTools(prompt, tools);

                // Find the best-ranked expected tool
                int bestExpectedRank = int.MaxValue;
                ToolMatchResult? bestExpectedResult = null;

                for (int rank = 0; rank < rankedTools.Count; rank++)
                {
                    if (expectedToolNames.Contains(rankedTools[rank].Name) && rank < bestExpectedRank)
                    {
                        bestExpectedRank = rank;
                        bestExpectedResult = rankedTools[rank];
                    }
                }

                // Diagnostics
                metric.AddDiagnostics(EvaluationDiagnostic.Informational($"Prompt: \"{TruncateString(prompt, 100)}\""));
                metric.AddDiagnostics(EvaluationDiagnostic.Informational($"Prompt keywords: [{string.Join(", ", KeywordMatcher.ExtractKeywords(prompt))}]"));
                metric.AddDiagnostics(EvaluationDiagnostic.Informational($"Expected tool(s): {string.Join(", ", expectedToolNames)}"));
                metric.AddDiagnostics(EvaluationDiagnostic.Informational($"Top {Math.Min(5, rankedTools.Count)} matches:"));

                for (int i = 0; i < Math.Min(5, rankedTools.Count); i++)
                {
                    var tool = rankedTools[i];
                    var isExpected = expectedToolNames.Contains(tool.Name) ? " ✓" : "";
                    metric.AddDiagnostics(EvaluationDiagnostic.Informational(
                        $"  #{i + 1}: {tool.Name} ({tool.MatchedCount} keywords, {tool.OverlapRatio:P0}){isExpected}"));
                }

                if (bestExpectedResult == null)
                {
                    MetricError($"Expected tool(s) [{string.Join(", ", expectedToolNames)}] not found in available tools.", metric);
                    return ValueTask.FromResult(new EvaluationResult(metric));
                }

                var displayRank = bestExpectedRank + 1;
                var passesRankCheck = displayRank <= topK;
                var passesMatchCheck = KeywordMatcher.IsMatch(bestExpectedResult);

                if (!passesRankCheck)
                {
                    metric.AddDiagnostics(EvaluationDiagnostic.Warning(
                        $"Expected tool '{bestExpectedResult.Name}' ranked #{displayRank}, but needs to be in top {topK}"));
                }

                if (!passesMatchCheck)
                {
                    metric.AddDiagnostics(EvaluationDiagnostic.Warning(
                        $"Expected tool '{bestExpectedResult.Name}' matched {bestExpectedResult.MatchedCount} keywords ({bestExpectedResult.OverlapRatio:P0}), needs ≥2 keywords or ≥20% overlap"));
                }

                if (!passesRankCheck || !passesMatchCheck)
                {
                    metric.AddDiagnostics(EvaluationDiagnostic.Informational(
                        $"Tool description: {TruncateString(bestExpectedResult.Description, 200)}"));
                    metric.AddDiagnostics(EvaluationDiagnostic.Informational(
                        $"Matched keywords: [{string.Join(", ", bestExpectedResult.MatchedKeywords)}]"));
                }

                metric.Value = passesRankCheck && passesMatchCheck;
                metric.Reason = metric.Value == true
                    ? $"Tool '{bestExpectedResult.Name}' ranked #{displayRank} with {bestExpectedResult.MatchedCount} keyword matches ({bestExpectedResult.OverlapRatio:P0})"
                    : $"Tool '{bestExpectedResult.Name}' ranked #{displayRank} with {bestExpectedResult.MatchedCount} keyword matches ({bestExpectedResult.OverlapRatio:P0}) (required: top {topK}, ≥2 keywords or ≥20%). Description: \"{TruncateString(bestExpectedResult.Description, 150)}\"";

                Interpret(metric);
                return ValueTask.FromResult(new EvaluationResult(metric));
            }
            catch (Exception ex)
            {
                MetricError($"Error during prompt-to-tool match evaluation: {ex.Message}", metric);
                return ValueTask.FromResult(new EvaluationResult(metric));
            }
        }

        private static string TruncateString(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLength)
                return s;
            return s[..maxLength] + "...";
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
            metric.Reason = message;
            Interpret(metric);
        }
    }
}
