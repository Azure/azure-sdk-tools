// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

internal class ReleasePlanResponseSerializationTests
{
    [TestCase(OutputHelper.OutputModes.Json, false)]
    [TestCase(OutputHelper.OutputModes.Json, true)]
    [TestCase(OutputHelper.OutputModes.Mcp, false)]
    [TestCase(OutputHelper.OutputModes.Mcp, true)]
    public void ReleasePlanOutput_OmitsSdkPrStatusButPreservesDetails(OutputHelper.OutputModes outputMode, bool listResponse)
    {
        var releasePlan = new ReleasePlanWorkItem
        {
            ReleasePlanId = 77,
            Status = "In Progress",
            SDKInfo = [CreateSdkInfo()]
        };
        CommandResponse response = listResponse
            ? new ReleasePlanListResponse { ReleasePlanDetailsList = [releasePlan] }
            : new ReleasePlanResponse { ReleasePlanDetails = releasePlan };

        var json = new OutputHelper(outputMode).Format(response);
        using var document = JsonDocument.Parse(json);
        var details = listResponse
            ? document.RootElement.GetProperty("release_plans")[0]
            : document.RootElement.GetProperty("release_plan_details");
        var sdk = details.GetProperty("SDKInfo")[0];

        Assert.That(sdk.TryGetProperty("PullRequestStatus", out _), Is.False);
        Assert.That(sdk.GetProperty("Language").GetString(), Is.EqualTo("Python"));
        Assert.That(sdk.GetProperty("PackageName").GetString(), Is.EqualTo("azure-mgmt-contoso"));
        Assert.That(sdk.GetProperty("SdkPullRequestUrl").GetString(), Is.EqualTo(releasePlan.SDKInfo[0].SdkPullRequestUrl));
        Assert.That(sdk.GetProperty("GenerationPipelineUrl").GetString(), Is.EqualTo(releasePlan.SDKInfo[0].GenerationPipelineUrl));
        Assert.That(sdk.GetProperty("GenerationStatus").GetString(), Is.EqualTo("Failed"));
        Assert.That(sdk.GetProperty("ReleaseStatus").GetString(), Is.EqualTo("Pending"));
        Assert.That(sdk.GetProperty("ReleaseExclusionStatus").GetString(), Is.EqualTo("Not applicable"));
        Assert.That(details.GetProperty("Status").GetString(), Is.EqualTo("In Progress"));
        Assert.That(details.GetProperty("ReleasePlanLink").GetString(), Is.EqualTo(releasePlan.ReleasePlanLink));
        Assert.That(releasePlan.SDKInfo[0].PullRequestStatus, Is.EqualTo("Merged"), "Serialization must not clear the status used internally for release-plan selection.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SdkInfoSerialization_OmitsPrStatusWithDefaultAndWebOptions(bool webOptions)
    {
        var sdkInfo = CreateSdkInfo();
        var options = webOptions ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : new JsonSerializerOptions();

        var json = JsonSerializer.Serialize(sdkInfo, options);
        using var document = JsonDocument.Parse(json);

        Assert.That(document.RootElement.EnumerateObject().Any(property => property.Name.Equals("PullRequestStatus", StringComparison.OrdinalIgnoreCase)), Is.False);
        Assert.That(sdkInfo.PullRequestStatus, Is.EqualTo("Merged"));
    }

    [Test]
    public async Task McpToolResult_OmitsSdkPrStatus()
    {
        var sdkInfo = CreateSdkInfo();
        var releasePlan = new ReleasePlanWorkItem { ReleasePlanId = 77, SDKInfo = [sdkInfo] };
        var response = new ReleasePlanResponse { ReleasePlanDetails = releasePlan };
        var tool = McpServerTool.Create((Func<ReleasePlanResponse>)(() => response), new McpServerToolCreateOptions { Name = "test_release_plan_response" });
        var request = new RequestContext<CallToolRequestParams>(Mock.Of<McpServer>(), new JsonRpcRequest { Id = new RequestId(1), Method = "tools/call" })
        {
            Params = new CallToolRequestParams { Name = tool.ProtocolTool.Name },
            Services = Mock.Of<IServiceProvider>()
        };

        var result = await tool.InvokeAsync(request, CancellationToken.None);

        Assert.That(result.IsError, Is.Not.True);
        using var document = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
        var sdk = document.RootElement.GetProperty("release_plan_details").GetProperty("sdkInfo")[0];
        Assert.That(sdk.EnumerateObject().Any(property => property.Name.Equals("PullRequestStatus", StringComparison.OrdinalIgnoreCase)), Is.False);
        Assert.That(sdk.GetProperty("sdkPullRequestUrl").GetString(), Is.EqualTo(sdkInfo.SdkPullRequestUrl));
        Assert.That(sdk.GetProperty("generationStatus").GetString(), Is.EqualTo("Failed"));
        Assert.That(sdk.GetProperty("releaseStatus").GetString(), Is.EqualTo("Pending"));
        Assert.That(document.RootElement.GetProperty("release_plan_link").GetString(), Is.EqualTo(releasePlan.ReleasePlanLink));
        Assert.That(sdkInfo.PullRequestStatus, Is.EqualTo("Merged"));
    }

    [Test]
    public void PlainReleasePlanOutput_DoesNotExposeSdkPrStatus()
    {
        var releasePlan = new ReleasePlanWorkItem { ReleasePlanId = 77, SDKInfo = [CreateSdkInfo()] };

        var output = new OutputHelper(OutputHelper.OutputModes.Plain).Format(new ReleasePlanResponse { ReleasePlanDetails = releasePlan });

        Assert.That(output, Does.Not.Contain("PullRequestStatus").And.Not.Contain("Merged"));
        Assert.That(output, Does.Contain(releasePlan.ReleasePlanLink));
        Assert.That(releasePlan.SDKInfo[0].PullRequestStatus, Is.EqualTo("Merged"));
    }

    private static SDKInfo CreateSdkInfo() => new()
    {
        Language = "Python",
        PackageName = "azure-mgmt-contoso",
        SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/1",
        GenerationPipelineUrl = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=1",
        GenerationStatus = "Failed",
        ReleaseStatus = "Pending",
        PullRequestStatus = "Merged",
        ReleaseExclusionStatus = "Not applicable"
    };
}