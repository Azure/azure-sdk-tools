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

        [McpServerPrompt, Description("Get release readiness for a package")]
        public static Task<string> ExampleGetReleaseReadiness()
        {
            return Task.FromResult($"What's my release readiness for the C# package for Azure.Identity?");
        }
    }
}
