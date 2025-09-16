using Microsoft.VisualStudio.TestTools.UnitTesting;
using AwesomeAssertions;
using Azure.Sdk.Tools.Cli.Evaluations.Evaluators;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Models;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;

namespace Azure.Sdk.Tools.Cli.Evaluations.Scenarios
{
    public partial class Scenario
    {
        [TestMethod]
        public async Task AzsdkTypeSpecGeneration_Step02_TypespecValidation()
        {
            // 1. Validate ChatHistory. ex.. Should end with AI answering and not the user
            // Before it gets here will need to be converted from JSON to chat message somehow. 
            var json = await SerializationHelper.LoadScenarioFromJsonAsync(JsonPath);
            var fullChat = json.ChatHistory.Append(json.NextMessage);

            // 2. LLM question and answer
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
