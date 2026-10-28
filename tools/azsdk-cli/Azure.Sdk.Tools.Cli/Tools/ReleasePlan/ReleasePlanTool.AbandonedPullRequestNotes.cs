// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan;

public partial class ReleasePlanTool
{
    private async Task CloseAbandonedPullRequestsAsync(ReleasePlanWorkItem plan, ReleaseWorkflowResponse response, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        HashSet<(string RepoName, int Number)> links;
        try
        {
            if (plan.SDKInfo == null || plan.SDKInfo.Any(sdk => sdk == null))
            {
                throw new InvalidOperationException("SDK details are unavailable.");
            }
            links = GetSdkPullRequests(plan);
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
        var body = $"<!-- azsdk-abandoned-release-plan:{plan.ReleasePlanId} -->\nRelease plan [{plan.ReleasePlanId}]({plan.ReleasePlanLink}) has been abandoned. Closing this linked SDK PR.";
        foreach (var (repoName, number) in links.OrderBy(link => link.RepoName, StringComparer.Ordinal).ThenBy(link => link.Number))
        {
            var url = $"https://github.com/Azure/{repoName}/pull/{number}";
            ct.ThrowIfCancellationRequested();
            try
            {
                // Cancellation stops waiting, not a request already accepted by GitHub.
                var pr = await githubService.GetPullRequestAsync(url, ct).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (pr == null || pr.State.Value is not (ItemState.Open or ItemState.Closed))
                {
                    throw new InvalidOperationException("Current PR state could not be verified.");
                }
                if (pr.State.Value != ItemState.Open || pr.Merged)
                {
                    continue;
                }
                // Two non-atomic writes: close first, then add the notice. Closed PRs are never repaired on retry.
                await githubService.ClosePullRequestAsync("Azure", repoName, number, ct, body).WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
                response.Details.Add($"Closed {url} with an abandonment note for release plan {plan.ReleasePlanId}.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                ct.ThrowIfCancellationRequested();
                logger.LogWarning(ex, "Could not clean up abandoned release-plan PR {PullRequestUrl}", url);
                AddAbandonedPullRequestWarning(response, plan.ReleasePlanId, $"Could not complete PR cleanup at {url}. The PR may already be closed or its notice may be missing; inspect it and add the notice manually where needed. No write is automatically retried.");
            }
        }
    }

    private static void AddAbandonedPullRequestWarning(ReleaseWorkflowResponse response, int releasePlanId, string message)
    {
        // ReleaseWorkflowResponse has Details/NextSteps, not a Warnings property.
        response.Details.Add($"Warning: plan {releasePlanId} remains Abandoned. {message}");
        response.NextSteps ??= [];
        var guidance = $"Review all SDK pull requests linked to release plan {releasePlanId}; close any remaining open unmerged PRs and add the abandonment notice where missing.";
        if (!response.NextSteps.Contains(guidance, StringComparer.Ordinal))
        {
            response.NextSteps.Add(guidance);
        }
    }
}
