using IssueLabeler.Shared;

namespace Mcp.Evaluator.Evaluation
{
    /// <summary>
    /// Results from evaluating a single test case
    /// </summary>
    public class McpPredictionResult
    {
        public McpTestCase TestCase { get; set; } = null!;
        public string? PredictedServerLabel { get; set; }
        public string? PredictedToolLabel { get; set; }
        public bool ServerCorrect { get; set; }
        public bool ToolCorrect { get; set; }
        public bool BothCorrect => ServerCorrect && ToolCorrect;
        public double? ServerConfidence { get; set; }
        public double? ToolConfidence { get; set; }
        public Exception? Error { get; set; }
    }
}