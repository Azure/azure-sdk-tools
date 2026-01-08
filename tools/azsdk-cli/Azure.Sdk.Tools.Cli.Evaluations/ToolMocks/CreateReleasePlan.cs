using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Evaluations.ToolMocks
{
    public class CreateReleasePlan : IToolMock
    {
        public string ToolName => "azsdk_create_release_plan";
        public string CallId => "tooluse_l1vP7asdasdsadasd543gf";
        private string ToolResult => """{"message":"Release plan is being created","result":{"workItemId":29262,"workItemUrl":"","workItemHtmlUrl":"","serviceTreeId":"a7f2b8e4-9c1d-4a3e-b6f9-2d8e5a7c3b1f","productTreeId":"f1a8c5d2-6e4b-4f7a-9c2d-8b5e1f3a6c9e","productName":"","title":"","description":"","owner":"","status":"","specPullRequests":["https://github.com/Azure/azure-rest-api-specs/pull/38387"],"sdkReleaseMonth":"December 2025","isManagementPlane":false,"isDataPlane":true,"specAPIVersion":"2022-11-01-preview","specType":"TypeSpec","releasePlanLink":"","isTestReleasePlan":false,"releasePlanId":0,"sdkReleaseType":"beta","sdkInfo":[],"releasePlanSubmittedByEmail":"x@microsoft.com","isCreatedByAgent":true,"activeSpecPullRequest":"","sdkLanguages":"","isSpecApproved":false,"apiSpecWorkItemId":0,"languageExclusionRequesterNote":"","languageExclusionApproverNote":""},"next_steps":["Get release plan from \u0060workItem\u0060, work item value: 29262"]}""";

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
                            { "typeSpecProjectPath", "c:\\azure-rest-api-specs\\specification\\contosowidgetmanager\\Contoso.WidgetManager" },
                            { "targetReleaseMonthYear", "December 2025" },
                            { "serviceTreeId", "a7f2b8e4-9c1d-4a3e-b6f9-2d8e5a7c3b1f" },
                            { "productTreeId", "f1a8c5d2-6e4b-4f7a-9c2d-8b5e1f3a6c9e" },
                            { "specApiVersion", "2022-11-01-preview" },
                            { "specPullRequestUrl", "https://github.com/Azure/azure-rest-api-specs/pull/38387" },
                            { "sdkReleaseType", "beta" },
                            { "isTestReleasePlan", false }
                        }
                    )
                ]
            );
        }
    }
}
