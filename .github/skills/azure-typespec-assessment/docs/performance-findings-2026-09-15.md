# Performance Findings: 12-Report Reassessment Batch

## Scope

This report analyzes the fresh reassessment batch completed on September 15-16,
2026 for 12 historical Azure REST API specification pull requests.

Primary evidence:

- session task `8a2b80d5-b429-4246-92e7-5e5be10fcc95`;
- supplemental incomplete PR 43718 session
  `0e7d3e43-4386-4e6b-b330-5c5e995dc6f7`;
- retained `performance-reassessment-20260915\batch-summary.json`;
- per-case `result.json`, `execution.json`, and `preparation-manifest.json`;
- `initial-timing-audit.json`;
- the 12 rendered assessment artifacts and their input accounting;
- historical phase timings in
  `evals\fixtures\execution-time-breakdowns.json`.

Wall-clock durations include tool, scheduling, and process waits. They must not
be interpreted as CPU time or active model execution time.

## Executive summary

The largest observed bottleneck was batch orchestration and environment
contention. The batch launched 12 assessments concurrently and completed in
approximately 10 hours. Synchronized multi-hour gaps appeared in compiler,
source-evidence, and Agent phases, so the overnight wall time is not
representative of active processing.

After excluding those abnormal gaps, the largest normal-path cost is the Agent
workflow: reading and inspecting inputs, constructing Azure Guidelines
evidence, judging documentation and compatibility, and materializing the final
report. Deterministic document evidence extraction is already negligible.

| Metric                                |               Result |
| ------------------------------------- | -------------------: |
| Reports                               |                   12 |
| Batch elapsed time                    | Approximately 10h 3m |
| Reports meeting the 300-second target |                    0 |
| Initial batch median analysis time    |               2m 58s |
| Initial batch median total time       |              13m 47s |
| Clean retry reference, PR 44200       |              22m 40s |
| PR 44200 environment analysis         |               4m 18s |
| PR 44200 Agent and finalization       |              18m 22s |

The current practical performance is approximately 14-23 minutes per report
when the run is not affected by an overnight scheduling gap. The five-minute
target requires improvements to both environment preparation and the Agent/tool
workflow.

## Direct coordinator validation: PR 43718

The PR-aware coordinator was validated on September 16, 2026 by invoking
`run-assessment-analysis.mjs --pr` directly against PR 43718. The command
resolved PR metadata, reused or fetched the required commits, derived the
`specification/networkcloud` scope, created verified sparse worktrees, and
produced `model-input.json` without a separate full checkout or manual project
search.

| Setup phase               |  Time |
| ------------------------- | ----: |
| PR metadata               |  2.6s |
| Required-object fetch     |  0.2s |
| Scope discovery           |  0.1s |
| Sparse workspace creation |  7.6s |
| Project discovery         | 18.9s |
| Setup excluding fetch     | 29.9s |

This replaces the observed 18m 51s agent-driven delay before the coordinator
was invoked and meets the under-60-second setup target. The setup reduction is
approximately 97.4%.

There are two useful ways to measure this run:

| Measurement boundary                               |          Time |
| -------------------------------------------------- | ------------: |
| Instrumented coordinator (`timings.totalMs`)       |  **8m 50.7s** |
| Coordinator launch to `assessment.json` generation | **20m 56.2s** |
| Coordinator launch to assembly command completion  | **21m 09.0s** |

The coordinator started at `2026-09-16T08:30:39.873Z`.
`assessment.json` records `generatedAt` as
`2026-09-16T08:51:36.072Z`, and the guarded assembly, validation, and rendering
command completed at `2026-09-16T08:51:48.917Z`. The previously reported 20m
44s value was a phase-rounded session estimate. The timestamp-derived
**20m 56.2s** is the more precise end-to-end report-generation measurement;
**21m 09.0s** includes the remainder of the command that wrote and checked the
artifacts.

The coordinator's instrumented 8m 50.7s consisted of:

| Coordinator work                                          |         Time | Share |
| --------------------------------------------------------- | -----------: | ----: |
| PR setup excluding fetch                                  |        29.9s |  5.6% |
| Required-object fetch                                     |         0.2s |  0.0% |
| Four TypeSpec compiler invocations                        |     2m 52.6s | 32.5% |
| Semantic, REST, downstream, and documentation analyzers   |        14.5s |  2.7% |
| Remaining preparation, source evidence, and normalization |     5m 13.5s | 59.1% |
| **Instrumented coordinator total**                        | **8m 50.7s** |       |

The compiler total is the sum of baseline AutoRest (54.553s), baseline TCGC
(45.345s), target AutoRest (36.312s), and target TCGC (36.424s). The remaining
5m 13.5s is calculated by subtraction because the manifest does not expose
separate timers for every dependency, source-indexing, artifact-processing, and
normalization step.

For continuity with the original session analysis, the rounded phase view was:

| Report phase                                      |        Time |    Share |
| ------------------------------------------------- | ----------: | -------: |
| Direct setup, compilation, and deterministic work |      9m 05s |    43.8% |
| Bounded evidence review and Agent judgment        |     10m 56s |    52.7% |
| Assembly, validation, and HTML rendering          |         43s |     3.5% |
| **Rounded session estimate**                      | **20m 44s** | **100%** |

The 10m 56s bounded-evidence and judgment interval can be reconstructed from
tool and artifact timestamps as:

| Evidence and judgment activity                         | Estimated wall time | Notes |
| ------------------------------------------------------ | ------------------: | ----- |
| Post-analysis handoff and verification                 |               2m 09s | Read coordinator output, checked provenance and repository cleanliness, and waited for the explicit continue request |
| Rules, schemas, and bounded-input loading              |               1m 57s | Loaded classification, downstream, search, catalog, judgment schemas, and the 1.34 MiB model input |
| Semantic/downstream evidence extraction                |               2m 00s | Summarized seven intents, ten SDK candidates, eleven inference requests, and exact before/after facts; REST had no candidates |
| Azure Guidelines ranking, retrieval, and inspection    |               2m 46s | Read query profiles and catalog metadata, then fetched four shared official documents concurrently |
| Inference decision completion and serialization        |               1m 00s | Completed and wrote the eleven bounded inference decisions |
| Shared guideline evidence and per-intent compliance    |                  44s | Materialized shared evidence and the seven intent-level guideline decisions |
| Final bounded judgment serialization                   |                  20s | Wrote semantic, REST, downstream, and compliance decisions |
| **Total**                                              |          **10m 56s** | Wall-clock reconstruction; overlapping tool durations are not summed |

The approximately **3m** semantic/downstream evidence and inference interval
breaks down as:

| Semantic, downstream, and inference activity       | Estimated wall time | Detail |
| -------------------------------------------------- | ------------------: | ------ |
| Extract and inspect bounded judgment evidence      |                  59s | Produced a compact view of intents, candidates, requests, and referenced facts, then inspected the retained output |
| Summarize exact downstream before/after differences |                  43s | Compared the ten SDK candidates and extracted the precise changed contracts |
| Load inference contract and transition to search   |                  18s | Read the inference schema and prepared the eleven bounded decisions |
| Complete and serialize inference decisions         |               1m 00s | Finalized no-impact/candidate conclusions, wrote `inference.json`, and checked the result |
| **Total semantic/downstream and inference time**   |           **3m 00s** | Rounded reconstruction from tool and artifact timestamps |

REST did not add a separate judgment cost because this run had no REST
candidates. Semantic summaries and downstream compatibility shared the first
59 seconds of bounded evidence extraction, so assigning that interval entirely
to either dimension would be misleading. The clearest optimization target is
the repeated extraction and inspection of global facts before the small
4,499-byte inference result was written.

Azure Guidelines work therefore accounted for approximately **3m 30s**:
2m 46s for ranking, retrieval, and document inspection plus 44s to materialize
shared evidence and per-intent compliance decisions.

| Azure Guidelines activity                            | Estimated wall time | Detail |
| ---------------------------------------------------- | ------------------: | ------ |
| Load combined query profiles                         |                  25s | Read the seven intent query profiles once |
| Read, score, and select from the canonical catalog   |                  27s | Ranked the catalog across all intents and selected four documents |
| Fetch four selected official documents               |                  37s | Four fetches ran concurrently; summed request durations were higher than wall time |
| Inspect documents and map guidance to intent queries |               1m 17s | Searched the fetched content and reviewed compact request profiles |
| Materialize shared evidence and compliance decisions |                  44s | Wrote shared retrieval evidence and seven per-intent applicability decisions |
| **Total Azure Guidelines wall time**                 |          **3m 30s** | Rounded reconstruction from tool and artifact timestamps |

The network portion was only about 37 seconds because the four documents were
fetched concurrently. Approximately 2m 09s was spent loading, ranking, and
inspecting evidence, and another 44s was spent constructing the validated
shared evidence and per-intent decisions. This indicates that further gains
depend more on compact deterministic search inputs and reusable parsed document
evidence than on increasing fetch concurrency.

Documentation Completeness did not consume a separately identifiable Agent
judgment interval. It was derived deterministically from compiler evidence in
**2 ms** and was already present before bounded Agent judgment began. General
rules and schema loading may support all dimensions, but assigning part of that
shared 1m 57s specifically to documentation would be speculative.

The direct invocation removes the avoidable pre-coordinator work but does not
make the full report a sub-five-minute operation. Compilation and source
evidence still dominate deterministic work, while bounded evidence inspection,
Azure Guidelines retrieval, inference, and judgment remain the largest
end-to-end phase. Approximately 12m 05.5s elapsed between the end implied by
the coordinator timer and `assessment.json` generation. That interval includes
Agent/tool work, user-turn latency, and waiting, so it must not be interpreted
as 12 minutes of active model computation.

The validated output contained seven Semantic intents, no REST breaking
candidates, ten downstream SDK candidates, and eleven bounded inference
requests. The final report confirmed nine SDK model-type changes, rejected one
compatible optional method-parameter extension, and completed with no
assessment blockers. The source repository branch, commit, index, and working
tree remained unchanged.

The raw TCGC artifacts were also substantial:

| TCGC artifact |      Bytes |      Size |
| ------------- | ---------: | --------: |
| Baseline      |  5,205,162 |  4.96 MiB |
| Target        |  6,365,374 |  6.07 MiB |
| Combined      | 11,570,536 | 11.03 MiB |

These raw files are not sent directly to the Agent, but their size affects
compiler output generation and deterministic normalization. The bounded
`model-input.json` for this run was approximately 1.34 MiB.

## Environment and deterministic analysis

PR 44200 provides the cleanest complete retry without a multi-hour phase:

| Phase                                              |                  Time |
| -------------------------------------------------- | --------------------: |
| Dependency setup                                   |                   99s |
| Compilation                                        |                   83s |
| Source evidence                                    |                   41s |
| Workspace preparation                              |                   15s |
| Semantic, REST, downstream, and document analyzers | Less than 3s combined |
| Recorded analysis wall time                        |                  258s |

This shows that the deterministic analyzers are not the main cost. Dependency
setup, compilation, and source collection consume almost the entire environment
phase.

The retry batch also contains clear contention artifacts:

- five compiler phases lasted approximately 8.4 hours;
- five Agent phases lasted approximately 8.3 hours;
- PR 44988 spent approximately 8.5 hours in source evidence;
- dependency setup had a retry median near 26.6 minutes, compared with 99
  seconds for PR 44200.

These synchronized durations indicate queueing, suspension, cache locking, or
shared process contention. The retained telemetry does not prove which one
caused each gap, so these durations must not be attributed directly to
compiler, source-index, or model performance.

## Agent judgment and tool overhead

The successful retry records provide a post-analysis `agentWallMs` measurement
for PRs 42435, 44200, and 44988. This is the elapsed interval from deterministic
analysis completion through validated report rendering. It includes model
reasoning, file and schema reads, Azure Guidelines retrieval, tool scheduling,
shell startup, waiting, judgment authoring, and finalization. It is therefore
an **AI/tool workflow wall time**, not pure model inference time.

| PR    | Semantic intents | AI/tool workflow | Finalization | Before finalization |
| ----- | ---------------: | ---------------: | -----------: | ------------------: |
| 42435 |                1 |      **14m 59.2s** |        20.6s |       **14m 38.6s** |
| 44200 |                2 |      **18m 21.7s** |         8.4s |       **18m 13.3s** |
| 44988 |               11 |      **23m 26.9s** |        18.0s |       **23m 08.9s** |

The "before finalization" value is `agentWallMs` minus the separately recorded
finalization window. It is the tightest available upper bound for AI judgment
and its supporting tool work. It must not be labeled active model time because
the retained records do not separate token generation from reads, commands,
scheduling, and waits.

The report-size trend is sublinear: PR 44988 has 11 times as many Semantic
intents as PR 42435, but its measured AI/tool workflow is only 1.56 times
longer. Shared setup and context loading are substantial fixed costs, while
batching work across intents amortizes part of the per-intent cost. Dividing
these wall times by intent count would be misleading because the work overlaps
and final decisions are authored together.

### Detailed PR 44200 judgment timeline

PR 44200 is the only one of these three reports with a nonoverlapping timeline
reconstructed from visible Agent turns and tool timestamps:

| Observed workflow window                                               |          Time | Share of AI/tool workflow |
| ---------------------------------------------------------------------- | ------------: | ------------------------: |
| Shared references, schemas, bounded input, and catalog setup           |     4m 19.6s |                     23.6% |
| Read 41 descriptions, downstream facts, and inference schema           |        59.8s |                      5.4% |
| Guidelines scoring, ranking, retrieval, linkage, and supporting reads  |    10m 36.0s |                     57.7% |
| Author combined decisions, correct one excerpt, and start finalization |     2m 06.3s |                     11.5% |
| Finalize evidence, assemble, validate, and render                      |         8.4s |                      0.8% |
| Initial setup and analysis-return handling not assigned above          |        11.6s |                      1.1% |
| **Total**                                                              | **18m 21.7s** |                  **100%** |

These are observed workflow windows, not exclusive dimension-level model
timers. The 59.8-second evidence read combines documentation and SDK facts, and
the 2m 06.3s decision window authors multiple dimensions together.

The separately instrumented PR 44200 finalization stages were:

| Finalization stage               |    Time |
| -------------------------------- | ------: |
| Compliance evidence plan         |    2.7s |
| Assessment assembly              |    1.6s |
| Whole-report validation          |    0.4s |
| HTML rendering                   |    2.5s |
| Wrapper and inter-stage overhead | 1.2s |
| **Finalization window**          | **8.4s** |

For PRs 42435 and 44988, the retained artifacts expose only aggregate
`agentWallMs` and finalization timings. No defensible category-level split
between Azure Guidelines, compatibility judgment, documentation judgment,
input inspection, and waiting can be reconstructed from those artifacts.

The older 11-report phase fixture contains the following dimension estimates
for these PRs. These values are useful as workload-shape references, but they
are **not a breakdown of the successful retry wall times above**:

| PR    | Semantic and documentation | REST | Downstream | Azure Guidelines | Other overhead | Historical total |
| ----- | -------------------------: | ---: | ---------: | ---------------: | -------------: | ---------------: |
| 42435 |                     5m 10s | 1m 20s |      1m 20s |           3m 12s |            29s |        11m 31.3s |
| 44200 |                      4m 14s | 2m 20s |         55s |           4m 15s |            24s |        12m 08.4s |
| 44988 |                      8m 40s | 1m 55s |       1m 07s |           3m 16s |         2m 04s |        17m 03.0s |

The historical fixture marks Semantic, REST, and downstream values as
estimated; most Azure Guidelines values are measured, while overhead is
derived. Documentation judgment is mixed into the Semantic estimate and cannot
be separated. The successful retries used different orchestration and took
14m 59.2s, 18m 21.7s, and 23m 26.9s respectively, so the historical dimension
values must not be added to or subtracted from the retry table.

The best defensible breakdown by PR is therefore:

- **PR 42435:** 14m 38.6s before finalization and 20.6s finalization; no
  category-level retry timeline is retained. Historical workload reference:
  5m 10s Semantic/documentation, 1m 20s REST, 1m 20s downstream, 3m 12s
  Guidelines, and 29s overhead.
- **PR 44200:** use the observed 18m 21.7s timeline above. The historical
  dimension fixture is secondary and should not replace that observed retry
  breakdown.
- **PR 44988:** 23m 08.9s before finalization and 18.0s finalization; no
  category-level retry timeline is retained. Historical workload reference:
  8m 40s Semantic/documentation, 1m 55s REST, 1m 07s downstream, 3m 16s
  Guidelines, and 2m 04s overhead.

### Effect of the current simplified workflow

The measurements above predate the current Documentation Completeness and
shared-guidance changes. Those runs performed Agent documentation judgment and
selected four Azure Guidelines documents per Semantic intent. The current
workflow instead:

- determines documentation presence without an AI decision;
- ranks Azure Guidelines once across all Semantic intents;
- fetches and stores four guidance documents once per report.

The historical timings therefore overstate the AI work required by the current
workflow, especially for PR 44988, which previously selected 44 guidance
documents for 11 intents. The final three refreshed reports reused existing
validated Semantic, REST, and downstream artifacts, so they do not provide a
clean replacement benchmark. A new warm-cache run with phase instrumentation
is required to measure the current AI judgment cost.

Tool interaction count is a material contributor:

- PR 44200 used 44 recorded tool calls;
- PR 44988 used 93 recorded tool calls;
- individual reads and shell calls commonly took 30-60 seconds;
- agents repeatedly inspected schemas and inputs and created temporary scripts
  to author plans and judgments.

The workflow therefore pays substantial fixed latency before and between actual
judgment steps.

### Supplemental incomplete run: PR 43718

The September 16 PR 43718 run is not part of the 12-report aggregate because it
did not produce a judgment or report. It is still useful for separating
deterministic analysis from Agent overhead:

| Phase                                      |              Time |
| ------------------------------------------ | ----------------: |
| PR discovery and pre-assessment setup      |           18m 51s |
| Deterministic analysis                     |            25m 7s |
| Uninstrumented setup before compilation    | Approximately 14m |
| Four TypeSpec compiler invocations         |           10m 58s |
| Deterministic dimension analyzers combined | Approximately 20s |
| Post-analysis Agent artifact inspection    | Approximately 16m |
| Completed Agent judgment and finalization  |     Not completed |

The compiler time consisted of:

| Compiler invocation |   Time |
| ------------------- | -----: |
| Baseline AutoRest   | 2m 24s |
| Baseline TCGC       | 1m 12s |
| Target AutoRest     | 4m 44s |
| Target TCGC         | 2m 38s |

Recorded deterministic dimension timings were 2.626 seconds for Semantic
intents, 0.695 seconds for REST, 16.448 seconds for downstream SDK impact, and
0.002 seconds for Documentation Completeness. Azure Guidelines preparation and
judgment were not separately timed or completed.

The largest measured analysis costs were the approximately 14-minute interval
before the first compiler invocation and the approximately 11 minutes of
compilation. The preparation manifest does not subdivide the first interval
among dependency setup, workspace preparation, source collection, and waiting,
so attributing it to any single activity would be speculative.

After analysis, the Agent spent approximately 16 minutes repeatedly inspecting
artifacts but did not write `compliance-search-evidence.json`,
`assessment-judgment.json`, `assessment.json`, or `assessment.html`. Therefore,
this interval is tool and reasoning overhead, not a valid per-dimension
judgment measurement. The run ended after approximately 61 minutes with an
incorrect early-completion decision.

## Azure Guidelines performance

Azure Guidelines is the second-largest measured active phase after general
semantic and Agent reasoning.

Historical full-assessment measurements show:

| Metric                                   | Result |
| ---------------------------------------- | -----: |
| Mean Azure Guidelines time per report    | 3m 18s |
| Median                                   | 3m 16s |
| Minimum                                  |    49s |
| Maximum                                  | 7m 51s |
| Share of historical full assessment time |  26.2% |

The current 12 reports contain:

| Workload                              |               Result |
| ------------------------------------- | -------------------: |
| Semantic intents                      |                   32 |
| Selected guideline documents          |                  128 |
| Unique selected URLs                  |                   25 |
| Repeated selections                   |                  103 |
| Downloaded content                    |              51.6 MB |
| Average content per selected document | Approximately 413 KB |

The four-documents-per-intent policy makes guideline work scale linearly with
semantic intent count. PR 44988 alone selected 44 documents for 11 intents and
downloaded 21.7 MB.

Only 25 unique URLs were selected across the batch. A batch-level URL and
content-hash cache could therefore reuse 103 of the 128 selections, an 80.5%
selection-reuse opportunity. Existing per-case receipts do not eliminate
cross-case downloads and parsing.

Other guideline costs include:

- scoring the full catalog independently for every intent;
- repeated helper invocations for plan authoring, ranking, fetching, and
  finalization;
- reading large retained pages to locate small relevant excerpts;
- preserving four successful documents even when the same references apply to
  many intents.

## Documentation assessment performance

The 12 reports assessed 229 eligible descriptions. Deterministic document
evidence extraction took only 32 ms across the retained preparation timings, so
optimizing that code would not materially improve end-to-end performance.

Documentation still contributes to Agent time because every eligible
description requires a semantic comparison with its declaration. PR 44200
assessed 41 descriptions, and PR 44988 assessed 102. This work is currently
mixed into `agentWallMs`, so its exact active duration cannot be separated from
guideline and compatibility judgment.

Several PR 44988 documentation units remained blocked because declaration
ownership or compiler context was unavailable. Better deterministic ownership
evidence would improve both completeness and performance by preventing the
Agent from investigating units that cannot be resolved.

## Historical phase distribution

The historical 11-report phase breakdown attributes full assessment time as:

| Phase                                      | Share |
| ------------------------------------------ | ----: |
| Semantic understanding and Agent reasoning | 40.8% |
| Azure Guidelines                           | 26.2% |
| REST judgment                              | 15.5% |
| Downstream judgment                        |  9.7% |
| Other overhead                             |  7.8% |

Current deterministic semantic, REST, downstream, and document analyzers run in
milliseconds to seconds for most cases. The historical percentages primarily
represent Agent reasoning and interaction rather than deterministic analyzer
CPU time.

## Performance gaps and recommended actions

### 1. Separate active time from wall time

Record start, end, active execution, and wait time independently for:

- dependency setup;
- workspace preparation;
- each compiler invocation;
- source evidence;
- deterministic analyzers;
- model-input reading;
- guideline scoring, fetching, searching, and comparison;
- documentation judgment;
- REST/downstream judgment;
- assembly, validation, and rendering.

Do not calculate AI cost as `totalWallMs - analysisWallMs`; that assigns idle
and scheduling gaps to the Agent.

### 2. Limit compilation concurrency

Do not run 12 dependency and compiler pipelines concurrently against shared
writable caches. Prewarm the toolchain once, then use approximately three or
four isolated assessment lanes. Prefer immutable shared packages or per-case
writable cache locations where necessary.

### 3. Add a batch-level guideline cache

Fetch each canonical guideline URL once per batch and reuse the retained bytes,
content hash, and parsed searchable representation across cases. Preserve
per-intent ranking and applicability decisions, but avoid repeated network and
parsing work.

### 4. Reduce Agent tool round trips

Provide deterministic commands that:

- summarize all bounded model input and schemas in one response;
- materialize the compliance plan from compact Agent decisions;
- materialize the final judgment from compact dimension decisions;
- assemble, validate, and render in one guarded finalization command.

Avoid repeated ad hoc Node and PowerShell probes and temporary authoring
scripts.

### 5. Batch judgments by semantic intent

Judge related documentation descriptions and compatibility candidates in one
structured operation per semantic intent while preserving one exact decision
per required item. This reduces repeated context loading without reducing
coverage.

### 6. Improve deterministic documentation ownership

Resolve generated and augmented declaration ownership before Agent judgment.
Mark genuinely unsupported units early and provide the exact blocker so the
Agent does not spend time attempting an unresolvable review.

## Suggested performance targets

| Phase                                       |                                  Target |
| ------------------------------------------- | --------------------------------------: |
| Warm environment and deterministic analysis |                              At most 2m |
| Azure Guidelines                            | At most 1m per report with shared cache |
| All Agent judgments                         |                              At most 2m |
| Assembly, validation, and rendering         |                             At most 15s |
| End-to-end warm report                      |                              At most 5m |

Measure these targets first with controlled warm-cache single-report runs, then
with three- or four-lane batch runs. Do not use the September 15 overnight retry
wall times as the baseline because they include unresolved multi-hour waits.
