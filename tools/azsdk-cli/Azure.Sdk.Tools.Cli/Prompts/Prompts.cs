using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Prompts
{
    public class Prompts
    {
        [McpServerPrompt, Description("Start the next steps, after TypeSpec changes")]
        public static Task<string> NextSteps()
        {
            return Task.FromResult("I have completed my TypeSpec changes. What's the next step to generate SDK?");
        }

        [McpServerPrompt, Description("Get status for a spec PR")]
        public static Task<string> GetStatusForSpecPR(string prNumber)
        {
            return Task.FromResult($"I have submitted a spec PR#{prNumber}. What's the status of it");
        }
    }
}
