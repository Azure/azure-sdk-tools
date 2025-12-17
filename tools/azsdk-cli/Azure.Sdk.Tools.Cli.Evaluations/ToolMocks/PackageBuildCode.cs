using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class PackageBuildCode : IToolMock
    {
        public string ToolName => "azsdk_package_build_code";
        public string CallId => "tooluse_l1vP7afmgopmnhjpjp";
        private string ToolResult => """{"message":"Build completed successfully.","result":"succeeded","language":"DotNet","package_name":"Azure.Health.Deidentification","version":"1.1.0-beta.2","package_type":"Unknown","sdk_repo":"","next_steps":[],"operation_status":"Succeeded"}""";
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
                            { "packagePath", "C:\\azure-sdk-for-net\\sdk\\healthdataaiservices\\Azure.Health.Deidentification" },
                        }
                    )
                ]
            );
        }
    }
}
