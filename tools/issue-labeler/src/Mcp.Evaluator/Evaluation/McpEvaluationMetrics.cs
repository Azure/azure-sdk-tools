using IssueLabeler.Shared;

namespace Mcp.Evaluator.Evaluation
{
    /// <summary>
    /// Aggregated metrics from a test run
    /// </summary>
    public class McpEvaluationMetrics
    {
        public int TotalCases { get; set; }
        public int SuccessfulPredictions { get; set; }
        public int FailedPredictions { get; set; }
        
        public int ServerCorrect { get; set; }
        public int ServerIncorrect { get; set; }
        public int ServerMissing { get; set; }
        
        public int ToolCorrect { get; set; }
        public int ToolIncorrect { get; set; }
        public int ToolMissing { get; set; }
        
        public int BothCorrect { get; set; }
        
        public double ServerAccuracy => TotalCases > 0 ? (double)ServerCorrect / TotalCases : 0;
        public double ToolAccuracy => TotalCases > 0 ? (double)ToolCorrect / TotalCases : 0;
        public double CombinedAccuracy => TotalCases > 0 ? (double)BothCorrect / TotalCases : 0;
        public double SuccessRate => TotalCases > 0 ? (double)SuccessfulPredictions / TotalCases : 0;

        public override string ToString()
        {
            return $@"
=== MCP Labeler Evaluation Metrics ===
Total Test Cases: {TotalCases}
Successful Predictions: {SuccessfulPredictions} ({SuccessRate:P2})
Failed Predictions: {FailedPredictions}

Server Label Metrics:
  Correct: {ServerCorrect} ({ServerAccuracy:P2})
  Incorrect: {ServerIncorrect}
  Missing: {ServerMissing}

Tool Label Metrics:
  Correct: {ToolCorrect} ({ToolAccuracy:P2})
  Incorrect: {ToolIncorrect}
  Missing: {ToolMissing}

Combined Accuracy:
  Both Correct: {BothCorrect} ({CombinedAccuracy:P2})
";
        }
    }
}
