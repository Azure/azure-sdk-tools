using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class VerifySetup : IToolMock
    {
        public string ToolName => "azsdk_verify_setup";
        public string CallId => "tooluse_l1vP7afmgopmnhgmgv";
        private string ToolResult => """{"results":[],"operation_status":"Succeeded"}""";
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
                            { "packagePath", "C:/azure-rest-api-specs" },
                        }
                    )
                ]
            );
        }
    }
}
