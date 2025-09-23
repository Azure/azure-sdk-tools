using Microsoft.VisualStudio.TestTools.UnitTesting;
using AwesomeAssertions;
using Azure.Sdk.Tools.McpEvals.Evaluators;
using Azure.Sdk.Tools.McpEvals.Helpers;
using Azure.Sdk.Tools.McpEvals.Models;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;

namespace Azure.Sdk.Tools.McpEvals.Scenarios
{
    public partial class Scenario
    {
        [TestMethod]
        public async Task AzsdkTypeSpecGeneration_Step02_TypespecValidation()
        {
            // 1. Load Scenario Data from JSON for this test. 
            var filePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "TestData", "example.json");
            var json = await SerializationHelper.LoadScenarioFromChatMessagesAsync(filePath);
            var fullChat = json.ChatHistory.Append(json.NextMessage);
            
            // 2. Get chat response
            var response = await ChatCompletion!.GetChatResponseAsync(fullChat);

            // 3. Custom Evaluator to check tool inputs
            var expectedToolInputEvaluator = new ExpectedToolInputEvaluator();

            // Pass the expected outcome through the additional context. 
            var additionalContext = new ExpectedToolInputEvaluatorContext(json.ExpectedOutcome);
            var result = await expectedToolInputEvaluator.EvaluateAsync(fullChat, response, additionalContext: [additionalContext]);

            // 4. Assert the results
            EvaluationRating[] expectedRatings = [EvaluationRating.Good, EvaluationRating.Exceptional];
            BooleanMetric expectedToolInput = result.Get<BooleanMetric>(ExpectedToolInputEvaluator.ExpectedToolInputMetricName);
            expectedToolInput.Interpretation!.Failed.Should().BeFalse(because: expectedToolInput.Interpretation.Reason);
            expectedToolInput.Interpretation.Rating.Should().BeOneOf(expectedRatings, because: expectedToolInput.Reason);
            expectedToolInput.ContainsDiagnostics(d => d.Severity >= EvaluationDiagnosticSeverity.Warning).Should().BeFalse();
        }
    }
}
