using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class LinkNamespaceApprovalIssue : IToolMock
    {
        public string ToolName => "azsdk_link_namespace_approval_issue";
        public string CallId => "tooluse_link_namespace_001";
        private string toolResult => """{"result":"Linked namespace approval issue"}""";

        public ChatMessage GetMockResponse(string callid)
        {
            return new ChatMessage(
                ChatRole.Tool,
                [
                    new FunctionResultContent(
                        callid,
                        toolResult
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
                            { "releasePlanWorkItemId", 12345 },
                            { "namespaceApprovalIssue", "https://github.com/Azure/azure-sdk/issues/1234" }
                        }
                    )
                ]
            );
        }
    }
}
