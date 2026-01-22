using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class GetGithubUserDetails : IToolMock
    {
        public string ToolName => "azsdk_get_github_user_details";
        public string CallId => "tooluse_github_user_01";
        private string ToolResult => """{"username":"testuser","name":"Test User","email":"testuser@microsoft.com","organization":"Azure","operation_status":"Succeeded"}""";
        
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
                            { "username", "testuser" },
                        }
                    )
                ]
            );
        }
    }
}
