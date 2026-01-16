using IssueLabeler.Shared;
using IssueLabelerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Mcp.Evaluator.Evaluation
{
    /// <summary>
    /// Test harness for evaluating MCP labeler accuracy using ground truth dataset
    /// </summary>
    public class McpLabelerEvaluator
    {
        private readonly ILabeler _labeler;
        private readonly ILogger _logger;

        public McpLabelerEvaluator(ILabeler labeler, ILogger logger)
        {
            _labeler = labeler;
            _logger = logger;
        }

        /// <summary>
        /// Evaluate the labeler against the full ground truth dataset
        /// </summary>
        public async Task<(List<McpPredictionResult> Results, McpEvaluationMetrics Metrics)> EvaluateAsync(
            List<McpTestCase> testCases,
            bool stopOnFirstError = false)
        {
            var results = new List<McpPredictionResult>();

            _logger.LogInformation($"Starting evaluation of {testCases.Count} test cases...");

            foreach (var testCase in testCases)
            {
                _logger.LogInformation("Testing issue {IssueNumber}: {Title}", testCase.IssueNumber, testCase.Title);

                var result = await EvaluateSingleAsync(testCase);
                results.Add(result);

                if (result.Error != null)
                {
                    _logger.LogError($"  ERROR: {result.Error.Message}");
                    if (stopOnFirstError)
                    {
                        break;
                    }
                }
                else
                {
                    _logger.LogInformation($"  Server: {result.PredictedServerLabel ?? "NONE"} (Expected: {testCase.ExpectedServerLabel}) - {(result.ServerCorrect ? "True" : "False")}");
                    _logger.LogInformation($"  Tool: {result.PredictedToolLabel ?? "NONE"} (Expected: {testCase.ExpectedToolLabel}) - {(result.ToolCorrect ? "True" : "False")}");
                }

                _logger.LogInformation("");
            }

            var metrics = CalculateMetrics(results);
            _logger.LogInformation(metrics.ToString());

            return (results, metrics);
        }

        /// <summary>
        /// Evaluate a single test case
        /// </summary>
        private async Task<McpPredictionResult> EvaluateSingleAsync(McpTestCase testCase)
        {
            var result = new McpPredictionResult
            {
                TestCase = testCase
            };

            try
            {
                var issuePayload = testCase.ToIssuePayload();
                var predictions = await _labeler.PredictLabels(issuePayload);

                // Extract predicted labels
                result.PredictedServerLabel = predictions.ContainsKey("Server") ? predictions["Server"] : null;
                result.PredictedToolLabel = predictions.ContainsKey("Tool") ? predictions["Tool"] : null;

                // Check correctness
                result.ServerCorrect = CheckLabelMatch(
                    result.PredictedServerLabel,
                    testCase.ExpectedServerLabel);

                result.ToolCorrect = CheckLabelMatch(
                    result.PredictedToolLabel,
                    testCase.ExpectedToolLabel);

            }
            catch (Exception ex)
            {
                result.Error = ex;
            }

            return result;
        }

        /// <summary>
        /// Check if predicted label matches expected label(s)
        /// Supports comma-separated expected labels for partial matching
        /// </summary>
        private bool CheckLabelMatch(string predictedLabel, string expectedLabel)
        {
            if (predictedLabel == null || expectedLabel == null)
                return predictedLabel == expectedLabel;

            // Split expected label by comma in case of multiple valid labels
            var expectedLabels = expectedLabel
                .Split(',')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            // Check if predicted matches any of the expected labels
            return expectedLabels.Any(expected => 
                string.Equals(predictedLabel, expected, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Calculate aggregated metrics from results
        /// </summary>
        private McpEvaluationMetrics CalculateMetrics(List<McpPredictionResult> results)
        {
            var metrics = new McpEvaluationMetrics
            {
                TotalCases = results.Count,
                SuccessfulPredictions = results.Count(r => r.Error == null),
                FailedPredictions = results.Count(r => r.Error != null)
            };

            var successfulResults = results.Where(r => r.Error == null).ToList();

            metrics.ServerCorrect = successfulResults.Count(r => r.ServerCorrect);
            metrics.ServerIncorrect = successfulResults.Count(r => !r.ServerCorrect && r.PredictedServerLabel != null);
            metrics.ServerMissing = successfulResults.Count(r => r.PredictedServerLabel == null);

            metrics.ToolCorrect = successfulResults.Count(r => r.ToolCorrect);
            metrics.ToolIncorrect = successfulResults.Count(r => !r.ToolCorrect && r.PredictedToolLabel != null);
            metrics.ToolMissing = successfulResults.Count(r => r.PredictedToolLabel == null);

            metrics.BothCorrect = successfulResults.Count(r => r.BothCorrect);

            return metrics;
        }

        /// <summary>
        /// Generate a detailed report of failures
        /// </summary>
        public string GenerateFailureReport(List<McpPredictionResult> results)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Failure Analysis ===\n");

            var failures = results.Where(r => r.Error == null && !r.BothCorrect).ToList();

            if (failures.Count == 0)
            {
                report.AppendLine("No failures! All predictions were correct.");
                return report.ToString();
            }

            report.AppendLine($"Total Failures: {failures.Count}\n");

            // Server label failures
            var serverFailures = failures.Where(r => !r.ServerCorrect).ToList();
            if (serverFailures.Any())
            {
                report.AppendLine($"Server Label Failures: {serverFailures.Count}");
                foreach (var f in serverFailures)
                {
                    report.AppendLine($"  #{f.TestCase.IssueNumber}: {f.TestCase.Title}");
                    report.AppendLine($"    Expected: {f.TestCase.ExpectedServerLabel}");
                    report.AppendLine($"    Predicted: {f.PredictedServerLabel ?? "NONE"}");
                    report.AppendLine();
                }
            }

            // Tool label failures
            var toolFailures = failures.Where(r => !r.ToolCorrect).ToList();
            if (toolFailures.Any())
            {
                report.AppendLine($"Tool Label Failures: {toolFailures.Count}");
                foreach (var f in toolFailures)
                {
                    report.AppendLine($"  #{f.TestCase.IssueNumber}: {f.TestCase.Title}");
                    report.AppendLine($"    Expected: {f.TestCase.ExpectedToolLabel}");
                    report.AppendLine($"    Predicted: {f.PredictedToolLabel ?? "NONE"}");
                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Export results to CSV for further analysis
        /// </summary>
        public void ExportToCsv(List<McpPredictionResult> results, string filePath)
        {
            using var writer = new StreamWriter(filePath);

            // Header
            writer.WriteLine("IssueNumber,Title,ExpectedServer,PredictedServer,ServerCorrect,ExpectedTool,PredictedTool,ToolCorrect,BothCorrect,Error");

            // Data rows
            foreach (var r in results)
            {
                writer.WriteLine($"{r.TestCase.IssueNumber}," +
                    $"\"{r.TestCase.Title}\"," +
                    $"{r.TestCase.ExpectedServerLabel}," +
                    $"{r.PredictedServerLabel ?? "NONE"}," +
                    $"{r.ServerCorrect}," +
                    $"{r.TestCase.ExpectedToolLabel}," +
                    $"{r.PredictedToolLabel ?? "NONE"}," +
                    $"{r.ToolCorrect}," +
                    $"{r.BothCorrect}," +
                    $"\"{r.Error?.Message ?? ""}\"");
            }

            _logger.LogInformation($"Results exported to: {filePath}");
        }
    }
}
