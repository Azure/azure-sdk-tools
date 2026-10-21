// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Mock.Tests;

[TestFixture(false)]
[TestFixture(true)]
public class ReleasePlanGenerationFixtureTests
{
    private const string ProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager";
    private readonly bool _wireArguments;

    public ReleasePlanGenerationFixtureTests(bool wireArguments)
    {
        _wireArguments = wireArguments;
    }

    [TestCase("workItemId", 35000)]
    [TestCase("workItem", 35000)]
    [TestCase("releasePlanId", 50001)]
    public void Get_CanonicalIdsRemainDistinctAndKeepPastDueWarning(string selector, int id)
    {
        var response = (ReleasePlanResponse)new GetReleasePlanHandler().Handle(Arguments(new() { [selector] = id }));

        Assert.Multiple(() =>
        {
            Assert.That(response.ReleasePlanDetails!.WorkItemId, Is.EqualTo(35000));
            Assert.That(response.ReleasePlanDetails.ReleasePlanId, Is.EqualTo(50001));
            Assert.That(response.ReleasePlanDetails.ApiReleaseType, Is.EqualTo(ApiReleaseType.GA));
            Assert.That(string.Join(" ", response.Warnings ?? []), Does.Contain("49999").And.Contain("May 2026"));
        });
    }

    [TestCase("path")]
    [TestCase("pr")]
    [TestCase("apiVersion")]
    public void Get_PreservesCanonicalNonIdSelectors(string selector)
    {
        var arguments = new Dictionary<string, object?>();
        if (selector == "pr")
        {
            arguments["specPullRequestUrl"] = "https://github.com/Azure/azure-rest-api-specs/pull/38387";
        }
        else
        {
            arguments["typeSpecProjectPath"] = ProjectPath;
        }
        if (selector == "apiVersion")
        {
            arguments["apiReleaseType"] = "GA";
            arguments["apiVersion"] = "2024-01-01";
        }

        var response = (ReleasePlanResponse)new GetReleasePlanHandler().Handle(Arguments(arguments));

        Assert.That(response.ReleasePlanDetails!.WorkItemId, Is.EqualTo(35000));
        if (selector == "pr")
        {
            Assert.That(response.ReleasePlanDetails.ActiveSpecPullRequest, Is.EqualTo(arguments["specPullRequestUrl"]));
        }
    }

    [TestCase("workItemId", 35001)]
    [TestCase("releasePlanId", 50002)]
    public void Get_PrivatePreviewHasItsOwnIdsAndPrivateSpecRepository(string selector, int id)
    {
        var response = (ReleasePlanResponse)new GetReleasePlanHandler().Handle(Arguments(new() { [selector] = id }));

        Assert.Multiple(() =>
        {
            Assert.That(response.ReleasePlanDetails!.WorkItemId, Is.EqualTo(35001));
            Assert.That(response.ReleasePlanDetails.ReleasePlanId, Is.EqualTo(50002));
            Assert.That(response.ReleasePlanDetails.ApiReleaseType, Is.EqualTo(ApiReleaseType.PrivatePreview));
            Assert.That(response.ReleasePlanDetails.ActiveSpecPullRequest, Does.StartWith("https://github.com/Azure/azure-rest-api-specs-pr/pull/"));
            Assert.That(response.ReleasePlanDetails.SDKInfo, Is.Empty);
        });
    }

    [TestCase("workItemId", 29262)]
    [TestCase("releasePlanId", 29262)]
    public void Get_UnknownIdCannotFallBackToCanonicalPathOrPr(string selector, int id)
    {
        var response = new GetReleasePlanHandler().Handle(Arguments(new()
        {
            [selector] = id,
            ["typeSpecProjectPath"] = ProjectPath,
            ["specPullRequestUrl"] = "https://github.com/Azure/azure-rest-api-specs/pull/38387"
        }));

        Assert.That(response, Is.Not.InstanceOf<ReleasePlanResponse>());
    }

    [TestCase("specPullRequestUrl", "https://github.com/Azure/azure-rest-api-specs-pr/pull/12346")]
    [TestCase("typeSpecProjectPath", "specification/unknown/Unknown")]
    [TestCase("typeSpecProjectPath", ProjectPath)]
    public void Get_NonIdSelectorsCannotResolvePrivatePreview(string selector, string value)
    {
        var response = new GetReleasePlanHandler().Handle(Arguments(new()
        {
            [selector] = value,
            ["apiReleaseType"] = "Private Preview"
        }));

        Assert.That(response, Is.Not.InstanceOf<ReleasePlanResponse>());
    }

    [Test]
    public void Get_UnknownApiVersionDoesNotResolveCanonicalPlan()
    {
        var response = new GetReleasePlanHandler().Handle(Arguments(new()
        {
            ["typeSpecProjectPath"] = ProjectPath,
            ["apiReleaseType"] = "GA",
            ["apiVersion"] = "2099-01-01"
        }));

        Assert.That(response, Is.Not.InstanceOf<ReleasePlanResponse>());
    }

    [TestCase(35000, 50002)]
    [TestCase(35001, 50001)]
    public void Get_ConflictingPlanIdsDoNotSelectAnotherPlan(int workItemId, int releasePlanId)
    {
        var response = new GetReleasePlanHandler().Handle(Arguments(new()
        {
            ["workItemId"] = workItemId,
            ["releasePlanId"] = releasePlanId
        }));

        Assert.That(response, Is.Not.InstanceOf<ReleasePlanResponse>());
    }

    [TestCase(35000, "beta", null)]
    [TestCase(50001, "beta", "")]
    [TestCase(35000, "beta", "2024-01-01-preview")]
    [TestCase(50001, "stable", null)]
    [TestCase(35000, "stable", "2024-01-01")]
    public void Generate_QueuesCanonicalPlanWithOptionalApiVersion(int id, string releaseType, string? apiVersion)
    {
        var arguments = GenerationArguments(id, "Python");
        arguments["sdkReleaseType"] = releaseType;
        if (apiVersion != null)
        {
            arguments["apiVersion"] = apiVersion;
        }
        var response = (ReleaseWorkflowResponse)new RunGenerateSdkHandler().Handle(Arguments(arguments));
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(response));

        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("Queued"));
            Assert.That(response.ResponseErrors, Is.Empty);
            Assert.That(response.TypeSpecProject, Is.EqualTo(ProjectPath));
            Assert.That(wire.RootElement.GetProperty("status").GetString(), Is.EqualTo("Queued"));
            Assert.That(wire.RootElement.GetProperty("language").GetString(), Is.EqualTo("Python"));
            Assert.That(wire.RootElement.GetProperty("details").ToString(), Does.Contain("90001"));
        });
    }

    [TestCase(".NET", SdkLanguage.DotNet)]
    [TestCase("Python", SdkLanguage.Python)]
    [TestCase("JavaScript", SdkLanguage.JavaScript)]
    [TestCase("Java", SdkLanguage.Java)]
    [TestCase("Go", SdkLanguage.Go)]
    public void Generate_EchoesSupportedLanguage(string language, SdkLanguage expected)
    {
        var response = (ReleaseWorkflowResponse)new RunGenerateSdkHandler().Handle(Arguments(GenerationArguments(35000, language)));

        Assert.That(response.Status, Is.EqualTo("Queued"));
        Assert.That(response.Language, Is.EqualTo(expected));
    }

    [TestCase("workItemId", null)]
    [TestCase("workItemId", 0)]
    [TestCase("workItemId", -1)]
    [TestCase("workItemId", 29262)]
    [TestCase("typespecProjectRoot", "specification/unknown/Unknown")]
    [TestCase("typespecProjectRoot", null)]
    [TestCase("sdkReleaseType", "preview")]
    [TestCase("sdkReleaseType", null)]
    [TestCase("language", "Rust")]
    [TestCase("language", null)]
    public void Generate_InvalidInputsFailWithoutQueueing(string key, object? value)
    {
        var arguments = GenerationArguments(35000, "Python");
        if (value == null)
        {
            arguments.Remove(key);
        }
        else
        {
            arguments[key] = value;
        }
        AssertNotQueued((ReleaseWorkflowResponse)new RunGenerateSdkHandler().Handle(Arguments(arguments)), "Failed");
    }

    [Test]
    public void Generate_StableFromExplicitPreviewFails()
    {
        var arguments = GenerationArguments(35000, "Python");
        arguments["sdkReleaseType"] = "stable";
        arguments["apiVersion"] = "2024-01-01-preview";

        AssertNotQueued((ReleaseWorkflowResponse)new RunGenerateSdkHandler().Handle(Arguments(arguments)), "Failed");
    }

    [TestCase(35001)]
    [TestCase(50002)]
    public void Generate_PrivatePreviewReturnsGuidanceBeforeGenerationValidation(int id)
    {
        var response = (ReleaseWorkflowResponse)new RunGenerateSdkHandler().Handle(Arguments(new() { ["workItemId"] = id }));

        AssertNotQueued(response, "Success");
        Assert.That(string.Join(" ", response.Details), Does.Contain("SDK cannot be generated and released").And.Contain("merge").And.Contain("locally"));
        Assert.That(response.ResponseErrors, Is.Empty);
    }

    [TestCase(".NET", SdkLanguage.DotNet, "net", 45001)]
    [TestCase("Python", SdkLanguage.Python, "python", 45002)]
    [TestCase("JavaScript", SdkLanguage.JavaScript, "js", 45003)]
    [TestCase("Java", SdkLanguage.Java, "java", 45004)]
    public void GetPr_UsesCanonicalPlanSdkInfoForRequestedLanguage(string language, SdkLanguage expected, string repo, int pr)
    {
        foreach (var id in new[] { 35000, 50001 })
        {
            var response = (ReleaseWorkflowResponse)new GetSdkPullRequestLinkHandler().Handle(Arguments(new()
            {
                ["workItemId"] = id,
                ["language"] = language
            }));

            Assert.Multiple(() =>
            {
                Assert.That(response.Status, Is.EqualTo("Completed"));
                Assert.That(response.Language, Is.EqualTo(expected));
                Assert.That(response.ResponseErrors, Is.Empty);
                Assert.That(string.Join(" ", response.Details), Does.Contain($"https://github.com/Azure/azure-sdk-for-{repo}/pull/{pr}"));
            });
        }
    }

    [TestCase("Go")]
    [TestCase("Rust")]
    [TestCase("")]
    public void GetPr_DoesNotInventLinksForLanguagesWithoutFixtureSdkInfo(string language)
    {
        var response = (ReleaseWorkflowResponse)new GetSdkPullRequestLinkHandler().Handle(Arguments(new()
        {
            ["workItemId"] = 35000,
            ["language"] = language
        }));

        Assert.That(response.Status, Is.EqualTo("Failed"));
        Assert.That(response.ResponseErrors, Is.Not.Empty);
        Assert.That(response.Details, Is.Empty);
    }

    private static Dictionary<string, object?> GenerationArguments(int id, string language) => new()
    {
        ["workItemId"] = id,
        ["language"] = language,
        ["sdkReleaseType"] = "beta",
        ["typespecProjectRoot"] = ProjectPath
    };

    private Dictionary<string, object?> Arguments(Dictionary<string, object?> arguments) => _wireArguments
        ? JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!
        : arguments;

    private static void AssertNotQueued(ReleaseWorkflowResponse response, string status)
    {
        Assert.That(response.Status, Is.EqualTo(status));
        Assert.That(string.Join(" ", response.Details), Does.Not.Contain("90001"));
        if (status == "Failed")
        {
            Assert.That(response.ResponseErrors, Is.Not.Empty);
            Assert.That(response.OperationStatus, Is.EqualTo(Status.Failed));
            using var wire = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.That(wire.RootElement.GetProperty("status").GetString(), Is.EqualTo("Failed"));
            Assert.That(wire.RootElement.GetProperty("response_errors").GetArrayLength(), Is.GreaterThan(0));
        }
    }
}
