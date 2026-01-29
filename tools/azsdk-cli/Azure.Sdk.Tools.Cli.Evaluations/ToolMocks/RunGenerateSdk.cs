using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class RunGenerateSdk : IToolMock
    {
        public string ToolName => "azsdk_run_generate_sdk";
        public string CallId => "tooluse_run_generate_01";
        private string ToolResult => """{"pipeline_url":"https://dev.azure.com/azure-sdk/internal/_build/results?buildId=12345","operation_status":"Succeeded"}""";
        
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
                            { "specRepoPath", "/azure-rest-api-specs" },
                            { "typespecProject", "specification/contosowidgetmanager/Contoso.WidgetManager" },
                            { "language", "python" },
                        }
                    )
                ]
            );
        }
    }
}
