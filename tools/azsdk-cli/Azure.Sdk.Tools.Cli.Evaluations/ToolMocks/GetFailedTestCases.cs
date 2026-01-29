using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class GetFailedTestCases : IToolMock
    {
        public string ToolName => "azsdk_get_failed_test_cases";
        public string CallId => "tooluse_failed_tests_01";
        private string ToolResult => """{"failed_tests":[{"name":"TestMethod1","error":"Assert.AreEqual failed"}],"total_failures":1,"operation_status":"Succeeded"}""";
        
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
