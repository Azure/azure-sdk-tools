# Spec: Partner Self-Service - SLA Status Tool

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Alternatives Considered](#alternatives-considered)
- [Open Questions](#open-questions)
- [Success Criteria](#success-criteria)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)
- [Implementation Plan](#implementation-plan)
- [Testing Strategy](#testing-strategy)
- [Metrics/Telemetry](#metricstelemetry)

---

## Definitions

- **SLA**: Service Level Agreement — the maximum acceptable time for the SDK team or service team to respond to or resolve a customer-reported issue.
- **FQR (First Question Response)**: Time from issue creation to the first comment by a team member (not a bot).
- **Approaching SLA**: An issue within a configurable window (default 7 days) of breaching its SLA threshold.
- **Breached SLA**: An issue that has exceeded its SLA threshold without the required action.
- **Team member**: A GitHub user with `MEMBER` or `COLLABORATOR` association on the repo, excluding bot accounts.
- **Service label**: A GitHub label identifying which Azure service an issue belongs to (e.g., `KeyVault`, `Storage`).

---

## Background / Problem Statement

### Current State

Partner and service teams rely on the SDK team as intermediaries to check SLA status, issue queues, and ownership. The SDK team manually queries Kusto dashboards and composes emails to service leads. The existing infrastructure has comprehensive issue triage automation ([github-event-processor](../../tools/github-event-processor), [issue-labeler](../../tools/issue-labeler)) and well-defined triage labels (`customer-reported`, `needs-team-triage`, `needs-team-attention`), but no self-service query surface for SLA metrics.

### Why This Matters

The SDK team spends significant time acting as middlemen. Service team leads cannot independently check whether their issues are approaching SLA breach. This creates delays and scales poorly as more services onboard. See [#14115](https://github.com/Azure/azure-sdk-tools/issues/14115) and [#14116](https://github.com/Azure/azure-sdk-tools/issues/14116).

---

## Goals and Exceptions/Limitations

### Goals

- [ ] A service team lead can run `azsdk sla status --service KeyVault` and get SLA status without asking the SDK team
- [ ] Compute FQR, bug resolution, and question resolution metrics from live GitHub data
- [ ] Surface approaching and breached SLA issues with actionable detail
- [ ] Expose as both CLI command and MCP tool (`azsdk_sla_status`)

### Exceptions and Limitations

#### GitHub API Rate Limits

**Description:** Querying issues + comments across 7 SDK repos could hit rate limits (~5,000/hr for authenticated users).

**Impact:** Tool may return incomplete data for broad queries.

**Workaround:** Require `--repo` for initial version; use aggressive label+date filtering; surface rate limit warnings.

#### No Historical Trend Data

**Description:** GitHub API provides current state only — no pre-computed historical metrics.

**Impact:** Cannot show week-over-week SLA compliance trends.

**Workaround:** Phase 2 will add Kusto integration for historical analysis behind an `ISLADataProvider` interface.

---

## Design Proposal

### Overview

Add a new `SLAStatusTool` to azsdk-cli that queries the GitHub Issues API, computes SLA metrics, and returns a structured summary. The tool follows the existing dual CLI+MCP pattern.

### Architecture

```
┌─────────────────────────────────────────────┐
│  CLI: azsdk sla status --service KeyVault   │
│  MCP: azsdk_sla_status { service: "..." }   │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│  SLAStatusTool.cs  [McpServerToolType]      │
│  Orchestrates queries, formats output       │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│  ISLAMetricsService                         │
│  SLA computation, threshold checks,         │
│  team-member detection, business-day calc   │
└──────────┬──────────────┬───────────────────┘
           │              │
┌──────────▼────┐  ┌──────▼──────────────────┐
│ IGitHubService│  │ ISLAConfigProvider      │
│ (existing +   │  │ Thresholds, repo map,  │
│  new methods) │  │ defaults               │
└───────────────┘  └─────────────────────────┘
```

### SLA Metric Definitions

| Metric | Definition | Default Threshold |
|--------|-----------|-------------------|
| **FQR** | Issue creation → first team member comment | 3 business days |
| **Bug Resolution** | Bug issue creation → close | 90 calendar days |

> **Note on lookback window:** The default `--days` value (180) is intentionally 2× the longest SLA threshold (90d bug resolution). If the lookback equaled the SLA threshold, a bug would fall out of the query window the moment it breached — making breached issues invisible. The lookback must always exceed the longest SLA to capture both approaching and breached issues.
| **Question Resolution** | Question issue creation → close or `issue-addressed` label | 14 calendar days |

**Label-based filtering:**

- `customer-reported` → tracked for FQR
- `bug` → bug resolution SLA
- `question` → question resolution SLA
- `issue-addressed` → excluded from active tracking

**Team member detection:** `author_association` is `MEMBER`, `COLLABORATOR`, or `OWNER`; exclude accounts ending in `[bot]`.

### New GitHub Service Methods

The existing `IGitHubService` will be extended with:

```cs
Task<IReadOnlyList<Issue>> SearchIssuesAsync(string repo, string owner, IReadOnlyList<string> labels, DateTimeOffset? since, ItemStateFilter state, CancellationToken ct);
Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(string repo, string owner, int issueNumber, CancellationToken ct);
```

### GitHub API Query Strategy

```
GET /repos/{owner}/{repo}/issues?labels={service},{customer-reported}&state=open&since={lookback}&per_page=100
```

For FQR, fetch only the first comment per issue:

```
GET /repos/{owner}/{repo}/issues/{number}/comments?per_page=1
```

### Response Model

```cs
public class SLAStatusResponse : CommandResponse
{
    public string Service { get; set; }
    public SLASummary Summary { get; set; }
    public SLAMetricSummary FirstQuestionResponse { get; set; }
    public SLAMetricSummary BugResolution { get; set; }
    public SLAMetricSummary QuestionResolution { get; set; }
    public List<SLAIssueDetail> ApproachingBreaches { get; set; }
    public List<SLAIssueDetail> BreachedIssues { get; set; }
}

public class SLAMetricSummary
{
    public string MetricName { get; set; }
    public TimeSpan SLAThreshold { get; set; }
    public int TotalTracked { get; set; }
    public int WithinSLA { get; set; }
    public int Approaching { get; set; }
    public int Breached { get; set; }
    public double CompliancePercent { get; set; }
}

public class SLAIssueDetail
{
    public string IssueUrl { get; set; }
    public string Title { get; set; }
    public string Repo { get; set; }
    public string Assignee { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SLAStatus { get; set; }         // "ok" | "approaching" | "breached"
    public TimeSpan? TimeUntilBreach { get; set; }
    public string SLAMetricType { get; set; }     // "fqr" | "bug_resolution" | "question_resolution"
}
```

### File Structure

```
Azure.Sdk.Tools.Cli/
├── Tools/SLA/SLAStatusTool.cs
├── Services/SLA/
│   ├── ISLAMetricsService.cs
│   ├── SLAMetricsService.cs
│   ├── ISLAConfigProvider.cs
│   └── SLAConfigProvider.cs
├── Models/SLA/
│   ├── SLAStatusResponse.cs
│   ├── SLAMetricSummary.cs
│   ├── SLAIssueDetail.cs
│   └── SLAConfig.cs
```

**Modified files:** `ServiceRegistrations.cs` (register new services), `SharedOptions.cs` (add to `ToolsList`), `SharedCommandGroups.cs` (add `SLA` group).

---

## Alternatives Considered

### Alternative: Kusto-first approach

**Description:** Query existing Kusto SLA dashboards directly instead of computing metrics from GitHub API.

**Why not chosen:** No Kusto client exists in azsdk-cli. Adding one requires non-trivial auth and infrastructure. GitHub API provides real-time data and validates metric definitions before investing in Kusto integration. The `ISLAMetricsService` interface allows adding Kusto as a backend in Phase 2.

### Alternative: Place under `eng` command group

**Description:** Use `azsdk eng sla-status` instead of a new `sla` group.

**Why not chosen:** `eng` is described as "Internal azsdk engineering system commands." SLA status is partner-facing. A dedicated `sla` group is cleaner and extensible for future commands (`sla approaching`, `sla report`).

---

## Open Questions

- [ ] **SLA threshold values**: Are 3d FQR / 14d question / 90d bug correct? Need validation against existing dashboard definitions (azure-sdk-pr/issues/2550).
  - Context: Authoritative SLA definitions are in a private repo.
  - Options: Use these defaults and make configurable; validate with SDK team leads.

- [ ] **Default repo scope**: Should the tool query all 7 SDK repos by default, or require `--repo`?
  - Context: Querying all repos is expensive but gives the full picture.
  - Options: (A) Require `--repo` initially, (B) default to all repos with pagination.

- [ ] **Authentication**: Is `GITHUB_TOKEN` / `gh auth` sufficient for cross-repo queries, or do we need a service principal?
  - Context: Some Azure SDK repos may have different access requirements.

---

## Success Criteria

This tool is complete when:

- [ ] `azsdk sla status --service KeyVault --repo azure-sdk-for-python` returns correct FQR, bug, and question resolution metrics
- [ ] Approaching and breached SLA issues are surfaced with assignee and time-remaining detail
- [ ] MCP tool `azsdk_sla_status` returns the same data as the CLI command
- [ ] Metrics match a manual spot-check against GitHub issue data
- [ ] Unit tests cover SLA computation edge cases (no comments, bot-only comments, weekends)

---

## Agent Prompts

### Check SLA Status for a Service

**Prompt:**

```text
What's the SLA status for the KeyVault service in the Python SDK repo?
```

**Expected Agent Activity:**

1. Agent calls `azsdk_sla_status` with `service: "KeyVault"`, `repo: "azure-sdk-for-python"`
2. Agent summarizes the compliance percentages and highlights any approaching/breached issues

### Find Breached SLAs

**Prompt:**

```text
Are there any issues approaching SLA breach for the Storage service?
```

**Expected Agent Activity:**

1. Agent calls `azsdk_sla_status` with `service: "Storage"`
2. Agent focuses the response on the `ApproachingBreaches` and `BreachedIssues` lists

---

## CLI Commands

### SLA Status

**Command:**

```bash
azsdk sla status --service <label> [--repo <repo>] [--days <lookback>] [--approaching-window <days>]
```

**Options:**

- `--service <label>`: Service label to query (required)
- `--repo <repo>`: Specific repo name, e.g. `azure-sdk-for-python` (optional, defaults to all SDK repos)
- `--days <n>`: Look-back window in days (default: 180 — must exceed longest SLA threshold to capture breached issues)
- `--approaching-window <n>`: Days before breach to flag as approaching (default: 7)
- `--include-closed`: Include recently closed issues in metrics

**Expected Output:**

```text
SLA Status: KeyVault — azure-sdk-for-python (last 180 days)

  FQR compliance (3d SLA):        83.3%  (10/12)
  Bug resolution (90d SLA):       80.0%  (4/5)
  Question resolution (14d SLA):  85.7%  (6/7)

⚠ Approaching (2)
  #4521  "Cert rotation fails"       2d remaining  → @owner1
  #4498  "Managed HSM timeout"       5d remaining  → @owner2

🚨 Breached (1)
  #4312  "No response on auth issue" FQR 5d overdue → unassigned
```

**Error Cases:**

```text
Error: Service label "FooBar" not found in azure-sdk-for-python.
Available labels: KeyVault, Storage, EventHubs, ...
```

---

## Implementation Plan

### Phase 1: Core SLA Status Tool

- Define `ISLAConfigProvider` with default thresholds and repo mappings
- Extend `IGitHubService` with issue search and comment listing
- Implement `ISLAMetricsService` with FQR, bug, and question resolution computation
- Implement `SLAStatusTool` with CLI + MCP dual interface
- Register services and add to tool list
- Unit tests with mocked GitHub responses

### Phase 2: Automation & Notifications (future — [#14116](https://github.com/Azure/azure-sdk-tools/issues/14116))

- Kusto integration behind `ISLADataProvider` interface
- `azsdk sla approaching` filtered view
- GitHub issue comment notifications for approaching SLA
- Historical trend reporting

---

## Testing Strategy

### Unit Tests

- SLA computation with mocked GitHub issue/comment data
- Business-day calculation (weekday-only, spanning weekends)
- Team-member detection (MEMBER vs. bot vs. external user)
- Edge cases: issues with no comments, bot-only comments, closed issues

### Integration Tests

- End-to-end tool invocation with recorded GitHub API responses
- Verify CLI output formatting matches expected table format

---

## Metrics/Telemetry

| Metric | Description | Purpose |
|--------|-------------|---------|
| `azsdk_sla_status.invocations` | Count of tool invocations | Track adoption |
| `azsdk_sla_status.duration_ms` | Execution time | Monitor API performance / rate limit impact |
| `azsdk_sla_status.repos_queried` | Number of repos queried per invocation | Understand usage patterns |
| `azsdk_sla_status.rate_limit_warnings` | Count of rate limit warnings | Detect scaling issues |

Telemetry follows existing `InstrumentedTool` pattern — no additional PII is collected.
