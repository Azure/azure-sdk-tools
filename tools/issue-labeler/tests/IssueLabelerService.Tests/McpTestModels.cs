using IssueLabeler.Shared;

namespace IssueLabelerService.Tests
{
    /// <summary>
    /// Represents a test case with ground truth labels for evaluation
    /// </summary>
    public class McpTestCase
    {
        public int IssueNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string ExpectedServerLabel { get; set; } = string.Empty;
        public string ExpectedToolLabel { get; set; } = string.Empty;
        public string? Notes { get; set; }

        public IssuePayload ToIssuePayload()
        {
            return new IssuePayload
            {
                IssueNumber = IssueNumber,
                Title = Title,
                Body = Body,
                IssueUserLogin = "testuser",
                RepositoryName = "mcp",
                RepositoryOwnerName = "microsoft"
            };
        }
    }

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
        public int RagResultCount { get; set; }
        public double? ServerConfidence { get; set; }
        public double? ToolConfidence { get; set; }
        public Exception? Error { get; set; }
    }

    /// <summary>
    /// Aggregated metrics from a test run
    /// </summary>
    public class McpEvaluationMetrics
    {
        public int TotalCases { get; set; }
        public int SuccessfulPredictions { get; set; }
        public int FailedPredictions { get; set; }
        
        // Server label metrics
        public int ServerCorrect { get; set; }
        public int ServerIncorrect { get; set; }
        public int ServerMissing { get; set; }
        
        // Tool label metrics
        public int ToolCorrect { get; set; }
        public int ToolIncorrect { get; set; }
        public int ToolMissing { get; set; }
        
        // Combined metrics
        public int BothCorrect { get; set; }
        
        // RAG metrics
        public double AverageRagResults { get; set; }
        public int ZeroRagResults { get; set; }
        
        // Calculated metrics
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

RAG Performance:
  Average RAG Results: {AverageRagResults:F2}
  Zero Results: {ZeroRagResults}
";
        }
    }
}
