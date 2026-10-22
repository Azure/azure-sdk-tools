// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class ReleasePlanListResponseTests
{
    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 42)]
    [TestCase(true, 42)]
    public void Format_InBothModes_ShowsIdentifierLinkAndReason(bool dryRun, int releasePlanId)
    {
        var plan = new ReleasePlanWorkItem
        {
            WorkItemId = 400, ReleasePlanId = releasePlanId, Title = "Contoso release",
            Status = dryRun ? "In Progress" : "Abandoned", SDKReleaseMonth = "September 2026"
        };
        var response = new ReleasePlanListResponse
        {
            DryRun = dryRun,
            Message = dryRun ? "Dry run: 1 eligible plan. No plans were changed." : "Abandoned 1 inactive release plan.",
            ReleasePlanDetailsList = [plan],
            EligibilityReasons = new Dictionary<int, string> { [400] = "No SDKs released and no linked SDK PRs." }
        };

        var text = response.ToString();

        Assert.That(text, Does.Contain(response.Message));
        Assert.That(text, Does.Contain($"Release Plan ID: {(releasePlanId > 0 ? releasePlanId : 400)}"));
        Assert.That(text, Does.Contain($"Release Plan Link: {plan.ReleasePlanLink}"));
        Assert.That(text, Does.Contain($"Status: {plan.Status}"));
        Assert.That(text, Does.Contain($"{(dryRun ? "Eligibility" : "Abandonment")} Reason: {response.EligibilityReasons[400]}"));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.That(json.RootElement.GetProperty("message").GetString(), Is.EqualTo(response.Message));
        Assert.That(json.RootElement.GetProperty("eligibility_reasons").GetProperty("400").GetString(), Is.EqualTo(response.EligibilityReasons[400]));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PartialFailure_InBothModes_PreservesSummaryAndPlanDetails(bool dryRun)
    {
        var plan = new ReleasePlanWorkItem { WorkItemId = 401, ReleasePlanId = 41, Status = dryRun ? "In Progress" : "Abandoned" };
        var response = new ReleasePlanListResponse
        {
            DryRun = dryRun,
            Message = dryRun ? "Dry run: 1 eligible plan; the list is incomplete." : "Abandoned 1 plan; some plans could not be fully processed.",
            ReleasePlanDetailsList = [plan],
            EligibilityReasons = new Dictionary<int, string> { [401] = "No SDKs released and no linked SDK PRs." },
            ResponseErrors = ["Plan 402 could not be processed."]
        };

        var text = response.ToString();

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(text, Does.Contain(response.Message).And.Contain("Release Plan ID: 41"));
        Assert.That(text, Does.Contain(plan.ReleasePlanLink).And.Contain(response.EligibilityReasons[401]));
        Assert.That(text, Does.Contain("[ERROR] Plan 402 could not be processed."));
        Assert.That(text.IndexOf("Total Release Plans:", StringComparison.Ordinal),
            Is.EqualTo(text.LastIndexOf("Total Release Plans:", StringComparison.Ordinal)), "Partial results must not be duplicated.");
    }

    [Test]
    public void OrdinaryList_ShowsFallbackIdentifierAndDashboardLinkWithoutCleanupReason()
    {
        var plan = new ReleasePlanWorkItem { WorkItemId = 403, Status = "In Progress" };
        var response = new ReleasePlanListResponse { Message = "List of overdue Release plans:", ReleasePlanDetailsList = [plan] };

        var text = response.ToString();

        Assert.That(text, Does.Contain(response.Message).And.Contain("Release Plan ID: 403"));
        Assert.That(text, Does.Contain(plan.ReleasePlanLink));
        Assert.That(text, Does.Not.Contain("Abandonment Reason:").And.Not.Contain("Eligibility Reason:"));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.That(json.RootElement.TryGetProperty("eligibility_reasons", out _), Is.False);
        Assert.That(json.RootElement.TryGetProperty("preview_summary", out _), Is.False);
        Assert.That(json.RootElement.TryGetProperty("skipped_plans", out _), Is.False);
    }

    [TestCase(0)]
    [TestCase(42)]
    public void PreviewDiagnostics_PreserveSkippedPlanIdsAndLinksAlongsideErrors(int releasePlanId)
    {
        var plan = new ReleasePlanWorkItem
        {
            WorkItemId = 420, ReleasePlanId = releasePlanId, Title = "Contoso release", SDKReleaseMonth = "October 2026"
        };
        var displayId = releasePlanId > 0 ? releasePlanId : plan.WorkItemId;
        var response = new ReleasePlanListResponse
        {
            DryRun = true,
            Message = "Dry run: the eligible list is incomplete.",
            ReleasePlanDetailsList = [],
            EligibilityReasons = [],
            SkippedPlans = new Dictionary<int, string> { [displayId] = plan.ReleasePlanLink },
            PreviewSummary = new ReleasePlanPreviewSummary
            {
                Scanned = 2, Eligible = 0, Skipped = 1, EvaluationErrors = 1,
                SkippedByReason = new Dictionary<string, int> { ["grace_period"] = 1 }
            },
            ResponseErrors = ["Plan 421 could not be evaluated."]
        };

        var text = response.ToString();

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(text, Does.Contain("Scanned: 2; Eligible: 0; Skipped: 1; Evaluation errors: 1"));
        Assert.That(text, Does.Contain("grace_period: 1"));
        Assert.That(text, Does.Contain($"Release Plan ID: {displayId} | Release Plan Link: {plan.ReleasePlanLink}"));
        Assert.That(text, Does.Not.Contain(plan.Title).And.Not.Contain(plan.SDKReleaseMonth));
        Assert.That(text, Does.Contain("[ERROR] Plan 421 could not be evaluated."));
        Assert.That(text.IndexOf("Skipped release plans", StringComparison.Ordinal),
            Is.EqualTo(text.LastIndexOf("Skipped release plans", StringComparison.Ordinal)));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.That(json.RootElement.GetProperty("release_plans").GetArrayLength(), Is.Zero);
        var links = json.RootElement.GetProperty("skipped_plans");
        Assert.That(links.EnumerateObject().Count(), Is.EqualTo(1));
        Assert.That(links.GetProperty(displayId.ToString()).GetString(), Is.EqualTo(plan.ReleasePlanLink));
    }

    [Test]
    public void ErrorOnlyResponse_DoesNotInventPlanDetails()
    {
        var response = new ReleasePlanListResponse { ResponseError = "Initial scan failed." };

        Assert.That(response.ToString(), Does.Contain("[ERROR] Initial scan failed."));
        Assert.That(response.ToString(), Does.Not.Contain("Total Release Plans:").And.Not.Contain("Release Plan ID:"));
    }
}