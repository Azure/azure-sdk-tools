using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class CreatePullRequest : IToolMock
    {
        public string ToolName => "azsdk_create_pull_request";
        public string CallId => "tooluse_l1vP7afmgopxyd543gf";
        private string ToolResult => """{"result":"No pull request found for branch jeo02:testing-contoso-sdk-gen in repository Azure/azure-rest-api-specs. Proceeding to create a new pull request.\r\nChecking if changes are mergeable to main branch in repository [Azure/azure-rest-api-specs]...\r\nChanges are mergeable. Proceeding to create pull request for changes in jeo02:testing-contoso-sdk-gen.\r\nPull request created successfully as draft PR.\r\nOnce you have successfully generated the SDK transition the PR to review ready.\r\nPull request created successfully."}""";

        public ChatMessage GetMockResponse(string callid)
        {
            return new ChatMessage(
                ChatRole.Tool,
                [
                    new FunctionResultContent(
                        callid,
                        ToolResult
                    )
                ]
            );
        }

        public ChatMessage GetMockCall()
        {
            return new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        CallId,
                        ToolName,
                        new Dictionary<string, object?>
                        {
                            { "description", "This PR adds the TypeSpec specification for Contoso Widget Manager service.\n\n## Changes\n- Added TypeSpec project for Contoso Widget Manager\n- Includes management plane API definitions\n- TypeSpec validation has been completed successfully\n\n## Review Notes\n- This is a new service specification\n- All TypeSpec validation checks have passed\n- Ready for API design review" },
                            { "repoPath", "c:\\Users\\juanospina\\source\\repos\\azure-rest-api-specs" },
                            { "title", "Add Contoso Widget Manager TypeSpec specification" },
                            { "draft", true }
                        }
                    )
                ]
            );
        }
    }
}
