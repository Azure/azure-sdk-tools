using AwesomeAssertions;
using Azure.Sdk.Tools.McpEvals.Evaluators;
using Azure.Sdk.Tools.McpEvals.Helpers;
using Azure.Sdk.Tools.McpEvals.Models;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Html;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using ModelContextProtocol.Protocol;
using NUnit.Framework;

namespace Azure.Sdk.Tools.McpEvals.Scenarios
{
    public partial class Scenario
    {
    [Test]
        public async Task AzsdkTypeSpecGeneration_Step02_TypespecValidation()
        {
            // 1. Load Scenario Data from JSON for this test. 
            var filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "example.json");
            var json = await SerializationHelper.LoadScenarioFromChatMessagesAsync(filePath);
            var fullChat = json.ChatHistory.Append(json.NextMessage);

            // 2. Get chat response
            var expectedToolResults = SerializationHelper.GetExpectedToolsByName(json.ExpectedOutcome, s_toolNames);
            var response = await s_chatCompletion!.GetChatResponseWithExpectedResponseAsync(fullChat, expectedToolResults);

            // 3. Custom Evaluator to check tool inputs
            // Layers the reporting configuration on top of it for a nice html report. 
            // Could not make this static because each test will have to define what evaluators it wants to use.
            var reportingConfiguration = DiskBasedReportingConfiguration.Create(
                executionName: s_executionName,                     // Having a static execution name allows us to see all results in one report
                storageRootPath: ReportingPath,
                evaluators: [new ExpectedToolInputEvaluator()],     // In this test we only want to run the ExpectedToolInputEvaluator
                chatConfiguration: s_chatConfig,
                enableResponseCaching: true);
            await using ScenarioRun scenarioRun = await reportingConfiguration.CreateScenarioRunAsync(this.ScenarioName);

            // Pass the expected outcome through the additional context. 
            var additionalContext = new ExpectedToolInputEvaluatorContext(json.ExpectedOutcome, s_toolNames);
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
