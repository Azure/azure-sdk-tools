using AwesomeAssertions;
using Azure.Sdk.Tools.McpEvals.Evaluators;
using Microsoft.Extensions.AI.Evaluation;

namespace Azure.Sdk.Tools.McpEvals.Helpers
{
    public class EvaluationHelper
    {
        public static void ValidateToolInputsEvaluator(EvaluationResult result)
        {
            EvaluationRating[] expectedRatings = [EvaluationRating.Good, EvaluationRating.Exceptional];
            BooleanMetric expectedToolInput = result.Get<BooleanMetric>(ExpectedToolInputEvaluator.ExpectedToolInputMetricName);
            expectedToolInput.Interpretation!.Failed.Should().BeFalse(because: expectedToolInput.Interpretation.Reason);
            expectedToolInput.Interpretation.Rating.Should().BeOneOf(expectedRatings, because: expectedToolInput.Reason);
            expectedToolInput.ContainsDiagnostics(d => d.Severity >= EvaluationDiagnosticSeverity.Warning).Should().BeFalse();
        }
    }
}