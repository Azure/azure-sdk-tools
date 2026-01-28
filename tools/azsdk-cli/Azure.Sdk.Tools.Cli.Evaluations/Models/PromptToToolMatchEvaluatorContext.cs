using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Azure.Sdk.Tools.Cli.Evaluations.Models
{
    /// <summary>
    /// Context for PromptToToolMatchEvaluator containing the prompt to evaluate
    /// and the expected tool(s) that should be matched.
    /// </summary>
    public class PromptToToolMatchEvaluatorContext(
        string prompt,
        IEnumerable<string> expectedToolNames,
        IReadOnlyList<AIFunction> availableTools)
        : EvaluationContext(name: ContextName, content: prompt)
    {
        public static string ContextName => "Prompt To Tool Match Context";

        /// <summary>
        /// The user prompt to evaluate against tool descriptions.
        /// </summary>
        public string Prompt { get; } = prompt;

        /// <summary>
        /// The expected tool name(s) that should match the prompt.
        /// The evaluator checks if at least one of these tools ranks in the top K.
        /// </summary>
        public IEnumerable<string> ExpectedToolNames { get; } = expectedToolNames;

        /// <summary>
        /// All available tools from the MCP server to compare against.
        /// </summary>
        public IReadOnlyList<AIFunction> AvailableTools { get; } = availableTools;

        /// <summary>
        /// Minimum confidence score (cosine similarity) required for a match.
        /// Default is 0.4 (40%), chosen empirically:
        /// - Below 40%: Prompts are too vague to reliably identify the correct tool
        /// - Above 40%: Strong semantic match between prompt and tool description
        /// Combined with TopK ranking to catch both ambiguous prompts and similar tool descriptions.
        /// </summary>
        public double MinConfidence { get; init; } = 0.4;

        /// <summary>
        /// Number of top results to consider for a match.
        /// Default is 3, meaning the expected tool should rank in the top 3.
        /// </summary>
        public int TopK { get; init; } = 3;
    }
}
