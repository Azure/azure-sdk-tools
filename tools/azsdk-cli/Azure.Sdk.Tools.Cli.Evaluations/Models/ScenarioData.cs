using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.Models
{
    public class ScenarioData
    {
        public required IEnumerable<ChatMessage> ChatHistory { get; set; }
        public required ChatMessage NextMessage { get; set; }
        public required  IEnumerable<ChatMessage> ExpectedOutcome { get; set; }
    }
}
