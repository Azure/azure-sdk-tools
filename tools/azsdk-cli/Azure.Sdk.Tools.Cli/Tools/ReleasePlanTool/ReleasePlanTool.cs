using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlanTool
{

    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public class ReleasePlanTool : MCPHubTool
    {
        [McpServerTool, Description("Get release plan for a service, product and API spec pull request")]
        public async Task<List<string>> GetReleasePlan(string serviceTreeId, string productTreeId, string pullRequestLink)
        {
            // todo: once we get buy in from benbp/wesh we should move tools/mcp/dotnet/AzureSDKDevToolsMCP/Tools/ReleasePlanTool.cs here.
            // todo: same for `SpecPullRequestTool.cs` and `SpecValidationTool.cs`

            return new List<string> { "Hello", "World" };
        }
    }
}
