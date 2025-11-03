using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class GetModifiedTypespecProjects : IToolMock
    {
        public string ToolName => "azsdk_get_modified_typespec_projects";
        public string CallId => "tooluse_l1vP7lx3RwCftg6B33Gcbw";
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
                            { "repoRootPath", "C:\\azure-rest-api-specs" }
                        }
                    )
                ]
            );
        }
    }
}
