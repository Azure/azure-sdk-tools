# V2 research findings

Research date: **August 11, 2026**

## Executive findings

1. A Release Plan is the right correlation root. Its work item and API Spec child connect the intended scope, TypeSpec project, SDK languages, generation runs, SDK PRs, release state, and package versions without broad GitHub search.
2. Work item revisions are sufficiently detailed to reconstruct major machine-driven transitions. They are not a substitute for GitHub history: PR review, merge, comments, commits, check failures, and current status still need GitHub.
3. Correlation must be many-to-many and time-aware. In the one-year snapshot, 79 spec PR URLs were linked to multiple plans and 47 SDK PR URLs were linked to multiple plans. API Spec children can also replace the active spec PR over time.
4. Current Release Plan status fields cannot be treated as authoritative snapshots of GitHub. Of 948 language instances marked released, 459 retained a nonterminal PR status. A spot check found the linked GitHub PR was merged even though the Release Plan still said `ready for review`.
5. V1's display model remains strong, but its discovery pipeline is the main scalability problem. V2 should preserve release-window focus, contextual metrics, compacted idle time, grouped language lanes, filters, details, and links while replacing heuristic PR discovery with Release Plan linkage.

## Research method

The investigation covered:

- V1 documentation, schemas, data-generation skill, fetch/process scripts, rendering code, styles, and existing service datasets.
- A Playwright CLI walkthrough of the V1 homepage and Container Registry full-service view at 1600 x 1000, including release-window and all-history modes.
- The `release-plan-dashboard` Azure DevOps mapping, GitHub enrichment, caching, routing, stage calculation, card rendering, and per-language actions.
- An authenticated one-year Azure DevOps profile using [`scripts/profile-release-plans.js`](../scripts/profile-release-plans.js).
- A stratified sample of 30 Release Plan revision histories and their API Spec children.
- A GitHub comparison for a completed SDK PR to verify which timeline facts remain available and authoritative there.

The profile is point-in-time evidence, not a permanent baseline. The revision sample deliberately spans states and is useful for shape and density, not population-level estimates.

## V1 visual and interaction language

### Information architecture

V1 has two useful levels:

- **Sample/service chooser** organized into full-service histories, tool-assisted flows, standard flows, and in-flight work.
- **Timeline workspace** with a service header, release-window selector, KPI cards, event and actor filters, bar legend, synchronized lane labels and timeline, insights, tooltips, and a detail drawer.

The full-service view defaults to the latest API-version release window. Users can switch to all history, where lanes aggregate many PRs, or expand a language into individual PR lanes.

### Visual language worth retaining

- GitHub-like dark/light palette, system sans-serif typography, monospaced dates and durations.
- Fixed-width lane metadata beside a horizontally scrollable timeline.
- Language color badges and clear merged/open/closed/draft/release bar patterns.
- Event markers with stable shapes for creation, ready-for-review, approval, merge, release, comments, commits, nags, and failures.
- Orange/red emphasis for long idle gaps, pipeline delay, pending releases, and blocking events.
- Compact release-window pills and contextual KPI cards.
- Top and bottom time axes, zoom controls, synchronized hover, and compressed long gaps.
- Progressive disclosure: aggregate lane first, then expanded PR lanes, tooltip, then full detail drawer.

### Metrics currently displayed

Per release window:

- API/spec review duration
- Spec merge to SDK PR pipeline gap
- Fastest and slowest SDK PR
- End-to-end duration
- Review wait and wait-cycle count
- Author nags
- Manual fixes on automated PRs
- Subsequent edits on manual PRs
- Unique reviewers
- Release gap
- Pending release count
- Active time and tool-call success when present

Across a service:

- Spec and SDK PR counts
- Average release-window cycle
- Average pipeline and review wait
- Automation rate
- Fastest and slowest language
- Top reviewer
- Nag, manual-fix, and tool-call totals
- Per-language PR duration and release counts

### V1 collection limitations

The full-service skill estimates roughly 200–900 GitHub calls for a typical service:

1. Find commits touching the TypeSpec path.
2. Infer spec PRs from those commits.
3. Search each SDK repository by spec PR URL and merge SHA.
4. Fall back to commits touching package paths.
5. Fetch PR, comments, reviews, review comments, commits, issue events, and changed files.
6. Exclude mass changes, release noise, and sibling packages heuristically.
7. Infer release windows from API versions, PR bodies, changed paths, and temporal proximity.
8. Infer release pipelines from language-specific naming patterns and inspect recent builds.

This is expensive and cannot reliably distinguish legitimate shared PRs, replaced PRs, sibling packages, or missing correlation from a false negative. V2 should retain event processing only after authoritative linkage has established the entity set.

## Release Plan dashboard analysis

### Collection

The existing dashboard:

- Queries active Release Plan work items plus plans finished in the last 60 days.
- Fetches all parent fields and hierarchy-forward children.
- Maps one API Spec child per Release Plan.
- Models `.NET`, JavaScript, Python, Java, and Go through repeated language-specific fields.
- Extracts the active spec PR and previous spec PR links from API Spec fields.
- Enriches spec and SDK PR current status, labels, changed-file-derived project path, approvals, failed checks, requested reviewers, APIView URL, and latest human comment from GitHub.
- Enriches package versions from Package work items and release classifications from `azure-sdk` release CSVs.
- Caches the current result for one hour and lazy-loads detailed PR information.

This is a current-state dashboard. It intentionally discards parent and child revision history and therefore cannot calculate stage durations or reconstruct replaced values.

### Display logic to reuse conceptually

The dashboard supplies a valuable workflow vocabulary:

1. Create Spec PR
2. API Spec Review
3. Generate SDK
4. SDK Review & Merge
5. Release SDK

It also calculates current action ownership:

- Service team
- Spec PR reviewer
- SDK PR reviewer

Expanded cards add plane, release type, intended month, product metadata, spec status, package/version, generation and release pipeline links, exclusion state, package-feed links, and contextual actions. V2 should reuse this stage model as a summary above the detailed Gantt, but calculate it from timestamped events rather than current strings.

### Code and model constraints

- A single field per language supports only one current PR, package, generation run, and release run.
- The active spec PR is a mutable field; previous values are partly retained in HTML and fully recoverable only through revisions.
- `SDKPullRequestStatusFor*` is not kept synchronized through merge/release.
- `ReleasePipelineFor*` is sparsely populated.
- The dashboard's TypeSpec-path GitHub fallback is useful for display but should not become V2's primary correlation rule.
- Current stage logic treats `Finished` as fully complete. V2 must preserve the actual artifact outcomes and distinguish completed, excluded, abandoned, superseded, and missing.

## Azure DevOps data profile

### Inventory

The one-year, date-partitioned query found **652 Release Plans**:

| State | Count |
|---|---:|
| Finished | 224 |
| Abandoned | 197 |
| In Progress | 198 |
| New | 32 |
| Duplicate | 1 |

There were 535 management-plane and 117 data-plane plans, with no `both` or `unspecified` classifications in this snapshot. Creation sources were 426 Release Planner, 184 Copilot, and 42 Automation.

### Parent and child coverage

| Signal | Coverage |
|---|---:|
| API Spec child | 649 / 652 (99.5%) |
| Release Plan ID | 616 / 652 (94.5%) |
| Product Service Tree ID | 597 / 652 (91.6%) |
| Intended SDK release month | 594 / 652 (91.1%) |
| SDK release type | 573 / 652 (87.9%) |
| API Spec review-history PR link | 518 / 652 (79.4%) |
| Active API Spec PR link | 446 / 652 (68.4%) |
| Intended SDK languages | 353 / 652 (54.1%) |
| TypeSpec project path | 314 / 652 (48.2%) |

The path and language fields have much better coverage in the newest cohort, indicating rollout rather than uniformly random loss. Backfill still needs the API Spec child and GitHub changed-file fallback for older plans.

### Finished-plan artifact coverage

For intended, non-private-preview, non-excluded language instances on finished plans:

| Field | .NET | JavaScript | Python | Java | Go |
|---|---:|---:|---:|---:|---:|
| SDK PR | 75.3% | 74.7% | 75.8% | 74.9% | 76.0% |
| Generation pipeline | 67.7% | 67.2% | 66.3% | 66.9% | 68.4% |
| Release status | 91.2% | 89.7% | 90.5% | 90.3% | 86.6% |
| Released version | 60.0% | 59.2% | 59.0% | 57.7% | 53.2% |
| Release pipeline | 0% | 0.2% | 3.9% | 5.1% | 4.1% |

Absence is not always an error: one PR can cover multiple plans, an artifact can be excluded, or an older workflow may not have populated a newer field. The collector must preserve `missing`, `not applicable`, `excluded`, and `unknown` as different states.

### Correlation shape

- 487 unique spec PR URLs were observed.
- 79 spec PR URLs appeared on multiple Release Plans; the maximum was 13 plans.
- 1,210 unique SDK PR URLs were observed.
- 47 SDK PR URLs appeared on multiple Release Plans; the maximum was four plans.
- No profiled plan had multiple API Spec children, but one API Spec child can contain a sequence of active PRs.

One completed sample's API Spec child changed its active spec PR three times. Revision history therefore converts mutable current fields into a reliable link-validity timeline. Shared URLs should be modeled as legitimate edges, not force-deduplicated into a one-plan/one-PR assumption.

### Revision usefulness

The 30-plan stratified sample had:

- Median 6 parent revisions
- 90th percentile 54 parent revisions
- Maximum 67 parent revisions
- Median 1 revision per unique API Spec work item linked from the sampled plans
- 90th percentile 3 revisions per sampled API Spec work item

Useful timestamped parent transitions included:

- API spec approval changes
- Generation pipeline URL assignment
- Per-language generation `Pending`, `In progress`, `Completed`, and failure values
- SDK PR URL assignment and replacement
- SDK PR status changes
- Release status and released version assignment
- Release pipeline URL assignment where available
- Release Plan state changes

The parent revision timestamp is the time the orchestration updated the work item. It is an authoritative observation time, but not necessarily the exact external event time. Exact PR and build timestamps should replace it when the linked system can supply them.

### Confirmed quality issues

Across 2,694 intended language instances:

- 948 were marked released.
- 308 released instances lacked `ReleasedVersionFor*`.
- 459 released instances retained a nonterminal `SDKPullRequestStatusFor*`.
- 125 linked SDK PRs lacked a generation pipeline link.
- 23 generation-completed instances lacked an SDK PR.
- 11 plans had approved spec status but no active spec PR link.

These are reasons to join systems, not reasons to reject Release Plans as the root. The Release Plan says what should be correlated; GitHub and Azure Pipelines say what happened in those systems.

## Source-of-truth conclusion

Use the following precedence:

| Fact | Primary source | Fallback |
|---|---|---|
| Plan identity, intended scope/languages, exclusion, product | Release Plan revisions | Current Release Plan |
| Spec PR linkage over time | API Spec child revisions | Review-history HTML, then active field |
| SDK PR linkage over time | Release Plan revisions | Current language field |
| PR state and event timestamps | GitHub | Release Plan observation timestamp |
| Generation attempt/run | Exact Release Plan pipeline URL | Generation status revision |
| Release run and stage | Exact Release Plan pipeline URL | Release status revision |
| Published version/time | Package registry/release metadata | Released version and its revision time |
| Human behavior | GitHub comments, reviews, commits | None |
| Tool behavior | Correlated tool telemetry | None |

Every derived event should retain source, observed time, effective time, and confidence so the UI can explain approximations.
