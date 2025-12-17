using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class PackageGenerateCode : IToolMock
    {
        public string ToolName => "azsdk_package_generate_code";
        public string CallId => "tooluse_l1vP7afmgopmnhgmjp";
        private string ToolResult => """{"message":"SDK generation completed successfully using tspconfig.yaml.","result":"succeeded","language":"DotNet","package_name":"","version":"","package_type":"Unknown","sdk_repo":"azure-sdk-for-net","next_steps":["If the SDK is not Python, build the code"],"operation_status":"Succeeded"}""";
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
                            { "tspConfigPath","C:\\azure-rest-api-specs\\specification\\healthdataaiservices\\HealthDataAIServices.DeidServices\\tspconfig.yaml" },
                            { "localSdkRepoPath", "C:\\azure-sdk-for-net" },
                            { "tspLocationPath", "" },
                            { "emitterOptions", "" }
                        }
                    )
                ]
            );
        }
    }
}
