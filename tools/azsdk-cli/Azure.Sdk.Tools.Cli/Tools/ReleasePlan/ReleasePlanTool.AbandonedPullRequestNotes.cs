// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan;

public partial class ReleasePlanTool
{
    private async Task CloseAbandonedPullRequestsAsync(ReleasePlanWorkItem plan, ReleaseWorkflowResponse response, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        HashSet<string> links;
        try
        {
            if (plan.SDKInfo == null || plan.SDKInfo.Any(sdk => sdk == null))
            {
                throw new InvalidOperationException("SDK details are unavailable.");
            }
            links = GetManualAbandonmentPullRequests(plan);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning(ex, "Could not verify abandoned release-plan SDK links for plan {ReleasePlanId}", plan.ReleasePlanId);
            AddAbandonedPullRequestWarning(response, plan.ReleasePlanId, "Could not verify canonical SDK links; no PR cleanup was attempted.");
            return;
        }
        var id = plan.ReleasePlanId.ToString(CultureInfo.InvariantCulture);
        var marker = $"<!-- azsdk-abandoned-release-plan:{plan.WorkItemId.ToString(CultureInfo.InvariantCulture)} -->";
        // ReleasePlanLink is a read-only mapping from numeric IDs and fixed public/test dashboard constants,
        // not an externally supplied URL or user text. The marker always uses the primary work-item ID.
        var body = $"{marker}\nRelease plan [{id}]({plan.ReleasePlanLink}) has been abandoned. Closing this linked SDK PR.";
        foreach (var url in links.Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var parts = new Uri(url).AbsolutePath.Trim('/').Split('/');
                var number = int.Parse(parts[3], CultureInfo.InvariantCulture);
                var pr = await githubService.GetPullRequestAsync(parts[0], parts[1], number, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (pr == null || pr.State.Value is not (ItemState.Open or ItemState.Closed))
                {
                    throw new InvalidOperationException("Current PR state could not be verified.");
                }
                if (pr.State.Value != ItemState.Open || pr.Merged)
                {
                    continue;
                }
                var comments = await githubService.GetPullRequestIssueCommentsAsync(parts[0], parts[1], number, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (comments == null)
                {
                    throw new InvalidOperationException("Existing PR comments could not be verified.");
                }
                if (!comments.Any(comment => comment?.Body?.Split('\n')
                    .Any(line => string.Equals(line.TrimEnd('\r'), marker, StringComparison.Ordinal)) == true))
                {
                    ct.ThrowIfCancellationRequested();
                    await githubService.CreatePullRequestCommentAsync(parts[0], parts[1], number, body, ct).WaitAsync(ct);
                    ct.ThrowIfCancellationRequested();
                }
                // Comment and close are separate requests. Never close if the note could not be read/created.
                await githubService.ClosePullRequestAsync(parts[0], parts[1], number, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                response.Details.Add($"Closed {url} with an abandonment note for release plan {id}.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // A canceled comment or close may still have reached GitHub.
                logger.LogWarning("PR cleanup canceled for abandoned release plan {ReleasePlanId}; inspect PRs before retrying", plan.ReleasePlanId);
                throw;
            }
            catch (Exception ex)
            {
                ct.ThrowIfCancellationRequested();
                logger.LogWarning(ex, "Could not clean up abandoned release-plan PR {PullRequestUrl}", url);
                AddAbandonedPullRequestWarning(response, plan.ReleasePlanId, $"Could not complete PR cleanup at {url}. No comment or close request is retried in this invocation.");
            }
        }
    }

    private static void AddAbandonedPullRequestWarning(ReleaseWorkflowResponse response, int releasePlanId, string message)
    {
        // ReleaseWorkflowResponse has Details/NextSteps, not a Warnings property.
        response.Details.Add($"Warning: plan {releasePlanId} remains Abandoned. {message}");
        response.NextSteps ??= [];
        var guidance = $"Inspect linked PR states and comments, then retry abandonment with releasePlanId {releasePlanId} to finish cleanup without rewriting ADO state, or manually add the plan-specific notice and close the linked open unmerged SDK PRs.";
        if (!response.NextSteps.Contains(guidance, StringComparer.Ordinal))
        {
            response.NextSteps.Add(guidance);
        }
    }
}
