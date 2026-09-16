# Plan: Reduce Agent Evidence and Judgment Overhead

## Problem and measured baseline

The direct PR coordinator enhancement is complete. The new PR 43718 run in
session `1995a1f6-134d-4e95-a589-a588fcdb9d20` reduced setup excluding fetch
from 18m 51s to 29.9s and produced a complete validated report.

The new end-to-end timeline was:

| Phase                                              |        Time | Share |
| -------------------------------------------------- | ----------: | ----: |
| Direct setup, compilation, and deterministic work |      9m 05s | 43.8% |
| Bounded evidence review and Agent judgment        |     10m 56s | 52.7% |
| Assembly, validation, and HTML rendering          |         43s |  3.5% |
| Total report wall time                             | **20m 44s** |  100% |

The 10m 56s interval includes approximately two minutes waiting for the user to
request continuation after deterministic analysis. Active evidence review and
judgment therefore took approximately 8m 54s.

The bounded workload contained:

| Input or output                     | Result |
| ----------------------------------- | -----: |
| Semantic intents                    |      7 |
| REST candidates                     |      0 |
| Downstream candidates               |     10 |
| Inference requests                  |     11 |
| Facts                               |    204 |
| `model-input.json`                  | 1,400,420 bytes |
| Minified `facts` section            | 529,946 bytes |
| `inference.json`                    | 4,499 bytes |
| `assessment-judgment.json`          | 18,728 bytes |
| `compliance-search-evidence.json`   | 61,409 bytes |

The report phase used 15 PowerShell calls, 12 file views, 4 web fetches, 3
searches, and 3 patches. The large read-to-decision ratio and repeated tool
round trips are now a larger opportunity than compiler parallelism. On this
warm run, the four sequential compiler invocations totaled only 2m 53s;
pairwise AutoRest/TCGC parallelism has a theoretical saving of approximately
1m 22s.

## Goal

Reduce active evidence review, inference, and judgment from approximately
8m 54s to at most 5m 30s while preserving the complete assessment workflow and
report quality.

Also eliminate the manual continuation pause and reduce finalization to at most
15 seconds of process wall time.

The optimization must preserve:

- one concise result for every Semantic intent;
- exact deterministic and inferred REST/downstream candidate coverage;
- one Azure Guidelines decision for every Semantic intent;
- shared guideline analysis across all intents;
- compiler-derived Documentation Completeness;
- all blockers, provenance, source evidence, and stable IDs;
- validated `assessment.json` and complete `assessment.html`;
- the current read-only source-repository guarantees.

## Coordination with other performance work

This plan starts after:

- `perf-improvement-plan.md`, direct PR/local coordinator invocation;
- the separate shared Azure Guidelines analysis change;
- `perf-improvement-plan1.md`, pairwise AutoRest/TCGC compilation, if that work
  is enabled.

This plan must not duplicate or replace those implementations. Measure setup,
compiler, guideline, Agent, and finalization savings independently before
reporting combined end-to-end improvement.

## Design overview

Keep `model-input.json` as the canonical bounded machine input. Add a
deterministic Agent-facing projection that makes the required work explicit and
small enough for targeted reads:

```text
model-input.json + declared canonical evidence
                         |
                         v
             build-agent-workspace.mjs
                         |
          +--------------+---------------+
          |                              |
          v                              v
 agent-index.json                 intent packets
 status, counts, order,       exact evidence required for
 required output paths        one Semantic intent and its
 and coverage checklist       inference/candidate decisions
          |                              |
          +--------------+---------------+
                         |
                         v
              prefilled output drafts
     inference.json + assessment-judgment.json
                         |
                         v
                 bounded Agent work
                         |
                         v
              finalize-assessment.mjs
      validate inputs -> assemble -> validate -> render
```

The projection is an index over canonical evidence, not a replacement evidence
source. Assembly and validation continue to trust only the existing canonical
artifacts and validated Agent outputs.

## Required workflow behavior

When `run-assessment-analysis.mjs` returns `awaiting-agent-judgment`, the agent
must continue immediately in the same task. It must not stop for a status
summary or require the user to say "continue."

The assessment is complete only when one of these conditions is true:

1. `assessment.json` validates and `assessment.html` is rendered; or
2. deterministic analysis reports an all-dimensions-blocked terminal result
   and renders the required blocked report.

The existence of `model-input.json`, a partial dimension blocker, or empty REST
candidates is not a terminal condition.

## Implementation plan

1. **Add an Agent workspace builder**
   - Add `scripts/build-agent-workspace.mjs`.
   - Read only `model-input.json` and its declared canonical artifact
     references.
   - Write an `agent-workspace` directory under the assessment work directory.
   - Never modify `model-input.json` or canonical dimension artifacts.
   - Produce deterministic files with stable ordering and no inferred facts.

2. **Write a compact Agent index**
   - Write `agent-workspace/agent-index.json`.
   - Keep it below 20 KiB for representative assessments so one normal file
     read returns the complete index.
   - Include:
     - comparison identity and selected API versions;
     - dimension statuses and blockers;
     - Semantic intent, candidate, inference-request, and guideline-request
       counts;
     - exact required output files and schemas;
     - one ordered entry per Semantic intent;
     - exact candidate and inference IDs assigned to each intent;
     - coverage totals and a completion checklist;
     - paths to the corresponding intent packets.
   - Do not include large operation or SDK fact bodies in the index.

3. **Write one evidence packet per Semantic intent**
   - Write
     `agent-workspace/intents/<review-unit-id>.json`.
   - Include only canonical data required to:
     - summarize that Semantic intent;
     - resolve its bounded inference requests;
     - judge its REST and downstream candidates;
     - apply the already shared Azure Guidelines evidence.
   - Embed exact referenced facts so the Agent does not scan the global
     204-fact map.
   - Deduplicate facts within each packet.
   - Retain canonical IDs and evidence references for validation.
   - Never omit evidence merely to satisfy a byte target. If a packet is large,
     retain completeness and record its size in the index.

4. **Prefill structurally complete output drafts**
   - Generate `agent-workspace/inference.draft.json` only when inference
     requests exist.
   - Generate `agent-workspace/assessment-judgment.draft.json`.
   - Prefill every required ID exactly once in stable order.
   - Use explicit unresolved placeholders that fail schema validation until the
     Agent supplies the decision, rationale, title, summary, severity, or
     guidance references required by the existing contract.
   - Do not preselect decisions or generate success-shaped defaults.
   - The Agent writes final files at the existing canonical output paths.

5. **Make shared guideline evidence directly reusable**
   - Consume the separately implemented shared guideline analysis once.
   - Put only applicable shared document references and sections into each
     intent packet.
   - Do not refetch, rescore, or reanalyze a guideline document per intent.
   - Preserve one per-intent applicability decision and source/hunk coverage.

6. **Add one guarded finalization command**
   - Add `scripts/finalize-assessment.mjs --work <directory>`.
   - Validate `inference.json`, shared guideline evidence, and
     `assessment-judgment.json`.
   - Assemble `assessment.json`.
   - Validate the assembled assessment.
   - Render `assessment.html`.
   - Print the structured-result path and report path.
   - On failure, print compact actionable validation errors and leave existing
     evidence intact for the one permitted correction turn.
   - Do not return success unless both final artifacts exist and validation
     passes.

7. **Persist workflow state**
   - Write `workflow-state.json` atomically under the work directory.
   - Record states:
     - `preparing`;
     - `awaiting-agent-judgment`;
     - `agent-artifacts-written`;
     - `finalizing`;
     - `complete`;
     - `blocked`.
   - Record artifact paths, content hashes, phase start/end timestamps, and
     compact failure details.
   - Allow a later invocation to resume from valid completed deterministic
     artifacts rather than rerunning preparation and compilation.
   - Reject resume when comparison identity or canonical artifact hashes do not
     match.

8. **Update the skill workflow**
   - Require one read of `agent-index.json`, followed by each listed intent
     packet exactly once unless validation requests a correction.
   - Prohibit recursive artifact listing, broad report-file searches, and
     repeated schema inspection during normal execution.
   - Require immediate continuation after deterministic analysis.
   - Require `finalize-assessment.mjs` instead of separate assembly, validation,
     and rendering commands.
   - Preserve the existing one-correction-turn rule.

9. **Add Agent-phase telemetry**
   - Record:
     - deterministic-ready timestamp;
     - Agent-workspace construction duration;
     - user/host wait before judgment starts, when observable;
     - inference artifact timestamp;
     - guideline-evidence timestamp;
     - judgment artifact timestamp;
     - finalization process duration;
     - index and packet sizes;
     - number of Agent file reads and shell commands, when supplied by the
       evaluation harness.
   - Do not label elapsed wall time as active model execution time.

## Correctness and quality validation

1. **Projection completeness tests**
   - Every Semantic intent appears exactly once in the index and one packet.
   - Every deterministic and inferred candidate is assigned exactly once.
   - Every inference request appears exactly once.
   - Every referenced fact exists and is copied without mutation.
   - Every canonical evidence reference remains resolvable.
   - Blocked dimensions and partial blockers remain visible.

2. **Draft safety tests**
   - Unresolved drafts fail validation.
   - Missing, duplicate, unknown, or unsupported IDs fail validation.
   - Draft generation never selects `approve`, `reject`,
     `no-applicable-guidance`, or another decision.
   - Empty candidate dimensions remain explicitly covered without fabricated
     findings.

3. **Finalization tests**
   - A valid existing judgment produces the same assessment data and HTML as
     the existing three-command sequence, excluding timing-only fields.
   - Any invalid Agent artifact prevents both success and a success-shaped
     workflow state.
   - A partial documentation blocker still produces a report with the
     appropriate `not-assessed` coverage and potential limit.
   - Finalization is idempotent for unchanged inputs.

4. **Replay equivalence**
   - Build the Agent workspace from each retained assessment fixture.
   - Reuse its existing inference, guideline evidence, and judgment artifacts.
   - Require equivalent findings, dimension statuses, blockers, source links,
     provenance, stable anchors, and report sections.
   - Run the existing report-quality graders and reject any regression.

5. **Skill behavior evals**
   - Verify the agent continues after `awaiting-agent-judgment` without another
     user prompt.
   - Verify it reads the index and listed packets rather than scanning the work
     directory.
   - Verify it invokes the guarded finalizer and does not claim completion
     before validated HTML exists.
   - Verify shared guideline evidence is analyzed once across all intents.

## Performance validation

Benchmark the current PR 43718 artifacts and at least three retained fixtures:
one small case, one documentation-heavy case, and one downstream-heavy case.

Acceptance targets:

- Agent index is at most 20 KiB for the representative PR 43718 case.
- Normal Agent evidence reads are at most one index read plus one read per
  Semantic intent packet.
- Normal post-analysis shell commands are at most three:
  workspace construction, guarded finalization, and report serving.
- No manual user continuation is required.
- Active PR 43718 evidence review and judgment is at most 5m 30s.
- Finalization process wall time is at most 15 seconds.
- End-to-end PR 43718 wall time is at most 15 minutes after the current setup
  and compiler improvements are enabled.
- Findings, decisions, blockers, evidence, and report-quality grader results
  are equivalent to the reference workflow.

If the time target is missed, retain the workspace and telemetry to identify
whether the remaining cost is inference reasoning, downstream evidence volume,
guideline comparison, or tool latency. Do not remove evidence, combine distinct
decisions, or weaken validation to meet the target.

## Expected improvement

For the measured PR 43718 run:

- removing the manual continuation pause saves approximately two minutes;
- compact indexed evidence and prefilled drafts target a further three to four
  minutes;
- guarded finalization targets approximately 28 seconds of improvement over
  the measured 43-second finalization interval;
- compiler parallelism remains a separate approximately one-minute opportunity
  on this warm run.

The expected quality-preserving result is approximately 14-16 minutes
end-to-end, with a stretch target below 15 minutes after the compiler plan is
also enabled.

## Out of scope

- Changing assessment classification rules.
- Reducing the number of assessment dimensions.
- Removing bounded inference requests.
- Replacing Agent judgment with deterministic defaults.
- Summarizing away canonical evidence.
- Changing shared Azure Guidelines ranking or applicability semantics.
- Changing compiler or emitter versions.
- Parallelizing additional compiler roles or projects.
- Reusing artifacts without comparison and content-hash validation.
