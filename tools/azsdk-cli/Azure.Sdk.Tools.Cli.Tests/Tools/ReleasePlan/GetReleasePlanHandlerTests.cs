// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias MockServer;

using System.Reflection;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using GetReleasePlanHandler = MockServer::Azure.Sdk.Tools.Mock.Handlers.ReleasePlan.GetReleasePlanHandler;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan;

internal class GetReleasePlanHandlerTests
{
    private static readonly string[] RequiredDataPlaneLanguages = [".NET", "Java", "Python", "JavaScript"];
    private readonly GetReleasePlanHandler _handler = new();

    [TestCase(35010, 50010)]
    [TestCase(35011, 50011)]
    [TestCase(35012, 50012)]
    [TestCase(35013, 50013)]
    public void LifecycleFixturesResolveByWorkItemAndReleasePlanId(int workItemId, int releasePlanId)
    {
        foreach (var (key, value) in new[] { ("workItemId", workItemId), ("workItem", workItemId), ("releasePlanId", releasePlanId) })
        {
            var response = GetPlan(key, value);
            Assert.Multiple(() =>
            {
                Assert.That(response.ReleasePlanDetails!.WorkItemId, Is.EqualTo(workItemId));
                Assert.That(response.ReleasePlanDetails.ReleasePlanId, Is.EqualTo(releasePlanId));
                Assert.That(response.ReleasePlanDetails.IsDataPlane, Is.True);
                Assert.That(response.ReleasePlanDetails.SDKInfo.Select(sdk => sdk.Language),
                    Is.EquivalentTo(new[] { ".NET", "JavaScript", "Python", "Java", "Go" }));
            });
        }
    }

    [TestCase(35010, false)]
    [TestCase(35011, true)]
    public void BeforeGenerationHasLanguageEntriesWithEmptyGenerationState(int workItemId, bool isSpecApproved)
    {
        var plan = GetPlan("workItemId", workItemId).ReleasePlanDetails!;

        Assert.Multiple(() =>
        {
            Assert.That(plan.IsSpecApproved, Is.EqualTo(isSpecApproved));
            Assert.That(plan.SDKInfo, Is.Not.Empty);
            Assert.That(plan.SDKInfo.All(sdk => string.IsNullOrEmpty(sdk.GenerationStatus)
                && string.IsNullOrEmpty(sdk.GenerationPipelineUrl)
                && string.IsNullOrEmpty(sdk.SdkPullRequestUrl)
                && string.IsNullOrEmpty(sdk.PullRequestStatus)
                && string.IsNullOrEmpty(sdk.ReleaseStatus)), Is.True);
            Assert.That(IsReleasePlanComplete(plan), Is.False);
        });
    }

    [Test]
    public void GeneratedFixtureHasOpenUnreleasedPullRequestsForEveryRequiredLanguage()
    {
        var plan = GetPlan("workItemId", 35012).ReleasePlanDetails!;
        var requiredSdks = plan.SDKInfo.Where(sdk => RequiredDataPlaneLanguages.Contains(sdk.Language)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(requiredSdks.Select(sdk => sdk.Language), Is.EquivalentTo(RequiredDataPlaneLanguages));
            Assert.That(requiredSdks.All(sdk => sdk.GenerationStatus == "Completed"
                && sdk.PullRequestStatus == "Open" && sdk.ReleaseStatus == "Unreleased"
                && !string.IsNullOrEmpty(sdk.SdkPullRequestUrl) && !string.IsNullOrEmpty(sdk.PackageName)), Is.True);
            Assert.That(IsReleasePlanComplete(plan), Is.False);
        });
    }

    [Test]
    public void ReleasedFixtureSatisfiesProductionCompletionRule()
    {
        var plan = GetPlan("workItemId", 35013).ReleasePlanDetails!;
        var requiredSdks = plan.SDKInfo.Where(sdk => RequiredDataPlaneLanguages.Contains(sdk.Language)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(requiredSdks.Select(sdk => sdk.Language), Is.EquivalentTo(RequiredDataPlaneLanguages));
            Assert.That(requiredSdks.All(sdk => sdk.ReleaseStatus == "Released" && sdk.PullRequestStatus == "Merged"), Is.True);
            Assert.That(IsReleasePlanComplete(plan), Is.True);
        });

        plan.SDKInfo.RemoveAll(sdk => sdk.Language == ".NET");
        Assert.That(IsReleasePlanComplete(plan), Is.False);
    }

    [TestCase("workItemId", "35000")]
    [TestCase("workItem", "35000")]
    [TestCase("releasePlanId", "50001")]
    [TestCase("typeSpecProjectPath", "specification/contosowidgetmanager/Contoso.WidgetManager")]
    [TestCase("specPullRequestUrl", "https://github.com/Azure/azure-rest-api-specs/pull/38387")]
    public void ExistingContosoLookupsKeepWarningsAndNextSteps(string key, string value)
    {
        var response = GetPlan(key, value);

        Assert.Multiple(() =>
        {
            Assert.That(response.ReleasePlanDetails!.WorkItemId, Is.EqualTo(35000));
            Assert.That(response.Warnings, Is.Not.Empty);
            Assert.That(response.NextSteps, Is.Not.Empty);
            Assert.That(response.ReleasePlanDetails.ActiveSpecPullRequest, Is.EqualTo(key == "specPullRequestUrl"
                ? value : "https://github.com/Azure/azure-rest-api-specs/pull/12345"));
        });
    }

    [Test]
    public void UnknownLookupKeepsDefaultResponse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_handler.Handle(null), Is.TypeOf<DefaultCommandResponse>());
            Assert.That(_handler.Handle(new() { ["workItemId"] = 99999 }), Is.TypeOf<DefaultCommandResponse>());
            Assert.That(_handler.Handle(new()
            {
                ["typeSpecProjectPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager",
                ["apiReleaseType"] = "Public Preview"
            }), Is.TypeOf<DefaultCommandResponse>());
        });
    }

    private static bool IsReleasePlanComplete(ReleasePlanWorkItem plan)
    {
        var method = typeof(PackageReleaseStatusTool).GetMethod("IsReleasePlanComplete", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return (bool)method!.Invoke(null, new object[] { plan })!;
    }

    private ReleasePlanResponse GetPlan(string key, object value)
    {
        var response = _handler.Handle(new() { [key] = JsonSerializer.SerializeToElement(value) });
        Assert.That(response, Is.TypeOf<ReleasePlanResponse>());
        return (ReleasePlanResponse)response;
    }
}