using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class TypespecCheckProjectInPublicRepo : IToolMock
    {
        public string ToolName => "azsdk_typespec_check_project_in_public_repo";
        public string CallId => "tooluse_K5K9CrcGRX62NdDXOliWMA"; 
        private string ToolResult => """{"result": "true"}""";

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
                            { "typeSpecProjectPath", "C:\\Users\\juanospina\\source\\repos\\azure-rest-api-specs\\specification\\contosowidgetmanager\\Contoso.WidgetManager" }
                        }
                    )
                ]
            );
        }
    }
}
