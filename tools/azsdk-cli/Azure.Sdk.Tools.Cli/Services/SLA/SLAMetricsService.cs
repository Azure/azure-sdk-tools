// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses.SLA;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Services.SLA;

/// <summary>
/// Core SLA metrics computation service. Fetches GitHub issues by service label,
/// categorizes them by type (customer-reported, bug, non-bug customer issue), and computes
/// compliance against configured SLA thresholds.
///
/// Data flow:
///   1. Query issues from GitHub API by service label + lookback window
///   2. Categorize each issue by its labels (customer-reported → FQR, bug, non-bug customer issue)
///   3. For customer-reported issues, fetch comments to find first team response
///   4. Compute per-metric compliance (within SLA, approaching, breached)
///   5. Return structured response with actionable issue lists
/// </summary>
public class SLAMetricsService(
    IGitHubService gitHubService,
    ISLAConfigProvider config,
    ILogger<SLAMetricsService> logger
) : ISLAMetricsService
{
    public async Task<SLAStatusResponse> ComputeSLAStatusAsync(
        string serviceLabel,
        string? repo,
        int lookbackDays,
        int approachingWindowDays,
        bool includeClosed,
        CancellationToken ct)
    {
        var repos = repo != null ? [repo] : config.DefaultRepos.ToList();
        var since = DateTimeOffset.UtcNow.AddDays(-lookbackDays);
        var now = DateTimeOffset.UtcNow;

        // Step 1: Fetch issues from each repo, and validate service label only for explicit repo queries.
        // Errors on individual repos are logged and surfaced as response warnings.
        var allIssues = new List<(Issue Issue, string RepoName)>();
        var failedRepos = new List<string>();

        foreach (var repoName in repos)
        {
            try
            {
                if (repo != null)
                {
                    var labels = await gitHubService.ListRepositoryLabelsAsync(config.RepoOwner, repoName, ct);
                    if (!labels.Contains(serviceLabel, StringComparer.OrdinalIgnoreCase))
                    {
                        var availableLabels = string.Join(", ", labels.OrderBy(l => l).Take(25));
                        throw new InvalidOperationException(
                            $"Service label '{serviceLabel}' not found in {repoName}. Available labels: {availableLabels}");
                    }
                }

                var issues = await gitHubService.ListIssuesForSLAAsync(
                    config.RepoOwner, repoName, serviceLabel, since, includeClosed, ct);

                foreach (var issue in issues)
                {
                    allIssues.Add((issue, repoName));
                }
            }
            catch (Exception ex)
            {
                if (repo != null && ex is InvalidOperationException)
                {
                    throw;
                }

                failedRepos.Add(repoName);
                logger.LogWarning(ex, "Failed to query issues from {Owner}/{Repo} for label '{Label}'",
                    config.RepoOwner, repoName, serviceLabel);
            }
        }

        // Step 2: Categorize issues by label into the three SLA metric buckets.
        // An issue can appear in multiple buckets (e.g., customer-reported + bug).
        var customerReportedIssues = new List<(Issue Issue, string Repo)>();
        var bugIssues = new List<(Issue Issue, string Repo, bool IsAddressed)>();
        var customerIssueResolutionIssues = new List<(Issue Issue, string Repo, bool IsAddressed)>();

        foreach (var (issue, repoName) in allIssues)
        {
            var labels = issue.Labels.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var isCustomerReported = config.CustomerReportedLabels.Any(l => labels.Contains(l));
            var isBug = labels.Contains(config.BugLabel);
            var isAddressed = labels.Contains(config.IssueAddressedLabel);

            if (isCustomerReported)
            {
                customerReportedIssues.Add((issue, repoName));
            }

            if (isBug)
            {
                bugIssues.Add((issue, repoName, isAddressed));
            }

            if (isCustomerReported && !isBug)
            {
                customerIssueResolutionIssues.Add((issue, repoName, isAddressed));
            }
        }

        var fqrIssues = await FetchFirstTeamCommentsAsync(customerReportedIssues, ct);

        // Step 3: Compute SLA metrics for each bucket.
        // The approaching/breached lists are populated as side effects by each metric computation.
        var approaching = new List<SLAIssueDetail>();
        var breached = new List<SLAIssueDetail>();

        var fqrMetric = ComputeFQRMetric(fqrIssues, now, approachingWindowDays, approaching, breached);
        var bugMetric = ComputeResolutionMetric("Bug Resolution", config.BugResolutionThresholdDays,
            bugIssues, now, approachingWindowDays, approaching, breached, "bug_resolution");
        var customerIssueMetric = ComputeResolutionMetric("Question Resolution", config.QuestionResolutionThresholdDays,
            customerIssueResolutionIssues, now, approachingWindowDays, approaching, breached, "question_resolution");

        // Sort actionable lists: breached first (most overdue), then approaching (least time remaining)
        approaching.Sort((a, b) => (a.TimeUntilBreachDays ?? 0).CompareTo(b.TimeUntilBreachDays ?? 0));
        breached.Sort((a, b) => (a.TimeUntilBreachDays ?? 0).CompareTo(b.TimeUntilBreachDays ?? 0));

        var warnings = new List<string>();
        if (failedRepos.Count > 0)
        {
            warnings.Add(
                $"Failed to query {failedRepos.Count} repo(s): {string.Join(", ", failedRepos.OrderBy(r => r))}. Results may be incomplete.");
        }

        return new SLAStatusResponse
        {
            Service = serviceLabel,
            Repo = repo,
            LookbackDays = lookbackDays,
            TotalOpenIssues = allIssues.Count(i => i.Issue.State.Value == ItemState.Open),
            CustomerReportedOpen = fqrIssues.Count(i => i.Issue.State.Value == ItemState.Open),
            FirstQuestionResponse = fqrMetric,
            BugResolution = bugMetric,
            QuestionResolution = customerIssueMetric,
            ApproachingBreaches = approaching.Count > 0 ? approaching : null,
            BreachedIssues = breached.Count > 0 ? breached : null,
            Warnings = warnings.Count > 0 ? warnings : null,
        };
    }

    private async Task<List<(Issue Issue, string Repo, IssueComment? FirstTeamComment)>> FetchFirstTeamCommentsAsync(
        List<(Issue Issue, string Repo)> issues,
        CancellationToken ct)
    {
        const int commentFetchBatchSize = 10;

        var results = new List<(Issue Issue, string Repo, IssueComment? FirstTeamComment)>();

        foreach (var batch in issues.Chunk(commentFetchBatchSize))
        {
            var fetchTasks = batch.Select(async item =>
            {
                try
                {
                    var firstTeamComment = await gitHubService.GetFirstTeamIssueCommentAsync(
                        config.RepoOwner,
                        item.Repo,
                        item.Issue.Number,
                        ct);

                    return (item.Issue, item.Repo, firstTeamComment);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to fetch comments for {Owner}/{Repo}#{Number}",
                        config.RepoOwner, item.Repo, item.Issue.Number);
                    return (item.Issue, item.Repo, (IssueComment?)null);
                }
            });

            results.AddRange(await Task.WhenAll(fetchTasks));
        }

        return results;
    }

    /// <summary>
    /// Computes the First Question Response (FQR) metric.
    /// FQR measures how quickly a team member responds to a customer-reported issue.
    /// Uses business days (Mon-Fri) for threshold comparison.
    ///
    /// Classification logic per issue:
    ///   - Has team comment within threshold → within SLA
    ///   - Has team comment beyond threshold → breached
    ///   - Open, no team comment, within threshold → within SLA
    ///   - Open, no team comment, approaching threshold → approaching
    ///   - Open, no team comment, past threshold → breached
    ///   - Closed without any team comment → breached
    /// </summary>
    private SLAMetricSummary ComputeFQRMetric(
        List<(Issue Issue, string Repo, IssueComment? FirstTeamComment)> issues,
        DateTimeOffset now,
        int approachingWindowDays,
        List<SLAIssueDetail> approaching,
        List<SLAIssueDetail> breached)
    {
        int totalTracked = 0;
        int withinSla = 0;
        int approachingCount = 0;
        int breachedCount = 0;

        foreach (var (issue, repo, firstTeamComment) in issues)
        {
            // Only track open issues without a team response, or issues where we can measure response time
            totalTracked++;

            if (firstTeamComment != null)
            {
                var responseBusinessDays = BusinessDayCalculator.CountBusinessDays(
                    issue.CreatedAt, firstTeamComment.CreatedAt);

                if (responseBusinessDays <= config.FqrThresholdBusinessDays)
                {
                    withinSla++;
                }
                else
                {
                    breachedCount++;
                    breached.Add(CreateIssueDetail(issue, repo, "breached", "fqr",
                        -(responseBusinessDays - config.FqrThresholdBusinessDays)));
                }
            }
            else if (issue.State.Value == ItemState.Open)
            {
                // No team response yet on an open issue
                var elapsedBusinessDays = BusinessDayCalculator.CountBusinessDays(issue.CreatedAt, now);
                var daysRemaining = config.FqrThresholdBusinessDays - elapsedBusinessDays;

                if (daysRemaining < 0)
                {
                    breachedCount++;
                    breached.Add(CreateIssueDetail(issue, repo, "breached", "fqr", daysRemaining));
                }
                else if (daysRemaining <= approachingWindowDays)
                {
                    approachingCount++;
                    approaching.Add(CreateIssueDetail(issue, repo, "approaching", "fqr", daysRemaining));
                }
                else
                {
                    withinSla++;
                }
            }
            else
            {
                // Closed without team response — count as breached
                var closedAt = issue.ClosedAt ?? now;
                var elapsedBusinessDays = BusinessDayCalculator.CountBusinessDays(issue.CreatedAt, closedAt);
                var daysRemaining = config.FqrThresholdBusinessDays - elapsedBusinessDays;

                breachedCount++;
                breached.Add(CreateIssueDetail(issue, repo, "breached", "fqr", daysRemaining));
            }
        }

        return new SLAMetricSummary
        {
            MetricName = "FQR",
            SLAThresholdDays = config.FqrThresholdBusinessDays,
            SLAThresholdDisplay = $"{config.FqrThresholdBusinessDays}bd",
            TotalTracked = totalTracked,
            WithinSLA = withinSla,
            Approaching = approachingCount,
            Breached = breachedCount,
            CompliancePercent = totalTracked > 0 ? (double)withinSla / totalTracked * 100 : 100,
        };
    }

    /// <summary>
    /// Computes a resolution-time SLA metric (used for both Bug Resolution and Customer Issue Resolution).
    /// Uses calendar days for threshold comparison.
    ///
    /// Classification logic per issue:
    ///   - Closed within threshold days → within SLA
    ///   - Open with issue-addressed label → treated as resolved at current time
    ///   - Closed beyond threshold days → breached
    ///   - Open, age within threshold → within SLA (or approaching if near threshold)
    ///   - Open, age beyond threshold → breached
    /// </summary>
    private SLAMetricSummary ComputeResolutionMetric(
        string metricName,
        int thresholdDays,
        List<(Issue Issue, string Repo, bool IsAddressed)> issues,
        DateTimeOffset now,
        int approachingWindowDays,
        List<SLAIssueDetail> approaching,
        List<SLAIssueDetail> breached,
        string metricType)
    {
        int totalTracked = 0;
        int withinSla = 0;
        int approachingCount = 0;
        int breachedCount = 0;

        foreach (var (issue, repo, isAddressed) in issues)
        {
            totalTracked++;

            if (issue.State.Value == ItemState.Closed && issue.ClosedAt.HasValue)
            {
                var resolutionDays = (issue.ClosedAt.Value - issue.CreatedAt).TotalDays;
                if (resolutionDays <= thresholdDays)
                {
                    withinSla++;
                }
                else
                {
                    breachedCount++;
                    breached.Add(CreateIssueDetail(issue, repo, "breached", metricType,
                        thresholdDays - resolutionDays));
                }
            }
            else if (isAddressed)
            {
                var resolutionDays = (now - issue.CreatedAt).TotalDays;
                if (resolutionDays <= thresholdDays)
                {
                    withinSla++;
                }
                else
                {
                    breachedCount++;
                    breached.Add(CreateIssueDetail(issue, repo, "breached", metricType,
                        thresholdDays - resolutionDays));
                }
            }
            else
            {
                // Open issue — check if approaching or breached
                var ageDays = (now - issue.CreatedAt).TotalDays;
                var daysRemaining = thresholdDays - ageDays;

                if (daysRemaining < 0)
                {
                    breachedCount++;
                    breached.Add(CreateIssueDetail(issue, repo, "breached", metricType, daysRemaining));
                }
                else if (daysRemaining <= approachingWindowDays)
                {
                    approachingCount++;
                    approaching.Add(CreateIssueDetail(issue, repo, "approaching", metricType, daysRemaining));
                }
                else
                {
                    withinSla++;
                }
            }
        }

        return new SLAMetricSummary
        {
            MetricName = metricName,
            SLAThresholdDays = thresholdDays,
            SLAThresholdDisplay = $"{thresholdDays}d",
            TotalTracked = totalTracked,
            WithinSLA = withinSla,
            Approaching = approachingCount,
            Breached = breachedCount,
            CompliancePercent = totalTracked > 0 ? (double)withinSla / totalTracked * 100 : 100,
        };
    }

    /// <summary>
    /// Creates an SLAIssueDetail for the approaching/breached lists.
    /// daysRemaining is positive for approaching issues, negative for breached.
    /// </summary>
    private static SLAIssueDetail CreateIssueDetail(Issue issue, string repo, string status, string metricType, double daysRemaining)
    {
        return new SLAIssueDetail
        {
            IssueUrl = issue.HtmlUrl?.ToString() ?? $"https://github.com/Azure/{repo}/issues/{issue.Number}",
            IssueNumber = issue.Number,
            Title = issue.Title.Length > 80 ? issue.Title[..77] + "..." : issue.Title,
            Repo = repo,
            Assignee = issue.Assignee?.Login,
            CreatedAt = issue.CreatedAt,
            SLAStatus = status,
            TimeUntilBreachDays = Math.Round(daysRemaining, 1),
            SLAMetricType = metricType,
        };
    }

}
