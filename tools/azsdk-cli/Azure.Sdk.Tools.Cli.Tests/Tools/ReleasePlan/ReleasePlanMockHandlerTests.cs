// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias MockServer;

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using MockHandlers = MockServer::Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan;

// Exercises the hermetic MCP responses without invoking services, compilers, or pipelines.
[TestFixture]
internal class ReleasePlanMockHandlerTests
{
    private const string ProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager";
    private const string ApiVersion = "2022-11-01-preview";
    private const string SpecCommitSha = "0123456789abcdef0123456789abcdef01234567";
    private const string OtherCommitSha = "fedcba9876543210fedcba9876543210fedcba98";
    private const string SpecPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/38387";

    [TestCase("create", null)]
    [TestCase("create", false)]
    [TestCase("update", null)]
    [TestCase("update", false)]
    [TestCase("link", null)]
    [TestCase("link", false)]
    public void PublicMutation_RequiresConfirmationWhenFlagIsOmittedOrFalse(string operation, bool? confirm)
    {
        var arguments = TargetArguments(operation);
        if (confirm.HasValue)
        {
            arguments["confirmTarget"] = confirm.Value;
        }
        else
        {
            arguments.Remove("confirmTarget");
        }

        var response = HandleTarget(operation, arguments);

        AssertPreview(response, operation);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, response.GetType()));
        Assert.Multiple(() =>
        {
            Assert.That(json.RootElement.GetProperty("requires_confirmation").GetBoolean(), Is.True);
            Assert.That(json.RootElement.GetProperty("proposed_spec_target").GetProperty("SpecCommitSHA").GetString(), Is.EqualTo(SpecCommitSha));
            Assert.That(json.RootElement.GetProperty("proposed_spec_target").GetProperty("Packages")[0].GetProperty("Name").GetString(), Is.EqualTo("Azure.Template.Contoso"));
        });
    }

    [TestCase("create", "apiVersion")]
    [TestCase("create", "specCommitSha")]
    [TestCase("create", "both")]
    [TestCase("update", "apiVersion")]
    [TestCase("update", "specCommitSha")]
    [TestCase("update", "both")]
    [TestCase("link", "apiVersion")]
    [TestCase("link", "specCommitSha")]
    [TestCase("link", "both")]
    public void PublicMutation_TrueFlagDoesNotSubstituteForExplicitTarget(string operation, string missing)
    {
        var arguments = TargetArguments(operation);
        if (missing is "apiVersion" or "both")
        {
            arguments.Remove("apiVersion");
        }
        if (missing is "specCommitSha" or "both")
        {
            arguments.Remove("specCommitSha");
        }

        AssertPreview(HandleTarget(operation, arguments), operation);
    }

    [TestCase("create", false)]
    [TestCase("create", true)]
    [TestCase("update", false)]
    [TestCase("update", true)]
    [TestCase("link", false)]
    [TestCase("link", true)]
    public void PublicMutation_ConfirmsMatchingExplicitTargetIncludingMcpJsonArguments(string operation, bool jsonArguments)
    {
        var arguments = TargetArguments(operation);
        arguments["specCommitSha"] = SpecCommitSha.ToUpperInvariant();
        if (jsonArguments)
        {
            if (operation == "link")
            {
                arguments.Remove("workItemId");
                arguments["releasePlanId"] = 50001;
            }
            // MockMcpServerTool boxes JSON elements, not CLR booleans/integers.
            arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!;
        }

        var response = HandleTarget(operation, arguments);

        Assert.Multiple(() =>
        {
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.RequiresConfirmation, Is.False);
            Assert.That(response.ProposedSpecTarget?.ApiVersion, Is.EqualTo(ApiVersion));
            Assert.That(response.ProposedSpecTarget?.SpecCommitSHA, Is.EqualTo(SpecCommitSha));
            Assert.That(response.ProposedSpecTarget?.SDKReleaseType, Is.EqualTo("beta"));
        });
        if (response is ReleasePlanResponse planResponse)
        {
            var readPlan = (ReleasePlanResponse)new MockHandlers.GetReleasePlanHandler().Handle(new() { ["releasePlanId"] = 50001 });
            Assert.Multiple(() =>
            {
                Assert.That(planResponse.ReleasePlanDetails?.WorkItemId, Is.EqualTo(35000));
                Assert.That(planResponse.ReleasePlanDetails?.ReleasePlanId, Is.EqualTo(50001));
                Assert.That(planResponse.ReleasePlanDetails?.SpecCommitSHA, Is.EqualTo(readPlan.ReleasePlanDetails?.SpecCommitSHA));
                Assert.That(planResponse.ReleasePlanDetails?.SpecAPIVersion, Is.EqualTo(readPlan.ReleasePlanDetails?.SpecAPIVersion));
                Assert.That(planResponse.ReleasePlanDetails?.SDKReleaseType, Is.EqualTo(readPlan.ReleasePlanDetails?.SDKReleaseType));
                Assert.That(planResponse.ReleasePlanDetails?.ActiveSpecPullRequest, Is.EqualTo(readPlan.ReleasePlanDetails?.ActiveSpecPullRequest));
                Assert.That(planResponse.ReleasePlanDetails?.SDKInfo.Select(sdk => sdk.PackageName), Is.EqualTo(readPlan.ReleasePlanDetails?.SDKInfo.Select(sdk => sdk.PackageName)));
            });
        }
        else
        {
            var workflow = (ReleaseWorkflowResponse)response;
            Assert.That(workflow.Status, Is.EqualTo("Success"));
            Assert.That(workflow.Details, Has.Some.Contains(SpecCommitSha));
        }
    }

    [TestCase("create", "apiVersion", "2099-01-01-preview")]
    [TestCase("update", "apiVersion", "2099-01-01-preview")]
    [TestCase("link", "apiVersion", "2099-01-01-preview")]
    [TestCase("create", "specCommitSha", OtherCommitSha)]
    [TestCase("update", "specCommitSha", OtherCommitSha)]
    [TestCase("link", "specCommitSha", OtherCommitSha)]
    [TestCase("create", "specCommitSha", "main")]
    [TestCase("update", "specCommitSha", "main")]
    [TestCase("link", "specCommitSha", "main")]
    public void PublicMutation_RejectsMismatchedVersionOrSha(string operation, string key, string value)
    {
        var arguments = TargetArguments(operation);
        arguments[key] = value;

        var response = HandleTarget(operation, arguments);

        Assert.That(response.ResponseError, Is.Not.Null.And.Not.Empty);
        Assert.That(response.ProposedSpecTarget, Is.Null);
        Assert.That(response.OperationStatus, Is.EqualTo(Status.Failed));
    }

    [TestCase("update", OtherCommitSha)]
    [TestCase("link", OtherCommitSha)]
    [TestCase("update", "none")]
    [TestCase("link", "none")]
    public void PublicUpdate_RejectsStaleExpectedPin(string operation, string expectedPin)
    {
        var arguments = TargetArguments(operation);
        arguments["expectedSpecCommitSha"] = expectedPin;

        var response = HandleTarget(operation, arguments);

        Assert.That(response.ResponseError, Does.Contain("changed since it was inspected"));
    }

    [TestCase("update")]
    [TestCase("link")]
    public void PublicUpdate_RejectsAnotherApiVersionEvenWhenDeclaredInTheNewSnapshot(string operation)
    {
        var arguments = TargetArguments(operation);
        arguments["specPullRequestUrl"] = "https://github.com/Azure/azure-rest-api-specs/pull/38500";
        arguments["apiVersion"] = "2024-01-01-preview";
        arguments["specCommitSha"] = OtherCommitSha;

        var response = HandleTarget(operation, arguments);

        Assert.That(response.ResponseError, Does.Contain("separate release plan"));
    }

    [TestCase("create")]
    [TestCase("update")]
    [TestCase("link")]
    public void PrivatePreview_RemainsSpecOnlyWithoutTargetConfirmation(string operation)
    {
        var arguments = TargetArguments(operation);
        arguments["apiReleaseType"] = "Private Preview";
        if (operation != "create")
        {
            arguments["workItemId"] = 35002;
        }
        arguments["specPullRequestUrl"] = "https://github.com/Azure/azure-rest-api-specs-pr/pull/12345";
        arguments.Remove("apiVersion");
        arguments.Remove("specCommitSha");
        arguments.Remove("expectedSpecCommitSha");
        arguments.Remove("confirmTarget");

        var response = HandleTarget(operation, arguments);

        Assert.Multiple(() =>
        {
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.RequiresConfirmation, Is.False);
            Assert.That(response.ProposedSpecTarget, Is.Null);
        });
        if (response is ReleasePlanResponse planResponse)
        {
            Assert.That(planResponse.ReleasePlanDetails?.ApiReleaseType, Is.EqualTo(ApiReleaseType.PrivatePreview));
            Assert.That(planResponse.ReleasePlanDetails?.SpecCommitSHA, Is.Empty);
            Assert.That(planResponse.ReleasePlanDetails?.SDKInfo, Is.Empty);
        }
    }

    [Test]
    public void TrackingCreate_DoesNotInventPublicSpecLinkOrPin()
    {
        var arguments = TargetArguments("create");
        arguments.Remove("specPullRequestUrl");
        arguments.Remove("apiVersion");
        arguments.Remove("specCommitSha");
        arguments.Remove("confirmTarget");

        var response = (ReleasePlanResponse)HandleTarget("create", arguments);

        Assert.Multiple(() =>
        {
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.RequiresConfirmation, Is.False);
            Assert.That(response.ProposedSpecTarget, Is.Null);
            Assert.That(response.ReleasePlanDetails?.ActiveSpecPullRequest, Is.Empty);
            Assert.That(response.ReleasePlanDetails?.SpecCommitSHA, Is.Empty);
        });
        var generation = GenerationArguments();
        generation["workItemId"] = response.ReleasePlanDetails!.WorkItemId;
        var result = (ReleaseWorkflowResponse)new MockHandlers.RunGenerateSdkHandler().Handle(generation);
        Assert.That(result.Status, Is.EqualTo("Failed"));
        Assert.That(result.ResponseError, Does.Contain("Preview and confirm"));
    }

    [TestCase("update")]
    [TestCase("link")]
    public void TrackingUpdate_PreviewsPreviousPinAsNoneAndAcceptsExplicitConfirmation(string operation)
    {
        var arguments = TargetArguments(operation);
        arguments["workItemId"] = 35003;
        arguments["expectedSpecCommitSha"] = "none";
        arguments["confirmTarget"] = false;

        var preview = HandleTarget(operation, arguments);
        Assert.That(preview.RequiresConfirmation, Is.True);
        Assert.That(preview.ProposedSpecTarget?.ExpectedPreviousSpecCommitSHA, Is.EqualTo("none"));
        if (preview is ReleasePlanResponse planResponse)
        {
            Assert.That(planResponse.ReleasePlanDetails?.SpecCommitSHA, Is.Empty);
        }

        arguments["confirmTarget"] = true;
        var confirmed = HandleTarget(operation, arguments);
        Assert.That(confirmed.ResponseError, Is.Null);
        Assert.That(confirmed.RequiresConfirmation, Is.False);
        Assert.That(confirmed.ProposedSpecTarget?.SpecCommitSHA, Is.EqualTo(SpecCommitSha));
    }

    [TestCase("apiVersion", ApiVersion)]
    [TestCase("specCommitSha", SpecCommitSha)]
    public void TrackingCreate_RejectsTargetInputsWithoutPublicPr(string key, string value)
    {
        var arguments = TargetArguments("create");
        arguments.Remove("specPullRequestUrl");
        arguments.Remove("apiVersion");
        arguments.Remove("specCommitSha");
        arguments[key] = value;

        Assert.That(HandleTarget("create", arguments).ResponseError, Does.Contain("public spec PR is required"));
    }

    [Test]
    public void MetadataOnlyUpdate_PreservesPinWithoutConfirmation()
    {
        var arguments = TargetArguments("update");
        arguments.Remove("specPullRequestUrl");
        arguments.Remove("confirmTarget");

        var response = (ReleasePlanResponse)HandleTarget("update", arguments);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.ProposedSpecTarget, Is.Null);
        Assert.That(response.ReleasePlanDetails?.SpecCommitSHA, Is.EqualTo(SpecCommitSha));
        arguments["sdkReleaseType"] = "stable";
        Assert.That(HandleTarget("update", arguments).ResponseError, Does.Contain("previewing and confirming"));
    }

    [Test]
    public void GaCreate_DerivesStableAndReturnsTheMergedSnapshot()
    {
        var arguments = TargetArguments("create");
        arguments["apiReleaseType"] = "GA";
        arguments["specPullRequestUrl"] = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
        arguments["apiVersion"] = "2024-01-01";
        arguments["specCommitSha"] = OtherCommitSha;

        var response = (ReleasePlanResponse)HandleTarget("create", arguments);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.ProposedSpecTarget?.SDKReleaseType, Is.EqualTo("stable"));
        Assert.That(response.ProposedSpecTarget?.IsSpecMerged, Is.True);
        Assert.That(response.ReleasePlanDetails?.SpecCommitSHA, Is.EqualTo(OtherCommitSha));
        arguments["specPullRequestUrl"] = SpecPullRequestUrl;
        arguments["apiVersion"] = ApiVersion;
        arguments["specCommitSha"] = SpecCommitSha;
        Assert.That(HandleTarget("create", arguments).ResponseError, Does.Contain("stable SDK release cannot target a preview"));
    }

    [TestCase("workItemId", "35000")]
    [TestCase("workItemId", "50001")]
    [TestCase("releasePlanId", "50001")]
    [TestCase("specPullRequestUrl", SpecPullRequestUrl)]
    [TestCase("typeSpecProjectPath", ProjectPath)]
    public void Get_ExposesTheSameFixedPinAndReturnsIndependentObjects(string key, string value)
    {
        var handler = new MockHandlers.GetReleasePlanHandler();
        var response = (ReleasePlanResponse)handler.Handle(new() { [key] = value });

        Assert.That(response.ReleasePlanDetails?.SpecCommitSHA, Is.EqualTo(SpecCommitSha));
        Assert.That(response.ReleasePlanDetails?.SpecAPIVersion, Is.EqualTo(ApiVersion));
        Assert.That(response.ReleasePlanDetails?.SDKReleaseMonth, Is.EqualTo("June 2026"));
        response.ReleasePlanDetails!.SpecCommitSHA = OtherCommitSha;
        var second = (ReleasePlanResponse)handler.Handle(new() { [key] = value });
        Assert.That(second.ReleasePlanDetails?.SpecCommitSHA, Is.EqualTo(SpecCommitSha), "No mutable fixture state may leak between calls or evals.");
    }

    [TestCase("Public Preview", ApiVersion, 35000, SpecCommitSha)]
    [TestCase("Public Preview", "2024-01-01-preview", 29262, OtherCommitSha)]
    [TestCase("GA", "2024-01-01", 35001, OtherCommitSha)]
    public void Get_ByProjectAndVersionReturnsTheMatchingPinnedFixture(string releaseType, string apiVersion, int workItemId, string sha)
    {
        var response = (ReleasePlanResponse)new MockHandlers.GetReleasePlanHandler().Handle(new()
        {
            ["typeSpecProjectPath"] = ProjectPath,
            ["apiReleaseType"] = releaseType,
            ["apiVersion"] = apiVersion
        });

        Assert.That(response.ReleasePlanDetails?.WorkItemId, Is.EqualTo(workItemId));
        Assert.That(response.ReleasePlanDetails?.SpecAPIVersion, Is.EqualTo(apiVersion));
        Assert.That(response.ReleasePlanDetails?.SpecCommitSHA, Is.EqualTo(sha));
    }

    [TestCase("workItemId", "0")]
    [TestCase("workItemId", "99999")]
    [TestCase("workItemId", "35003")]
    [TestCase("apiVersion", "2024-01-01-preview")]
    [TestCase("specCommitSha", OtherCommitSha)]
    [TestCase("sdkReleaseType", "stable")]
    [TestCase("typespecProjectRoot", "specification/another/Project")]
    [TestCase("pullRequestNumber", "38500")]
    [TestCase("language", "all")]
    public void Generate_RejectsMissingPinOrConflictingTarget(string key, string value)
    {
        var arguments = GenerationArguments();
        arguments[key] = value;

        var response = (ReleaseWorkflowResponse)new MockHandlers.RunGenerateSdkHandler().Handle(arguments);

        Assert.That(response.Status, Is.EqualTo("Failed"));
        Assert.That(response.ResponseError, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Details, Is.Empty);
    }

    [TestCase(35000, ".NET", SdkLanguage.DotNet)]
    [TestCase(50001, "Python", SdkLanguage.Python)]
    [TestCase(35000, "JavaScript", SdkLanguage.JavaScript)]
    [TestCase(50001, "Java", SdkLanguage.Java)]
    public void Generate_UsesStoredTargetWithoutNeedingOptionalGuards(int id, string language, SdkLanguage expectedLanguage)
    {
        var arguments = GenerationArguments();
        arguments["workItemId"] = id;
        arguments["language"] = language;

        var response = (ReleaseWorkflowResponse)new MockHandlers.RunGenerateSdkHandler().Handle(arguments);

        Assert.That(response.Status, Is.EqualTo("Success"));
        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Language, Is.EqualTo(expectedLanguage));
        Assert.That(response.Details, Has.Some.Contains(SpecCommitSha).And.Contains(ApiVersion).And.Contains("35000"));
        Assert.That(response.Details, Has.Some.Contains("Draft SDK review"));
    }

    [Test]
    public void Generate_AutoReleaseRequiresMergedSnapshotAndPrivatePreviewNeverQueues()
    {
        var handler = new MockHandlers.RunGenerateSdkHandler();
        var arguments = GenerationArguments();
        arguments["requireMergedSpec"] = JsonSerializer.SerializeToElement(true);

        var preview = (ReleaseWorkflowResponse)handler.Handle(arguments);
        Assert.That(preview.Status, Is.EqualTo("Failed"));
        Assert.That(preview.ResponseError, Does.Contain("merge commit"));

        arguments["workItemId"] = 29262;
        var merged = (ReleaseWorkflowResponse)handler.Handle(arguments);
        Assert.That(merged.Status, Is.EqualTo("Success"));
        Assert.That(merged.Details, Has.Some.Contains(OtherCommitSha).And.Contains("2024-01-01-preview"));

        arguments["workItemId"] = 35002;
        var privatePreview = (ReleaseWorkflowResponse)handler.Handle(arguments);
        Assert.That(privatePreview.Status, Is.EqualTo("Success"));
        Assert.That(privatePreview.Details, Has.Some.Contains("no SDK generation pipeline was triggered"));
        Assert.That(privatePreview.Details, Has.None.Contains("90001"));
    }

    private static ReleasePlanBaseResponse HandleTarget(string operation, Dictionary<string, object?> arguments) =>
        (ReleasePlanBaseResponse)(operation switch
        {
            "create" => new MockHandlers.CreateReleasePlanHandler().Handle(arguments),
            "update" => new MockHandlers.UpdateReleasePlanHandler().Handle(arguments),
            "link" => new MockHandlers.UpdateApiSpecPullRequestInReleasePlanHandler().Handle(arguments),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        });

    private static Dictionary<string, object?> TargetArguments(string operation)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["typeSpecProjectPath"] = ProjectPath,
            ["specPullRequestUrl"] = SpecPullRequestUrl,
            ["apiVersion"] = ApiVersion,
            ["specCommitSha"] = SpecCommitSha,
            ["confirmTarget"] = true
        };
        if (operation == "create")
        {
            arguments["apiReleaseType"] = "Public Preview";
            arguments["targetReleaseMonthYear"] = "June 2026";
        }
        else
        {
            arguments["workItemId"] = 35000;
            arguments["expectedSpecCommitSha"] = SpecCommitSha;
        }
        if (operation == "update")
        {
            arguments["sdkReleaseType"] = "beta";
        }
        return arguments;
    }

    private static Dictionary<string, object?> GenerationArguments() => new()
    {
        ["workItemId"] = 35000,
        ["typespecProjectRoot"] = ProjectPath,
        ["sdkReleaseType"] = "beta",
        ["language"] = "Python"
    };

    private static void AssertPreview(ReleasePlanBaseResponse response, string operation)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.RequiresConfirmation, Is.True);
            Assert.That(response.ProposedSpecTarget?.ApiVersion, Is.EqualTo(ApiVersion));
            Assert.That(response.ProposedSpecTarget?.SpecCommitSHA, Is.EqualTo(SpecCommitSha));
            Assert.That(response.ProposedSpecTarget?.SDKReleaseType, Is.EqualTo("beta"));
            Assert.That(response.ProposedSpecTarget?.TypeSpecProjectPath, Is.EqualTo(ProjectPath));
            Assert.That(response.ProposedSpecTarget?.SpecPullRequestUrl, Is.EqualTo(SpecPullRequestUrl));
            Assert.That(response.ProposedSpecTarget?.CommitUrl, Is.EqualTo($"https://github.com/Azure/azure-rest-api-specs/commit/{SpecCommitSha}"));
            Assert.That(response.ProposedSpecTarget?.IsSpecMerged, Is.False);
            Assert.That(response.ProposedSpecTarget?.AvailableApiVersions, Is.EquivalentTo(new[] { ApiVersion }));
            Assert.That(response.ProposedSpecTarget?.Packages.Select(package => package.PackageName), Is.EqualTo(new[]
            {
                "Azure.Template.Contoso", "azure-contoso-widgetmanager", "@azure/contoso-widgetmanager", "azure-contoso-widgetmanager"
            }));
            Assert.That(response.ProposedSpecTarget?.ExpectedPreviousSpecCommitSHA, Is.EqualTo(operation == "create" ? null : SpecCommitSha));
            Assert.That(response.NextSteps, Has.Some.Contains("confirmTarget=true"));
        });
        if (operation == "create")
        {
            Assert.That(((ReleasePlanResponse)response).ReleasePlanDetails, Is.Null, "A preview must not claim a created work item.");
        }
        else if (operation == "update")
        {
            Assert.That(((ReleasePlanResponse)response).ReleasePlanDetails?.SpecCommitSHA, Is.EqualTo(SpecCommitSha));
        }
        else
        {
            Assert.That(((ReleaseWorkflowResponse)response).Status, Is.EqualTo("Confirmation required"));
        }
    }
}