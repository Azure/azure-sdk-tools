using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Azure.Sdk.Tools.McpEvals.Models
{
    public class ExpectedToolInputEvaluatorContext(IEnumerable<ChatMessage> messages, IEnumerable<string> toolNames, bool checkInputs)
        : EvaluationContext(name: ExpectedResultContextName, content: String.Join(",", messages))
    {
        public static string ExpectedResultContextName => "Expected Result";
        public IEnumerable<ChatMessage> ChatMessages { get; } = messages;
        public IEnumerable<string> ToolNames { get; } = toolNames;
        public bool CheckInputs { get; } = checkInputs;
    }
}
