// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

// Fixed snapshots, not a stateful work-item store: every response owns fresh fixture objects.
internal static class ReleasePlanMockResponses
{
    public const string ProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager";
    public const string PreviewApiVersion = "2022-11-01-preview";
    public const string SpecCommitSha = "0123456789abcdef0123456789abcdef01234567";
    public const string MergedSpecCommitSha = "fedcba9876543210fedcba9876543210fedcba98";
    public const string SpecPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/38387";
    public const string ConfirmationNextStep = "Show the proposed project, packages, API version, SDK release type, spec PR, commit URL, merge status and available API versions. After approval, repeat with the exact specCommitSha and confirmTarget=true. API version is derived from unambiguous metadata; select only from availableApiVersions if missing or conflicting. For public updates, preserve ExpectedTargetRevision verbatim as expectedTargetRevision and optionally retain ExpectedPreviousSpecCommitSHA as expectedSpecCommitSha. Any parent or API Spec revision change requires a fresh preview and approval, even at the same SHA.";

    public static string Argument(Dictionary<string, object?>? arguments, string name) =>
        arguments?.GetValueOrDefault(name)?.ToString() ?? string.Empty;

    public static bool Flag(Dictionary<string, object?>? arguments, string name) =>
        bool.TryParse(Argument(arguments, name), out var value) && value;

    public static bool IsContosoProject(string path)
    {
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        return string.Equals(normalized, ProjectPath, StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("/" + ProjectPath, StringComparison.OrdinalIgnoreCase);
    }

    public static ReleasePlanWorkItem ContosoWorkItem(string? typespecPath = null, string? releaseMonth = null) => new()
    {
        WorkItemId = 35000,
        ReleasePlanId = 50001,
        ApiSpecWorkItemId = 45000,
        TargetRevision = "35000:2:45000:3",
        Title = "Release Plan - Contoso.WidgetManager",
        Status = "Active",
        Owner = "testuser@microsoft.com",
        SDKReleaseMonth = releaseMonth ?? "December 2026",
        ApiReleaseType = ApiReleaseType.PublicPreview,
        IsDataPlane = true,
        SpecType = "TypeSpec",
        SpecAPIVersion = PreviewApiVersion,
        SpecCommitSHA = SpecCommitSha,
        ActiveSpecPullRequest = SpecPullRequestUrl,
        APISpecProjectPath = typespecPath ?? ProjectPath,
        SDKReleaseType = "beta",
        SDKInfo =
        [
            new SDKInfo { Language = ".NET", PackageName = "Azure.Template.Contoso", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-net/pull/45001" },
            new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/45002" },
            new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-js/pull/45003" },
            new SDKInfo { Language = "Java", PackageName = "azure-contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/45004" }
        ]
    };

    public static ReleasePlanWorkItem? PlanForId(string id)
    {
        var plan = ContosoWorkItem();
        switch (id)
        {
            case "35000":
            case "50001":
                return plan;
            case "29262": // Existing pipeline-generation scenarios use this merged preview snapshot.
                plan.WorkItemId = plan.ReleasePlanId = 29262;
                plan.ApiSpecWorkItemId = 39262;
                plan.TargetRevision = "29262:2:39262:3";
                plan.SpecAPIVersion = "2024-01-01-preview";
                plan.SpecCommitSHA = MergedSpecCommitSha;
                plan.ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/38500";
                return plan;
            case "35001":
            case "50002":
                plan.WorkItemId = 35001;
                plan.ReleasePlanId = 50002;
                plan.ApiSpecWorkItemId = 45001;
                plan.TargetRevision = "35001:2:45001:3";
                plan.ApiReleaseType = ApiReleaseType.GA;
                plan.SDKReleaseType = "stable";
                plan.SpecAPIVersion = "2024-01-01";
                plan.SpecCommitSHA = MergedSpecCommitSha;
                plan.ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
                return plan;
            case "35002":
            case "50003":
                plan.WorkItemId = 35002;
                plan.ReleasePlanId = 50003;
                plan.ApiSpecWorkItemId = 45002;
                plan.TargetRevision = "35002:2:45002:3";
                plan.ApiReleaseType = ApiReleaseType.PrivatePreview;
                plan.ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs-pr/pull/12345";
                plan.SpecAPIVersion = plan.SpecCommitSHA = string.Empty;
                plan.SDKInfo = [];
                return plan;
            case "35003":
            case "50004": // Tracking-only: metadata is known, but no public target has been confirmed.
                plan.WorkItemId = 35003;
                plan.ReleasePlanId = 50004;
                plan.ApiSpecWorkItemId = 45003;
                plan.TargetRevision = "35003:2:45003:3";
                plan.ActiveSpecPullRequest = plan.SpecCommitSHA = string.Empty;
                return plan;
            default:
                return null;
        }
    }

    public static ReleasePlanWorkItem? PlanForPullRequest(string url) => url.TrimEnd('/').ToLowerInvariant() switch
    {
        "https://github.com/azure/azure-rest-api-specs/pull/38387" => PlanForId("35000"),
        "https://github.com/azure/azure-rest-api-specs/pull/38500" => PlanForId("29262"),
        "https://github.com/azure/azure-rest-api-specs/pull/12345" => PlanForId("35001"),
        "https://github.com/azure/azure-rest-api-specs-pr/pull/12345" => PlanForId("35002"),
        _ => null
    };

    public static ReleasePlanWorkItem? FindPlan(Dictionary<string, object?>? arguments)
    {
        foreach (var key in new[] { "releasePlanId", "workItemId", "workItem" })
        {
            var id = Argument(arguments, key);
            if (!string.IsNullOrWhiteSpace(id) && id != "0")
            {
                return PlanForId(id);
            }
        }
        var pr = Argument(arguments, "specPullRequestUrl");
        if (!string.IsNullOrWhiteSpace(pr))
        {
            return PlanForPullRequest(pr);
        }
        if (!IsContosoProject(Argument(arguments, "typeSpecProjectPath")))
        {
            return null;
        }
        var releaseType = Argument(arguments, "apiReleaseType");
        return releaseType.Equals("GA", StringComparison.OrdinalIgnoreCase) ? PlanForId("35001") :
            releaseType.Equals("Private Preview", StringComparison.OrdinalIgnoreCase) ? PlanForId("35002") :
            Argument(arguments, "apiVersion") == "2024-01-01-preview" ? PlanForId("29262") : ContosoWorkItem();
    }

    public static string? ValidateExpectedPin(Dictionary<string, object?>? arguments, ReleasePlanWorkItem plan)
    {
        var expected = arguments?.GetValueOrDefault("expectedSpecCommitSha")?.ToString();
        return expected != null && !string.Equals(plan.SpecCommitSHA, expected == "none" ? string.Empty : expected, StringComparison.OrdinalIgnoreCase)
            ? "The release plan's spec target changed since it was inspected. Retrieve the target and request confirmation again; no changes were saved."
            : null;
    }

    public static string? ValidateExpectedRevision(Dictionary<string, object?>? arguments, ReleasePlanWorkItem plan)
    {
        var expected = arguments?.GetValueOrDefault("expectedTargetRevision")?.ToString();
        return expected != null && (string.IsNullOrWhiteSpace(expected) || !string.Equals(plan.TargetRevision, expected, StringComparison.Ordinal))
            ? "The release plan or API Spec changed since the target was previewed. Preview again and obtain fresh approval; no changes were saved."
            : null;
    }

    public static string? GetTarget(Dictionary<string, object?>? arguments, string sdkReleaseType, ReleasePlanWorkItem? existingPlan, out ReleasePlanSpecTarget? target)
    {
        target = null;
        if (!IsContosoProject(Argument(arguments, "typeSpecProjectPath")))
        {
            return "Provide the Contoso TypeSpec project path to preview and confirm the public SDK target.";
        }
        var snapshot = PlanForPullRequest(Argument(arguments, "specPullRequestUrl"));
        if (snapshot == null || snapshot.ApiReleaseType == ApiReleaseType.PrivatePreview)
        {
            return "No public spec snapshot fixture exists for this PR. Use the Contoso public spec PR from the release plan.";
        }
        var version = Argument(arguments, "apiVersion");
        if (!string.IsNullOrWhiteSpace(version) && !string.Equals(version.Trim(), snapshot.SpecAPIVersion, StringComparison.OrdinalIgnoreCase))
        {
            return $"API version '{version}' is not declared at commit {snapshot.SpecCommitSHA}. Available versions: {snapshot.SpecAPIVersion}.";
        }
        var sha = Argument(arguments, "specCommitSha");
        if (!string.IsNullOrEmpty(sha) && !string.Equals(sha, snapshot.SpecCommitSHA, StringComparison.OrdinalIgnoreCase))
        {
            return $"Spec PR source changed or the supplied SHA does not match it. Review commit {snapshot.SpecCommitSHA} and confirm the intended target again.";
        }
        if (existingPlan != null && !string.IsNullOrWhiteSpace(existingPlan.SpecAPIVersion) && existingPlan.SpecAPIVersion != snapshot.SpecAPIVersion)
        {
            return "API version does not match the release plan. Use a separate release plan for the new API version.";
        }
        if (sdkReleaseType == "stable" && snapshot.SpecAPIVersion.Contains("preview", StringComparison.OrdinalIgnoreCase))
        {
            return "A stable SDK release cannot target a preview API version. Confirm a beta release or choose a stable API version.";
        }
        target = new ReleasePlanSpecTarget
        {
            TypeSpecProjectPath = ProjectPath,
            ApiVersion = snapshot.SpecAPIVersion,
            SpecCommitSHA = snapshot.SpecCommitSHA,
            SpecPullRequestUrl = snapshot.ActiveSpecPullRequest,
            CommitUrl = $"https://github.com/Azure/azure-rest-api-specs/commit/{snapshot.SpecCommitSHA}",
            SDKReleaseType = sdkReleaseType,
            IsSpecMerged = snapshot.SpecCommitSHA == MergedSpecCommitSha,
            ExpectedPreviousSpecCommitSHA = existingPlan == null ? null : string.IsNullOrEmpty(existingPlan.SpecCommitSHA) ? "none" : existingPlan.SpecCommitSHA,
            ExpectedTargetRevision = existingPlan?.TargetRevision,
            AvailableApiVersions = [snapshot.SpecAPIVersion],
            Packages = snapshot.SDKInfo.Select(sdk => new PackageInfo
            {
                Language = SdkLanguageHelpers.GetSdkLanguage(sdk.Language),
                PackageName = sdk.PackageName,
                ApiVersion = snapshot.SpecAPIVersion
            }).ToList()
        };
        return null;
    }

    public static bool NeedsConfirmation(Dictionary<string, object?>? arguments, ReleasePlanSpecTarget target) =>
        !Flag(arguments, "confirmTarget") || string.IsNullOrWhiteSpace(target.ApiVersion) || string.IsNullOrWhiteSpace(Argument(arguments, "specCommitSha"));

    public static void ApplyTarget(ReleasePlanWorkItem plan, ReleasePlanSpecTarget target)
    {
        plan.SpecAPIVersion = target.ApiVersion;
        plan.SpecCommitSHA = target.SpecCommitSHA;
        plan.ActiveSpecPullRequest = target.SpecPullRequestUrl;
        plan.SDKReleaseType = target.SDKReleaseType;
    }

    public static ReleasePlanResponse PlanResponse(ReleasePlanWorkItem plan, string message) => new()
    {
        TypeSpecProject = plan.APISpecProjectPath,
        PackageType = SdkType.Dataplane,
        Message = message,
        ReleasePlanDetails = plan,
        Warnings = [$"Release plan 49999 ({ReleasePlanWorkItem.DashboardBaseUrl}49999) is past due. Its target release month was May 2026."],
        NextSteps = ["Either postpone the past-due plan by updating its target release month, or abandon it and record the reason in the release plan dashboard."]
    };

    public static ReleasePlanResponse Preview(ReleasePlanSpecTarget target, ReleasePlanWorkItem? existingPlan = null) => new()
    {
        TypeSpecProject = ProjectPath,
        PackageType = SdkType.Dataplane,
        ProposedSpecTarget = target,
        RequiresConfirmation = true,
        ReleasePlanDetails = existingPlan,
        Message = "Review and confirm the release target. No release plan was created or updated.",
        NextSteps = [ConfirmationNextStep]
    };

    public static ReleaseWorkflowResponse Workflow(string status, params string[] details) => new()
    {
        Language = SdkLanguage.DotNet,
        Status = status,
        TypeSpecProject = ProjectPath,
        Details = details.ToList()
    };
}