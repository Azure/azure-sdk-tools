using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Azure.Sdk.Tools.Cli.Evaluations.Models
{
    /// <summary>
    /// Context for ToolDescriptionSimilarityEvaluator containing the list of tools to evaluate
    /// </summary>
    public class ToolDescriptionSimilarityEvaluatorContext(IReadOnlyList<AIFunction> tools)
        : EvaluationContext(name: ToolDescriptionSimilarityContextName, content: $"{tools.Count} tools to evaluate")
    {
        public static string ToolDescriptionSimilarityContextName => "Tool Description Similarity";
        public IReadOnlyList<AIFunction> Tools { get; } = tools;
    }
}
