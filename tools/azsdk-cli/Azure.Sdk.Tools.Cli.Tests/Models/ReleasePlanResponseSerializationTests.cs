// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class ReleasePlanResponseSerializationTests
{
    private static ReleasePlanWorkItem CreatePlan(int releasePlanId = 50001) => new()
    {
        WorkItemId = 35000,
        WorkItemUrl = "https://dev.azure.com/example/Release/_apis/wit/workItems/35000",
        WorkItemHtmlUrl = "https://dev.azure.com/example/Release/_workitems/edit/35000",
        ParentId = 34999,
        ApiSpecWorkItemId = 35001,
        Description = "Internal work-item description",
        Tag = "Internal work-item tags",
        ReleasePlanSubmittedByEmail = "notification-only@example.com",
        ReleasePlanId = releasePlanId,
        Title = "Contoso release",
        Status = "In Progress",
        SDKReleaseMonth = "December 2026",
        APISpecProjectPath = "specification/contoso/Contoso.Widget",
        ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/12345",
        SDKInfo =
        [
            new SDKInfo
            {
                Language = "Python",
                PackageName = "azure-contoso-widget",
                GenerationPipelineUrl = "https://dev.azure.com/example/internal/_build/results?buildId=123",
                SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/12345"
            }
        ]
    };

    private static void AssertNoStorageDetails(string json)
    {
        foreach (var field in new[] { "WorkItemId", "WorkItemUrl", "WorkItemHtmlUrl", "ParentId", "ApiSpecWorkItemId", "ReleasePlanSubmittedByEmail" })
        {
            Assert.That(json, Does.Not.Contain($"\"{field}\""));
        }
        Assert.That(json, Does.Not.Contain("_workitems/edit"));
        Assert.That(json, Does.Not.Contain("_apis/wit/workItems"));
        Assert.That(json, Does.Not.Contain("notification-only@example.com"));
        Assert.That(json, Does.Not.Contain("Internal work-item"));
    }

    [Test]
    public void McpResponseProjectsOnlyPublicPlanAndSdkFields()
    {
        var plan = CreatePlan();
        var response = new ReleasePlanResponse { ReleasePlanDetails = plan };
        var json = new OutputHelper(OutputHelper.OutputModes.Mcp).Format(response);
        using var document = JsonDocument.Parse(json);
        var details = document.RootElement.GetProperty("release_plan_details");

        AssertNoStorageDetails(json);
        Assert.Multiple(() =>
        {
            Assert.That(details.GetProperty("ReleasePlanId").GetInt32(), Is.EqualTo(50001));
            Assert.That(details.GetProperty("ReleasePlanLink").GetString(), Is.EqualTo(plan.ReleasePlanLink));
            Assert.That(details.GetProperty("APISpecProjectPath").GetString(), Is.EqualTo(plan.APISpecProjectPath));
            Assert.That(details.GetProperty("SDKInfo")[0].GetProperty("PackageName").GetString(), Is.EqualTo("azure-contoso-widget"));
            Assert.That(json, Does.Contain("_build/results?buildId=123"));
            Assert.That(json, Does.Contain("azure-sdk-for-python/pull/12345"));
            Assert.That(response.ReleasePlanDetails.WorkItemId, Is.EqualTo(35000), "Internal C# callers retain the backing model.");
        });
    }

    [Test]
    public void ListResponsesUseTheSamePublicProjection()
    {
        var response = new ReleasePlanListResponse { ReleasePlanDetailsList = [CreatePlan()] };
        var json = new OutputHelper(OutputHelper.OutputModes.Mcp).Format(response);
        using var document = JsonDocument.Parse(json);

        AssertNoStorageDetails(json);
        Assert.That(document.RootElement.GetProperty("release_plans")[0].GetProperty("ReleasePlanId").GetInt32(), Is.EqualTo(50001));
        Assert.That(response.ToString(), Does.Contain("releaseplan=50001"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LegacyPlanUsesOneConsistentDashboardId(bool isTest)
    {
        var plan = CreatePlan(releasePlanId: 0);
        plan.IsTestReleasePlan = isTest;
        var response = new ReleasePlanResponse { ReleasePlanDetails = plan };
        var json = JsonSerializer.Serialize(response);
        using var document = JsonDocument.Parse(json);
        var details = document.RootElement.GetProperty("release_plan_details");

        AssertNoStorageDetails(json);
        Assert.That(details.GetProperty("ReleasePlanId").GetInt32(), Is.EqualTo(35000));
        Assert.That(details.GetProperty("ReleasePlanLink").GetString(),
            Is.EqualTo($"{(isTest ? ReleasePlanWorkItem.DashboardBaseUrlTest : ReleasePlanWorkItem.DashboardBaseUrl)}35000"));
        Assert.That(response.ToString(), Does.Contain("Release Plan ID: 35000"));
    }

    [Test]
    public void MissingPlanIsStillOmittedFromJson()
    {
        Assert.That(JsonSerializer.Serialize(new ReleasePlanResponse()), Does.Not.Contain("release_plan_details"));
        Assert.That(JsonSerializer.Serialize(new ReleasePlanListResponse()), Does.Not.Contain("release_plans"));
    }

    [Test]
    public void CapabilitiesAreStructuredAndShownInCliOutput()
    {
        var response = new ReleasePlanResponse
        {
            ReleasePlanDetails = CreatePlan(),
            Capabilities = new() { CanAbandon = false, Reason = "Administrator access is required." }
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));

        Assert.That(document.RootElement.GetProperty("capabilities").GetProperty("can_abandon").GetBoolean(), Is.False);
        Assert.That(response.ToString(), Does.Contain("Administrator access is required."));
    }

    [Test]
    public void OtherWorkItemModelsKeepTheirExistingSerialization()
    {
        Assert.That(JsonSerializer.Serialize(new WorkItemBase { WorkItemId = 35000 }), Does.Contain("\"WorkItemId\":35000"));
    }
}