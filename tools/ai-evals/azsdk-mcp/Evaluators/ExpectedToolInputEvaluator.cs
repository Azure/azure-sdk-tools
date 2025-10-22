using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Azure.Sdk.Tools.McpEvals.Models;
using ModelContextProtocol.Client;

namespace Azure.Sdk.Tools.McpEvals.Evaluators
{
   public class ExpectedToolInputEvaluator : IEvaluator
   {
        public const string ExpectedToolInputMetricName = "Expected Tool Input";
        public IReadOnlyCollection<string> EvaluationMetricNames => [ExpectedToolInputMetricName];

        public ValueTask<EvaluationResult> EvaluateAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse modelResponse,
            ChatConfiguration? chatConfiguration = null,
            IEnumerable<EvaluationContext>? additionalContext = null,
            CancellationToken cancellationToken = default)
        {
            // Create the metric
            var metric = new BooleanMetric(ExpectedToolInputMetricName);
            var result = new ValueTask<EvaluationResult>(new EvaluationResult(metric));

            if (additionalContext?.OfType<ExpectedToolInputEvaluatorContext>().FirstOrDefault()
                is not ExpectedToolInputEvaluatorContext context)
            {
                MetricError($"A value of type {nameof(ExpectedToolInputEvaluatorContext)} was not found in the {nameof(additionalContext)} collection.", metric);
                return result;
            }

            // Get tool calls to compare them
            var expectedToolCalls = Helpers.ChatMessageHelper.GetToolContent(context.ChatMessages, context.ToolNames, true);
            var actualToolCalls = Helpers.ChatMessageHelper.GetToolContent(modelResponse.Messages, context.ToolNames, false);

            // Make sure we have tool calls to compare
            if (!expectedToolCalls.Any())
            {
                MetricError($"No Tool calls detected inside of {nameof(additionalContext)} collection.", metric);
                return result;
            }

            if (!actualToolCalls.Any())
            {
                MetricError($"Provided LLM Result contained no tool call. Expected to call: {PrintToolNames(expectedToolCalls)}", metric);
                return result;
            }

            // Equal number of tool calls
            var expCount = expectedToolCalls.Count();
            var actCount = actualToolCalls.Count();
            if (expCount != actCount)
            {
                MetricError($"The LLM Result had different set of tool calls than expected: Actual calls: {PrintToolNames(actualToolCalls)}, Expected calls: {PrintToolNames(expectedToolCalls)}", metric);
                return result;
            }

            var countCalls = 0;
            foreach (var toolCall in expectedToolCalls.Zip(actualToolCalls, (exp, act) => new {exp, act}))
            {
                var expToolName = toolCall.exp.Name;
                var actToolName = toolCall.act.Name;
                countCalls++;

                metric.AddDiagnostics(
                    EvaluationDiagnostic.Informational(
                        $"Tool call #{countCalls}, expected the {expToolName} tool, and LLM called the {actToolName} tool."));

                // If the names do not align then the tool calls were made in the wrong order by the LLM
                // Ends with because copilot attaches mcp_ to name
                if (actToolName.EndsWith(expToolName))
                {
                    MetricError($"Tool call made in the wrong order. Expected the {expToolName} tool but the LLM called {actToolName} tool. This was tool call #{countCalls}", metric);
                    return result;
                }

                // No arguments to compare or flag indicates to skip checking inputs
                if (toolCall.exp.Arguments == null || !context.CheckInputs)
                {
                    continue;
                }

                if(toolCall.act.Arguments == null)
                {
                    MetricError($"Tool call #{countCalls}, expected arguments for tool {expToolName} but LLM provided none.", metric);
                    return result;
                }

                // Since it is a dictionary with a object may just be easier to serialize and compare strings. Can then even return the string args to the user if something went wrong. 
                try
                {
                    // Serialize with consistent options for better comparison
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    // Sort the dictionaries to prevent ordering issues during comparison
                    var expectedArguments = new SortedDictionary<string, object?>(toolCall.exp.Arguments);
                    var actualArguments = new SortedDictionary<string, object?>(toolCall.act.Arguments);

                    var expectedJson = JsonSerializer.Serialize(expectedArguments, options);
                    var actualJson = JsonSerializer.Serialize(actualArguments, options);

                    if (!string.Equals(expectedJson, actualJson, StringComparison.OrdinalIgnoreCase))
                    {
                        MetricError($"Tool call arguments did not match. This was tool call #{countCalls}\nExpected Argument JSON:{expectedJson}\nActual Argument JSON:{actualJson}", metric);
                        return result;
                    }
                }
                catch (JsonException ex)
                {
                    MetricError($"Tool call #{countCalls}, failed to serialize either the expected tool call or the actual tool call. Error: {ex}", metric);
                    return result;
                }
                
            }

            metric.Value = true;
            metric.Reason = "The results tool calls input matched the expected tool calls output";

            Interpret(metric);

            // If we made it here then everything was a match and our tools input matched up to what we expected. 
            return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
        }

        private static void Interpret(BooleanMetric metric)
        {
            if (metric.Value == false)
            {
                metric.Interpretation =
                    new EvaluationMetricInterpretation(
                        EvaluationRating.Unacceptable,
                        failed: true,
                        reason: "Result did not match the expected outcome.");
            }
            else
            {
                metric.Interpretation = new EvaluationMetricInterpretation(
                    EvaluationRating.Exceptional,
                    reason: "Result matched what was expected.");
            }
        }

        private static string PrintToolNames(IEnumerable<FunctionCallContent> toolCalls)
        {
            return string.Join(",", toolCalls.Select(toolCall => toolCall.Name));
        }

        private static void MetricError(string message, BooleanMetric metric)
        {
            metric.AddDiagnostics(EvaluationDiagnostic.Error(message));
            metric.Value = false;
            Interpret(metric);
        }
    }
}
