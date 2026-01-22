using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class PackageRunCheck : IToolMock
    {
        public string ToolName => "azsdk_package_run_check";
        public string CallId => "tooluse_package_check_01";
        private string ToolResult => """{"results":[],"warnings":[],"operation_status":"Succeeded"}""";
        
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
                            { "packagePath", "/sdk/storage/azure-storage-blob" },
                            { "language", "python" },
                        }
                    )
                ]
            );
        }
    }
}
