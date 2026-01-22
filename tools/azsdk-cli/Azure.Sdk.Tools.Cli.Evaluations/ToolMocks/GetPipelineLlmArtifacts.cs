using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class GetPipelineLlmArtifacts : IToolMock
    {
        public string ToolName => "azsdk_get_pipeline_llm_artifacts";
        public string CallId => "tooluse_pipeline_artifacts_01";
        private string ToolResult => """{"artifacts":[{"name":"test-results","url":"https://dev.azure.com/artifacts/123"}],"operation_status":"Succeeded"}""";
        
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
                            { "pipelineUrl", "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=12345" },
                        }
                    )
                ]
            );
        }
    }
}
