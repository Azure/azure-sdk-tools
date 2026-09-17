# Plan: Reduce Agent Evidence and Judgment Overhead

## Problem and measured baseline

The direct PR coordinator improvement reduced PR 43718 setup excluding fetch
from 18m 51s to 29.9s, but the complete validated report still took about
20m 56s. The largest remaining phase was bounded evidence review, Azure
Guidelines analysis, inference, and Agent judgment.

| Phase                                             |        Time |
| ------------------------------------------------- | ----------: |
| Direct setup, compilation, and deterministic work |      9m 05s |
| Bounded evidence review and Agent judgment        |     10m 56s |
| Assembly, validation, and HTML rendering          |         43s |
| Total rounded session estimate                    | **20m 44s** |

The 10m 56s interval included approximately two minutes waiting for an explicit
continuation request. Active evidence review and judgment was approximately
8m 54s.

The original bounded workload contained:

| Input or output                   |                        Result |
| --------------------------------- | ----------------------------: |
| Semantic intents                  |                             7 |
| REST candidates                   |                             0 |
| Downstream candidates             |                            10 |
| Inference requests                |                            11 |
| Facts                             |                           204 |
| `model-input.json`                | approximately 1,400,460 bytes |
| Minified `facts` section          |                 529,946 bytes |
| `inference.json`                  |                   4,499 bytes |
| `assessment-judgment.json`        |                  18,728 bytes |
| `compliance-search-evidence.json` |                  61,409 bytes |

The large read-to-decision ratio and repeated tool round trips were a larger
opportunity than deterministic analyzers. The four compiler invocations still
remain a separate optimization area.

## Goal

Reduce active evidence review, inference, and judgment to at most 5m 30s while
preserving the complete assessment workflow and report quality. Eliminate the
manual continuation pause and reduce finalization process wall time to at most
15 seconds.

The optimization must preserve:

- one concise result for every Semantic intent;
- exact deterministic and inferred REST/downstream candidate coverage;
- one Azure Guidelines decision for every assessed Semantic intent;
- shared guideline analysis across all assessed intents;
- compiler-derived Documentation Completeness;
- all blockers, provenance, source evidence, and stable IDs;
- validated `assessment.json` and complete `assessment.html`;
- the current read-only source-repository guarantees.

## Coordination with other performance work

This plan follows:

- `perf-improvement-plan.md`, direct PR/local coordinator invocation;
- the shared Azure Guidelines analysis change;
- `perf-improvement-plan1.md`, pairwise AutoRest/TCGC compilation, when
  enabled.

Setup, compiler, guideline, Agent, and finalization savings must be measured
independently before combined end-to-end improvements are reported.

## Implemented design

`model-input.json` is both the canonical bounded machine input and the single
Agent-facing evidence input. Evidence is not split into per-intent packets:
one bounded read avoids duplicated facts and lets the Agent judge related
intents and candidates together.

API-version publication/carry-over intents and API-version-wide changes do not
require model inference. They are removed from the Agent-facing workload and
restored deterministically in the final Semantic section as informational
intents. They retain their complete affected-operation list but cannot own
REST, downstream, Azure Guidelines, or Documentation Completeness findings.

```text
canonical deterministic evidence
                 |
                 v
      build bounded model-input.json
                 |
        +--------+---------+
        |                  |
        v                  v
 assessed intents     informational publication
 and evidence         metadata and operations
        |                  |
        v                  |
 agent-index.json          |
 and safe drafts           |
        |                  |
        +--------+---------+
                 |
                 v
       bounded Agent judgment
                 |
                 v
      finalize-assessment.mjs
 validate -> restore informational
 intent -> assemble -> render
```

Assembly and validation trust only canonical artifacts and validated Agent
outputs. Informational-intent reinsertion is deterministic and uses canonical
Semantic evidence rather than Agent-authored content.

## Informational API-version scope

An API-version publication review unit is informational when all of these
conditions hold:

- its grouping reason is `publication` or
  `cross-project:api-version-publication`;
- it has at least 20 affected operations;
- every operation is matched through `direct-version-governance` or
  `version-transition-change`.

The narrow predicate prevents ordinary version changes from bypassing model
judgment.

An `api-version-wide-change` is informational when:

- its changed declaration is `Versions`;
- it directly owns no operation;
- every associated operation is mapped through
  `direct-version-governance` or `version-transition-change`.

This classification does not use an operation-count threshold. It describes
the semantic origin of the operation set rather than its size.

An informational intent:

- remains in `dimensions.semantic.items`;
- has a deterministic title and summary;
- has `informational: true`;
- includes all canonical affected operations;
- is excluded from inference requests and Agent Semantic coverage;
- is excluded from Azure Guidelines requests;
- is excluded from Documentation Completeness input;
- cannot be referenced by REST, downstream, Guidelines, or documentation
  findings.

## Required workflow behavior

When `run-assessment-analysis.mjs` returns `awaiting-agent-judgment`, the Agent
continues immediately in the same task. It does not stop for a status summary
or require the user to say "continue."

The assessment is complete only when:

1. `assessment.json` validates and `assessment.html` is rendered; or
2. deterministic analysis reports an all-dimensions-blocked terminal result
   and renders the required blocked report.

The existence of `model-input.json`, a partial dimension blocker, or empty REST
candidates is not a terminal condition.

## Implementation

### Bounded model input

`run-assessment-analysis.mjs` builds one bounded `model-input.json` containing
only evidence required for assessed Semantic intents, their inference requests,
deterministic REST/downstream candidates, and shared Azure Guidelines
decisions.

The coordinator excludes informational API-version intents and their
exclusively associated candidates from inference, Guidelines requests,
Documentation Completeness input, and finding relationship matching. Canonical
IDs, source evidence, and referenced facts remain intact.
Physical and minified byte counts are recorded without treating estimated token
counts as actual model usage.

### Agent workspace

`build-agent-workspace.mjs` writes:

- `agent-workspace/agent-index.json`;
- `agent-workspace/inference.draft.json` when inference is required;
- `agent-workspace/assessment-judgment.draft.json`.

The compact index contains comparison identity, statuses and blockers, assessed
and informational intent IDs, counts, exact output/schema paths, the single
model-input path, and a completion checklist. It does not duplicate operation
or SDK fact bodies.

Drafts contain every required ID exactly once in stable order. Explicit
unresolved placeholders fail validation until the Agent supplies required
decisions, rationales, summaries, severities, or guidance references. Draft
generation never selects a success-shaped default.

### Shared Azure Guidelines evidence

The separately implemented shared guideline analysis is consumed once and
applied only to assessed intents. Documents are not refetched, rescored, or
reanalyzed per intent. Per-intent applicability and source/hunk coverage remain
required.

### Guarded finalization

`finalize-assessment.mjs --work <directory>`:

1. validates `inference.json`, shared guideline evidence, and
   `assessment-judgment.json`;
2. restores informational Semantic intents from canonical evidence;
3. assembles `assessment.json`;
4. validates the assembled assessment;
5. renders `assessment.html`;
6. writes workflow telemetry and reports the final artifact paths.

Failure produces compact actionable validation errors, preserves existing
evidence for correction, and never records a success-shaped state.

### Workflow state and resume

`workflow-state.json` is written atomically with these states:

- `preparing`;
- `awaiting-agent-judgment`;
- `agent-artifacts-written`;
- `finalizing`;
- `complete`;
- `blocked`.

It records artifact paths, content hashes, phase timestamps, telemetry, and
compact failure details. Resume reuses valid deterministic artifacts only when
comparison identity and canonical artifact hashes match.

### Skill execution rules

The skill requires:

- one `agent-index.json` read;
- one bounded `model-input.json` read;
- exact schema reads relative to the skill root;
- immediate continuation after deterministic analysis;
- guarded finalization instead of separate assembly, validation, and rendering
  commands;
- at most one correction turn after finalizer validation failure.

Normal execution does not recursively list artifacts, broadly search report
files, search for schema locations, or repeatedly inspect schemas.

## Correctness and quality validation

### Scope and completeness

- Every assessed Semantic intent appears exactly once in the bounded input.
- Every informational Semantic intent appears only in deterministic
  reinsertion metadata.
- Every deterministic and inferred candidate appears exactly once.
- Every inference request appears exactly once.
- Every referenced fact and canonical evidence reference remains resolvable.
- Blocked dimensions and partial blockers remain visible.
- No finding can relate to an informational intent.

### Draft and finalizer safety

- Unresolved drafts fail validation.
- Missing, duplicate, unknown, or unsupported IDs fail validation.
- Draft generation never preselects a decision.
- Empty candidate dimensions remain explicitly covered without fabricated
  findings.
- Invalid Agent artifacts cannot produce a complete workflow state.
- Finalization is idempotent for unchanged inputs.
- Partial documentation blockers retain the required `not-assessed` coverage
  and potential limit.

### Replay equivalence

Retained assessment fixtures are replayed through the optimized coordinator and
guarded finalizer. Comparisons cover:

- comparison identity;
- Semantic intent IDs and counts;
- REST and downstream finding IDs;
- Azure Guidelines decisions and findings;
- Documentation Completeness finding IDs;
- blockers;
- validation and HTML generation.

Historical replay reconstructs Agent outputs from retained validated baselines.
It measures deterministic preparation and finalization compatibility, not
active model inference time.

## Performance acceptance

- Agent index is at most 20 KiB for representative PR 43718.
- Normal Agent evidence reads are one index read and one bounded model-input
  read.
- Normal post-analysis shell commands are at most workspace construction,
  guarded finalization, and optional report serving.
- No manual user continuation is required.
- Active PR 43718 evidence review and judgment is at most 5m 30s.
- Finalization process wall time is at most 15 seconds.
- Findings, decisions, blockers, evidence, and report-quality results are
  equivalent to the reference workflow.

If a target is missed, retain the workspace and telemetry to identify whether
the remaining cost is inference reasoning, downstream evidence volume,
guideline comparison, deterministic preparation, or tool latency. Do not
remove evidence, combine distinct decisions, or weaken validation to meet a
time target.

## Validated PR 43718 result

The optimized PR 43718 run reduced the physical Agent-facing
`model-input.json` from approximately 1,400,460 bytes to 227,186 bytes, an
83.8% reduction. Its minified accounting size was 120,692 bytes. The bounded
workload contained six assessed Semantic intents, one informational publication
intent, ten downstream candidates, ten inference requests, and six Guidelines
requests.

Guarded finalization completed in 188 ms internally and approximately 1.50
seconds including Agent-output preparation and finalizer process wall time. The
validated report retained all seven Semantic intents, all 95 operations on the
informational publication intent, the same nine downstream findings, no
additional dimension findings, and no blockers.

## Out of scope

- Changing assessment classification rules.
- Reducing the number of assessment dimensions.
- Removing bounded inference requests.
- Replacing Agent judgment with deterministic defaults.
- Summarizing away canonical evidence.
- Treating ordinary operation or model changes as informational.
- Changing shared Azure Guidelines ranking or applicability semantics.
- Changing compiler or emitter versions.
- Reusing artifacts without comparison and content-hash validation.
