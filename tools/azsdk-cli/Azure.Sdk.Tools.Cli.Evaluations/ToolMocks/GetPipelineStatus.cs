using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class GetPipelineStatus : IToolMock
    {
        public string ToolName => "azsdk_get_pipeline_status";
        public string CallId => "tooluse_pipeline_status_01";
        private string ToolResult => """{"pipeline_name":"azure-sdk-for-net - ci","status":"succeeded","branch":"main","build_id":"12345"}""";
        
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
                            { "pipelineUrl", "https://dev.azure.com/azure-sdk/internal/_build?definitionId=1234" },
                        }
                    )
                ]
            );
        }
    }
}
