using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class PackageGenerateCode : IToolMock
    {
        public string ToolName => "azsdk_package_generate_code";
        public string CallId => "tooluse_generate_code_01";
        private string ToolResult => """{"output_path":"/sdk/contosowidgetmanager/azure-contoso-widget","operation_status":"Succeeded"}""";
        
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
