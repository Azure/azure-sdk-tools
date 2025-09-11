using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Azure.Sdk.Tools.Cli.Evaluations.Models
{
    public class ExpectedToolInputEvaluatorContext(IEnumerable<ChatMessage> messages)
        : EvaluationContext(name: ExpectedResultContextName, content: String.Join(",", messages))
    {
        public static string ExpectedResultContextName => "Expected Result";
        public IEnumerable<ChatMessage> ChatMessages { get; } = messages;
    }
}
