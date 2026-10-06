// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

const int WorkItemId = 29426;
const int PlanId = 2006;
const string Title = "Release Plan - 2006 - GA - ExpressRoute Scalable Gateway";
const string SourceCommit = "93214720748557fa4e41b562cacb74117b6196c8";
var apply = args.Contains("--apply-work-item-29426", StringComparer.Ordinal);
var outputIndex = Array.IndexOf(args, "--output");
if (outputIndex < 0 || outputIndex + 1 >= args.Length || args.Length != (apply ? 3 : 2))
{
    throw new ArgumentException("Specify --output <directory> and optionally --apply-work-item-29426. No other plan can be selected.");
}
var output = Path.GetFullPath(args[outputIndex + 1]);
Directory.CreateDirectory(output);
var evidencePath = Path.Combine(output, $"single-plan-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}.json");
var connection = new DevOpsConnection(new AzureService());
var real = new DevOpsService(NullLogger<DevOpsService>.Instance, connection);
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var ct = deadline.Token;
var attempts = 0;
var writes = 0;
var confirmations = 0;
var complete = false;
string? failure = null;
int? beforeRevision = null;
int? afterRevision = null;
string? beforeState = null;
string? afterState = null;
var checks = new List<string>();
try
{
    var initial = await ReadSelected();
    beforeRevision = initial.Revision;
    beforeState = initial.Status;
    if (initial.Status == "Abandoned")
    {
        afterState = initial.Status;
        afterRevision = initial.Revision;
        checks.Add("Selected plan already Abandoned: no repeated write or notification. This does not establish a new service-identity write.");
        Console.WriteLine("Selected plan is already Abandoned; no new write attempted.");
        return;
    }
    RequireEligible(initial);
    var all = await real.ListOverdueReleasePlansAsync(ct);
    Ensure(all.Count(plan => plan.WorkItemId == WorkItemId) == 1, "selected-plan-in-real-overdue-scan");
    var others = all.Where(plan => plan.WorkItemId != WorkItemId).ToDictionary(plan => plan.WorkItemId, plan => (plan.Revision, plan.Status));
    var scoped = ClosedProxy.Create<IDevOpsService>((method, arguments) =>
    {
        if (method.Name == nameof(IDevOpsService.ListOverdueReleasePlansAsync))
        {
            return ListOnlySelected();
        }
        if (method.Name == nameof(IDevOpsService.UpdateWorkItemAsync) && arguments.Length == 4 && arguments[2] is int revision)
        {
            Ensure(apply && (int)arguments[0]! == WorkItemId, "one-explicitly-authorized-id");
            var fields = (Dictionary<string, string>)arguments[1]!;
            Ensure(fields.Count == 1 && fields.GetValueOrDefault("System.State") == "Abandoned", "only-state-change");
            return UpdateOnlySelected(fields, revision, (CancellationToken)arguments[3]!);
        }
        throw new InvalidOperationException($"Unexpected service operation blocked: {method.Name}.");
    });
    var notification = new InterceptNotification(async payload =>
    {
        var persisted = await ReadSelected();
        Ensure(persisted.Status == "Abandoned" && writes == 1, "confirmation-after-successful-write");
        Ensure(payload.Subject.Contains($"({PlanId})", StringComparison.Ordinal), "confirmation-for-only-plan-2006");
        confirmations++;
        await SaveEvidence();
    });
    using var http = new HttpClient(new BlockRequests());
    var tool = new ReleasePlanTool(scoped, ClosedProxy.Create<IGitHelper>(), ClosedProxy.Create<ITypeSpecHelper>(),
        NullLogger<ReleasePlanTool>.Instance, ClosedProxy.Create<IUserHelper>(), ClosedProxy.Create<IGitHubService>(),
        ClosedProxy.Create<IEnvironmentHelper>(), ClosedProxy.Create<IInputSanitizer>(), http,
        ClosedProxy.Create<INpxHelper>(), ClosedProxy.Create<IRawOutputHelper>(), notification);
    var preview = await tool.AbandonOverdueReleasePlans(dryRun: true, ct: ct);
    var afterPreview = await ReadSelected();
    Ensure(preview.ExitCode == 0 && preview.ReleasePlanDetailsList?.Single().WorkItemId == WorkItemId,
        "actual-released-handler-one-plan-preview");
    Ensure(afterPreview.Revision == initial.Revision && afterPreview.Status == initial.Status && attempts == 0 && confirmations == 0,
        "preview-no-writes-or-confirmation");
    checks.Add("Fresh single-plan preview passed: old target, no linked SDK PRs, no released SDKs, valid current revision; no state change.");
    await SaveEvidence();
    if (!apply)
    {
        Console.WriteLine("PASS: single-plan dry run only. No writes or emails.");
        return;
    }
    var response = await tool.AbandonOverdueReleasePlans(ct: ct);
    var abandoned = await ReadSelected();
    afterState = abandoned.Status;
    afterRevision = abandoned.Revision;
    if (response.ExitCode != 0)
    {
        throw new InvalidOperationException(string.Join("; ", response.ResponseErrors ?? [response.ResponseError ?? "Scoped cleanup failed."]));
    }
    Ensure(response.ReleasePlanDetailsList?.Single().WorkItemId == WorkItemId && abandoned.Status == "Abandoned"
        && attempts == 1 && writes == 1 && confirmations == 1, "one-live-write-one-intercepted-confirmation");
    checks.Add("Pipeline service identity performed one real atomic /rev-guarded Abandoned update; confirmation captured after persistence, not sent.");
    await SaveEvidence();
    var rerun = await tool.AbandonOverdueReleasePlans(ct: ct);
    var afterRerun = await ReadSelected();
    Ensure(rerun.ExitCode == 0 && rerun.ReleasePlanDetailsList?.Count == 0 && afterRerun.Revision == afterRevision
        && attempts == 1 && confirmations == 1, "rerun-no-repeat-write-or-confirmation");
    checks.Add("Scoped rerun passed: terminal plan excluded, no extra state write or email attempt.");
    var remaining = await real.ListOverdueReleasePlansAsync(ct);
    Ensure(remaining.All(plan => plan.WorkItemId != WorkItemId), "normal-query-excludes-abandoned-plan");
    var otherAfter = remaining.ToDictionary(plan => plan.WorkItemId, plan => (plan.Revision, plan.Status));
    var externallyChanged = others.Where(pair => !otherAfter.TryGetValue(pair.Key, out var state) || state != pair.Value).Select(pair => pair.Key).ToArray();
    checks.Add(externallyChanged.Length == 0
        ? $"All {others.Count} unrelated overdue states/revisions unchanged; proxy allowed no write outside 29426."
        : $"Concurrent unrelated revisions observed on {externallyChanged.Length} items; this verifier permitted no write outside 29426.");
    complete = true;
    Console.WriteLine($"PASS: pipeline identity cleaned only plan {PlanId}; revision {beforeRevision}->{afterRevision}, writes=1, captured confirmations=1, emails=0.");
}
catch (Exception exception)
{
    failure = exception.Message;
    Console.Error.WriteLine($"Single-plan cleanup blocked or failed: {exception.Message}. Inspect the existing evidence before retrying.");
    Environment.ExitCode = 1;
}
finally
{
    await SaveEvidence();
}

async Task<ReleasePlanWorkItem> ReadSelected()
{
    var plan = await real.GetReleasePlanForWorkItemAsync(WorkItemId, ct);
    Ensure(plan.WorkItemId == WorkItemId && plan.ReleasePlanId == PlanId && plan.Title == Title && !plan.IsTestReleasePlan
        && plan.ApiReleaseType == ApiReleaseType.GA && plan.Revision > 0, "selected-plan-identity");
    return plan;
}

static void RequireEligible(ReleasePlanWorkItem plan)
{
    Ensure(plan.Status is "New" or "Not Started" or "In Progress", "active-state");
    Ensure(DateTime.TryParseExact(plan.SDKReleaseMonth, ["MMMM yyyy", "MMM yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var month), "target-month");
    var now = DateTimeOffset.UtcNow;
    Ensure((now.Year - month.Year) * 12 + now.Month - month.Month > 1, "past-grace");
    Ensure(plan.SDKInfo.All(sdk => string.IsNullOrWhiteSpace(sdk.SdkPullRequestUrl)
        && !string.Equals(sdk.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase)), "no-sdk-pr-or-released-sdk");
}

async Task<List<ReleasePlanWorkItem>> ListOnlySelected()
{
    var plan = await ReadSelected();
    return plan.Status is "New" or "Not Started" or "In Progress" ? [plan] : [];
}

async Task<WorkItem> UpdateOnlySelected(Dictionary<string, string> fields, int revision, CancellationToken cancellation)
{
    var plan = await ReadSelected();
    RequireEligible(plan);
    Ensure(plan.Revision == revision && attempts == 0, "fresh-revision-one-attempt-no-retry");
    attempts++;
    await SaveEvidence();
    var changed = await real.UpdateWorkItemAsync(WorkItemId, fields, revision, cancellation);
    writes++;
    afterRevision = changed.Rev;
    afterState = changed.Fields["System.State"].ToString();
    await SaveEvidence();
    return changed;
}

async Task SaveEvidence()
{
    await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(new
    {
        recordedUtc = DateTimeOffset.UtcNow,
        sourceRelease = "azsdk_0.6.52",
        sourceCommit = SourceCommit,
        pipelineBuildId = Environment.GetEnvironmentVariable("BUILD_BUILDID"),
        usedPipelineIdentity = Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID") != null,
        planId = PlanId,
        workItemId = WorkItemId,
        workItemUrl = "https://dev.azure.com/azure-sdk/Release/_workitems/edit/29426",
        applyRequested = apply,
        beforeState,
        beforeRevision,
        afterState,
        afterRevision,
        stateUpdateAttempts = attempts,
        successfulStateUpdates = writes,
        confirmationsIntercepted = confirmations,
        applicationEmailsSent = 0,
        workItemsCreated = 0,
        globalCleanupInvoked = false,
        complete,
        failure,
        checks
    }, new JsonSerializerOptions { WriteIndented = true }));
}

static void Ensure(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Verification failed: {name}.");
    }
}

public class ClosedProxy : DispatchProxy
{
    private Func<MethodInfo, object?[], object?>? _handler;
    public static T Create<T>(Func<MethodInfo, object?[], object?>? handler = null) where T : class
    {
        var value = Create<T, ClosedProxy>();
        ((ClosedProxy)(object)value)._handler = handler;
        return value;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        _handler?.Invoke(targetMethod!, args ?? []) ?? throw new InvalidOperationException($"Unexpected dependency blocked: {targetMethod?.Name}.");
}

internal sealed class InterceptNotification(Func<EmailPayload, Task> capture) : INotificationService
{
    public async Task<NotificationResult> SendEmailNotificationAsync(EmailPayload payload, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await capture(payload);
        return new NotificationResult(NotificationStatus.Disabled);
    }
}

internal sealed class BlockRequests : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        throw new InvalidOperationException("Application email requests are blocked for the single-plan test.");
}