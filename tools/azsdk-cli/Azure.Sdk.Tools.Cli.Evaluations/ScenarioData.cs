using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations
{
    public class ScenarioData
    {
        public required IEnumerable<ChatMessage> ChatHistory { get; set; }
        public required ChatMessage NextMessage { get; set; }
        public required  IEnumerable<ChatMessage> ExpectedOutcome { get; set; }
    }
}
