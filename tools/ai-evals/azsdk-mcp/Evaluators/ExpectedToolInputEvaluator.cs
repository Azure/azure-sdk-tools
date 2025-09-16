using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using azsdk_mcp.Models;

namespace azsdk_mcp.Evaluators
{
   public class ExpectedToolInputEvaluator : IEvaluator
   {
        public const string ExpectedToolInputMetricName = "Azure SDK MCP";
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
                metric.AddDiagnostics(
                    EvaluationDiagnostic.Error(
                        $"A value of type {nameof(ExpectedToolInputEvaluatorContext)} was not found in the {nameof(additionalContext)} collection."));

                return result;
            }

            // Get tool calls to compare them
            var expectedToolCalls = GetToolContent(context.ChatMessages);
            var actualToolCalls = GetToolContent(modelResponse.Messages);

            if (!expectedToolCalls.Any())
            {
                metric.AddDiagnostics(
                    EvaluationDiagnostic.Error(
                        $"No Tool calls detected inside of {nameof(additionalContext)} collection."));

                return result;
            }

            if (!actualToolCalls.Any())
            {
                metric.AddDiagnostics(
                    EvaluationDiagnostic.Error(
                        $"Provided LLM Result contained no tool calls."));

                return result;
            }
            var expCount = expectedToolCalls.Count();
            var actCount = actualToolCalls.Count();
            if (expCount != actCount)
            {
                metric.AddDiagnostics(
                    EvaluationDiagnostic.Error(
                        $"The LLM Result had more tool calls than expected: Actual calls: {actCount}, Expected calls: {expCount}"));

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
                if (expToolName != actToolName)
                {
                    metric.AddDiagnostics(
                    EvaluationDiagnostic.Error(
                        $"Tool call made in the wrong order. Expected the {expToolName} tool but the LLM called {actToolName} tool. This was tool call #{countCalls}"));
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

                    var expectedJson = JsonSerializer.Serialize(toolCall.exp.Arguments, options);
                    var actualJson = JsonSerializer.Serialize(toolCall.act.Arguments, options);

                    if (!string.Equals(expectedJson, actualJson, StringComparison.OrdinalIgnoreCase))
                    {
                        metric.AddDiagnostics(
                            EvaluationDiagnostic.Error(
                                $"Tool call made in the wrong order. Expected the {expToolName} tool but the LLM called {actToolName} tool. This was tool call #{countCalls}\nExpected Argument JSON:{expectedJson}\nActual Argument JSON:{actualJson}"));
                        return result;
                    }
                }
                catch (JsonException ex)
                {
                    metric.AddDiagnostics(
                        EvaluationDiagnostic.Error(
                            $"Tool call #{countCalls}, failed to serialize either the {expToolName} tool or the {actToolName} tool. Error: {ex}"));
                    return result;
                }
                
            }

            metric.Value = true;
            metric.Reason = "The results tool calls input matched the expcted tool calls output";

            Interpret(metric);

            // If we made it here then everything was a match and our tools input matched up to what we expcted. 
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

        private static IEnumerable<FunctionCallContent> GetToolContent(IEnumerable<ChatMessage> messages)
        {
            return messages
                .Where(message => message.Role == ChatRole.Assistant)
                .SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>();
        }
   }
}
