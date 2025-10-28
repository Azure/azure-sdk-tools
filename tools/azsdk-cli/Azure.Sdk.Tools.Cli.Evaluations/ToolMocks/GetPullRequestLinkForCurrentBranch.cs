using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.McpEvals.ToolMocks
{
    public class GetPullRequestLinkForCurrentBranch : IToolMock
    {
        public string ToolName => "azsdk_get_pull_request_link_for_current_branch";
        public string CallId => "tooluse_l1vP7lx3RwCft6416a51sfd";
        private string ToolResult => """{"result": []}""";

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
                            { "repoRootPath", "C:\\Users\\juanospina\\source\\repos\\azure-rest-api-specs" }
                        }
                    )
                ]
            );
        }
    }
}
