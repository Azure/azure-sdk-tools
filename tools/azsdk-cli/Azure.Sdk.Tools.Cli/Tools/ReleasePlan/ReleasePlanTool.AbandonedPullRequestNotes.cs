// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan;

public partial class ReleasePlanTool
{
    private const string SdkGeneratorLogin = "azure-sdk-automation[bot]";

    private async Task RepairAbandonedPullRequestNotesAsync(ReleasePlanWorkItem plan, ReleaseWorkflowResponse response, CancellationToken ct)
    {
        // Legacy plans with no SDK links need neither GitHub access nor an ADO write.
        if (plan.SDKInfo?.Count == 0)
        {
            return;
        }
        try
        {
            var targetId = plan.WorkItemId;
            var planId = plan.ReleasePlanId;
            var revision = plan.Revision;
            var isTest = plan.IsTestReleasePlan;
            if (plan.SDKInfo == null || plan.SDKInfo.Any(sdk => sdk == null))
            {
                throw new InvalidOperationException("SDK details are unavailable.");
            }
            var links = GetManualAbandonmentPullRequests(plan);
            var fresh = await devOpsService.GetReleasePlanForWorkItemAsync(targetId, ct).WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (fresh == null || fresh.WorkItemId != targetId || fresh.ReleasePlanId != planId
                || fresh.Revision != revision || fresh.IsTestReleasePlan != isTest
                || !string.Equals(fresh.Status, "Abandoned", StringComparison.OrdinalIgnoreCase)
                || fresh.SDKInfo == null || fresh.SDKInfo.Any(sdk => sdk == null)
                || !links.SetEquals(GetManualAbandonmentPullRequests(fresh)))
            {
                throw new InvalidOperationException("The abandoned plan or its SDK links changed during verification.");
            }
            // Null values request live PR reads. Each failure is isolated from the other links.
            await AddAbandonedPullRequestNotesAsync(targetId, fresh.ReleasePlanLink,
                links.ToDictionary(url => url, _ => (PullRequest?)null, StringComparer.Ordinal), response, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning(ex, "Could not repair abandoned PR notes for release plan {WorkItemId}", plan.WorkItemId);
            AddAbandonedNoteWarning(response, plan.WorkItemId, "Could not verify the current canonical SDK links; no PR comments were added.", confirmedAbandoned: false);
        }
    }

    private async Task AddAbandonedPullRequestNotesAsync(int workItemId, string planLink, IReadOnlyDictionary<string, PullRequest?> pullRequests,
        ReleaseWorkflowResponse response, CancellationToken ct)
    {
        var id = workItemId.ToString(CultureInfo.InvariantCulture);
        var marker = $"<!-- azsdk-abandoned-release-plan:{id} -->";
        // ReleasePlanLink is a read-only mapping from numeric IDs and fixed public/test dashboard constants,
        // not an externally supplied URL or user text. The marker always uses the primary work-item ID.
        var body = $"{marker}\nRelease plan (work item {id}): [dashboard]({planLink}) has been abandoned. "
            + "This notice applies only to that plan; this PR is left open because it may contain shared or still-needed work.";
        foreach (var (url, verified) in pullRequests.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Reuse guard reads for known non-generated or closed PRs; do not expand their permissions.
                if (verified != null && !IsOpenGeneratedPullRequest(verified))
                {
                    if (verified.State.Value == ItemState.Open && !verified.Merged && verified.User?.Login == null)
                    {
                        AddAbandonedNoteWarning(response, workItemId, $"Skipped {url}: generated PR ownership could not be verified.");
                    }
                    continue;
                }
                var parts = new Uri(url).AbsolutePath.Trim('/').Split('/');
                var number = int.Parse(parts[3], CultureInfo.InvariantCulture);
                var pr = await githubService.GetPullRequestAsync(parts[0], parts[1], number, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (pr == null || pr.State.Value is not (ItemState.Open or ItemState.Closed))
                {
                    throw new InvalidOperationException("Current PR state could not be verified.");
                }
                if (!IsOpenGeneratedPullRequest(pr))
                {
                    if (pr.Merged || (pr.State.Value == ItemState.Open && pr.User?.Login == null))
                    {
                        AddAbandonedNoteWarning(response, workItemId, $"Skipped {url}: PR is merged or generated ownership is unknown.");
                    }
                    continue;
                }
                var comments = await githubService.GetPullRequestIssueCommentsAsync(parts[0], parts[1], number, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (comments == null)
                {
                    throw new InvalidOperationException("Existing PR comments could not be verified.");
                }
                // Exact line and exact plan ID: another plan's note cannot suppress this one.
                if (comments.Any(comment => comment?.Body?.Split('\n')
                    .Any(line => string.Equals(line.TrimEnd('\r'), marker, StringComparison.Ordinal)) == true))
                {
                    continue;
                }
                // State may have changed while paging comments. Never knowingly notify a closed/merged PR.
                pr = await githubService.GetPullRequestAsync(parts[0], parts[1], number, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (pr == null || pr.State.Value is not (ItemState.Open or ItemState.Closed))
                {
                    throw new InvalidOperationException("Current PR state could not be verified before commenting.");
                }
                if (!IsOpenGeneratedPullRequest(pr))
                {
                    AddAbandonedNoteWarning(response, workItemId, $"Skipped {url}: PR state or generated ownership changed before commenting.");
                    continue;
                }
                await githubService.CreatePullRequestCommentAsync(parts[0], parts[1], number, body, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                response.Details.Add($"Added an abandonment note for plan {id} to {url}; PR state unchanged.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The plan is already saved; a canceled POST may still have reached GitHub.
                logger.LogWarning("PR note operation canceled after release plan {WorkItemId} was abandoned; inspect comments before retrying", workItemId);
                throw;
            }
            catch (Exception ex)
            {
                ct.ThrowIfCancellationRequested();
                logger.LogWarning(ex, "Could not add abandoned release-plan note to {PullRequestUrl}", url);
                AddAbandonedNoteWarning(response, workItemId, $"Could not verify or add a PR note at {url}. No comment POST is retried in this invocation.");
            }
        }
    }

    private static bool IsOpenGeneratedPullRequest(PullRequest pr) => pr.State.Value == ItemState.Open && !pr.Merged
        && string.Equals(pr.User?.Login, SdkGeneratorLogin, StringComparison.OrdinalIgnoreCase);

    private static void AddAbandonedNoteWarning(ReleaseWorkflowResponse response, int workItemId, string message, bool confirmedAbandoned = true)
    {
        // ReleaseWorkflowResponse has Details/NextSteps, not a Warnings property.
        var state = confirmedAbandoned ? $"plan {workItemId} remains Abandoned." : $"No ADO state changes were made while repairing notes for plan {workItemId}.";
        response.Details.Add($"Warning: {state} {message}");
        response.NextSteps ??= [];
        var guidance = $"Inspect linked PR comments, then retry abandonment for work item {workItemId} to repair missing notes without rewriting ADO state, or add the plan-specific notice manually. PRs are never closed by this operation.";
        if (!response.NextSteps.Contains(guidance, StringComparer.Ordinal))
        {
            response.NextSteps.Add(guidance);
        }
    }
}
