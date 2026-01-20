using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public static class ToolMocks
    {
        private static readonly Dictionary<string, IToolMock> Mocks = new Dictionary<string, IToolMock>();

        static ToolMocks()
        {
            RegisterMocks();
        }

        private static void RegisterMocks()
        {
            var mockInstances = new List<IToolMock>
            {
                // Add all mocks here
                new TypespecCheckProjectInPublicRepo(),
                new RunTypespecValidation(),
                new GetModifiedTypespecProjects(),
                new GetPullRequestLinkForCurrentBranch(),
                new CreatePullRequest(),
                new CreateReleasePlan(),
                new VerifySetup(),
                new LinkNamespaceApprovalIssue(),
            };

            foreach (var mock in mockInstances)
            {
                Mocks[mock.ToolName] = mock;
            }
        }

        public static IToolMock GetToolMock(string toolName)
        {
            if (Mocks.TryGetValue(toolName, out var mock))
            {
                return mock;
            }
            throw new ArgumentException($"No mock found for tool: {toolName}");
        }

        public static IEnumerable<IToolMock> GetToolMocks(IEnumerable<string> toolNames)
        {
            var tools = new List<IToolMock>();
            var missing = new List<string>();
            foreach (var toolName in toolNames)
            {
                if (Mocks.TryGetValue(toolName, out var mock))
                {
                    tools.Add(mock);
                }
                else
                {
                    missing.Add(toolName);
                }
            }

            if(tools.Count == toolNames.Count())
            {
                return tools;
            }

            throw new ArgumentException($"No mock found for tools: {string.Join(", ", missing)}");
        }
    }
}
