# Data gap investment backlog

This backlog distinguishes missing source data from data that V2 can recover through exact joins. Priorities assume the goal is reliable fleet-wide timelines, not merely a current-status dashboard.

See [`e2e-metrics-coverage.md`](e2e-metrics-coverage.md) for the canonical mapping from these investments to the L1-L3, S1-S6, Q1-Q8, C1-C4, and B1-B2 display metrics.

## P0: foundational correlation and timestamps

| Gap | Evidence/impact | Recommended investment | V2 fallback |
|---|---|---|---|
| One mutable SDK PR field per language | Cannot represent multiple packages, retries, replacement PRs, or one PR reused by several releases without revision reconstruction | Add an SDK Artifact child record per plan/language/package with stable artifact ID, repository, PR, generation run, release run, and lifecycle | Convert parent field revisions into time-bounded edges |
| Active spec PR is mutable | One sampled API Spec child changed active PR three times; current value loses the earlier attempts | Store spec PRs as structured artifact links/children with role and superseded state | Diff API Spec child revisions and parse review-history links |
| Release Plan ID is not guaranteed on generated artifacts | Broad GitHub discovery is expensive and ambiguous | Put immutable Release Plan work item ID and artifact ID in every generated PR body/label and pipeline variable | Use exact Release Plan URL fields only; do not guess missing links |
| Exact release run is mostly absent | `ReleasePipelineFor*` coverage on finished plans was 0–5%; release duration cannot be trusted | Always write release definition/run/stage IDs and URL when release is queued | Use release-status revision as approximate observation time |
| Exact package publication time is absent | Revision time can lag the registry; merge-to-publish is a key metric | Write immutable published timestamp, package, version, feed, and release run ID after feed verification | Query package registry/release metadata; otherwise mark approximate |
| PR status is stale | 459 of 948 released language instances retained nonterminal PR status | Stop treating ADO PR status as synchronized truth, or update it through merge/close with event time | GitHub is authoritative; retain ADO status only as an observation |
| Release version is incomplete | 308 of 948 released instances lacked a released version | Make version required when setting release status to released; validate package feed result | Registry lookup; show released/version-unknown distinctly |
| Intended artifact cardinality is implicit | `SDKLanguages` is only 54.1% populated overall and cannot describe multiple packages in one language | Persist explicit plan-language-package intents with required/optional/excluded state | Infer from package fields and revision evidence with confidence |

## P1: workflow and bottleneck attribution

| Gap | Why it matters | Recommended investment | V2 fallback |
|---|---|---|---|
| Generation attempts are flattened | A final pipeline URL/status hides retries, queue time, and failure recovery | Append attempt records with run ID, queue/start/end, result, trigger, failure category, and superseded PR | Fetch exact linked run; revisions reveal only some attempts |
| Release attempts/approvals are flattened | Cannot separate waiting for approval from pipeline execution | Record release attempt and approval pending/start/end timestamps | Azure Pipeline timeline when exact run exists |
| “Waiting on” is inferred | Current action-owner logic uses status heuristics | Emit structured stage, blocker category, responsible role, and entered/exited timestamps | Deterministic stage rules with an `inferred` badge |
| Exclusion history is under-specified | Exclusion, missing emitter, not applicable, and no intended SDK are materially different | Store reason code, requested/approved timestamps, approver role, and applicability | Diff exclusion fields; preserve unknown separately |
| API readiness is a current string | Cannot distinguish authoring, ready-for-review, approval, and merge | Record milestone timestamps or rely explicitly on GitHub event IDs | GitHub ready/merge events |
| APIView lifecycle is not structured | API review can be a major wait but URL/status is found in comments | Write APIView ID, URL, status, requested/completed timestamps to API Spec artifact | Parse public bot comments, lower confidence |
| Build failure category is missing | “Failed” does not explain emitter, test, infra, or configuration friction | Emit stable failure codes and failed stage/job IDs | Classify pipeline timeline/check names deterministically |
| Manual intervention is not explicit | V1 infers it from phrases and post-generation commits | Emit typed intervention events from the generation/release tools | Show “human-touched” from GitHub facts; do not claim manual fix |

## P2: behavioral analysis and efficiency

| Gap | Why it matters | Recommended investment | V2 fallback |
|---|---|---|---|
| Tool telemetry lacks universal correlation | Tool use cannot be assigned safely to a plan | Include Release Plan ID, artifact ID, PR URL, package, language, invocation ID, result, and duration | Exclude uncorrelated calls from plan timelines |
| Check history is expensive | Current check status is easy; historical failure/resolution requires more calls or retained telemetry | Write summarized check transitions to the artifact/run record | Refresh active GitHub checks and retain normalized snapshots |
| Review ownership transitions are implicit | Requested reviewer changes and handoffs affect queue attribution | Store requested-reviewer events or collect GitHub timeline events | GitHub timeline, if available |
| Package work item linkage is name-based | Name changes and collisions can select the wrong latest package record | Persist Package work item ID on each SDK artifact | Name/language lookup with ambiguity warning |
| Service/path identity changes over time | Current path is missing on 52.4% of the one-year population and can be renamed | Add stable service/project ID plus path history | API Spec PR changed-file derivation and Service Tree ID |
| Duplicate/abandoned reason is coarse | These plans distort cycle and failure metrics if treated as incomplete | Add structured terminal reason and superseding plan ID | State/reason revision and related-link inspection |

## P3: optional qualitative insight

| Gap | Recommendation |
|---|---|
| Nag detection depends on phrases and identity assumptions | Keep as an optional, explainable classifier over public comments; report precision samples and never use it as a core KPI without review |
| Sentiment is not operationally defined | Do not publish sentiment by default. Prefer objective blockers, response time, review state, and explicit action requests |
| Narrative bottleneck summaries can drift | Generate them from versioned deterministic metrics, include the calculation window, and keep the source metrics visible |
| User comparisons can be misleading | Require minimum sample sizes, role/cohort controls, and privacy review; default to process-level insights |

## Collector investments that avoid source changes

These can be implemented in V2 without changing Release Plan tooling:

1. Partitioned WIQL inventory and incremental `ChangedDate` watermarks.
2. Parent and API Spec child revision diffing.
3. Exact PR deduplication and immutable caching after merge/close.
4. Many-to-many, time-bounded plan/PR edges.
5. Exact build lookup from recorded run URLs.
6. Package registry lookup for missing versions/timestamps.
7. Provenance and confidence on every normalized event.
8. Completeness dashboards and regression thresholds.

## Recommended instrumentation contract

The most durable fix is an append-only artifact event contract emitted by release tooling:

```json
{
  "schemaVersion": 1,
  "releasePlanWorkItemId": 35326,
  "artifactId": "35326:dotnet:Azure.ResourceManager.NetApp:1",
  "language": ".NET",
  "package": "Azure.ResourceManager.NetApp",
  "eventType": "sdk-pr-linked",
  "occurredAt": "2026-07-15T17:22:00Z",
  "sourceRunId": "6680000",
  "pullRequest": "https://github.com/Azure/azure-sdk-for-net/pull/61030",
  "result": "succeeded"
}
```

This could be stored as child work items, an append-only Azure table/blob, or another queryable event store. The Release Plan remains the operator-facing summary, while the event stream becomes the timeline-grade source.

## Suggested success targets

After instrumentation:

- 100% of generated PRs carry Release Plan and artifact IDs.
- 100% of generation and release attempts have stable run IDs and timestamps.
- 100% of released artifacts have package, version, and publication time.
- Fewer than 1% of terminal artifacts disagree with GitHub PR state.
- Fewer than 2% of intended artifacts end in unexplained `unknown`.
- Incremental collection performs no repository-wide PR searches.
- Every displayed duration can link to its start/end evidence and confidence.
