# Complete Read-Only Workflow

## Inputs

Resolve the baseline ref and specification/project root once. Default the repository to the current Git root. Use a work directory that is not assessed source.

For local assessment, use an explicitly supplied baseline ref or commit ID
without asking again. If none is supplied, ask the user to confirm `origin/main`
(recommended) or provide another ref/commit ID before preparation. Use the
host's user-question tool when available, and wait for confirmation rather than
silently accepting the script's default. This is a gate before preflight,
merge-base commands, work-directory creation, or analysis, not a notification
that accompanies starting the run. Approval to perform a read-only assessment
does not confirm its baseline. If the selected baseline cannot be
resolved, ask for a valid ref. If confirmation cannot be obtained, stop with a
clear blocker; never silently substitute a different baseline.
For PR assessment, resolve the PR's actual target baseline rather than applying
the local `origin/main` recommendation.

The coordinator captures committed, staged, unstaged, and relevant untracked TypeSpec changes; creates service-scoped sparse base/current worktrees; selects one API version per side; compiles each affected project independently with AutoRest and TCGC using that same version pair; runs the analyzers; collects source-only documentation evidence; calculates deterministic hunk coverage; and writes the bounded `model-input.json` once.

For the head, select the newest newly added API version when one exists; otherwise select its latest API version. When the PR adds no version and that head version exists in base, compile both sides with that same version. When head adds a version, select base's latest stable version, or its latest preview when no stable version exists. Record the pair and selection reasons in the manifest and report.

## Deterministic analysis

Set concrete values using the baseline resolved above, then run:

```powershell
$Repo = (git rev-parse --show-toplevel)
$Base = "<resolved-baseline-ref-or-commit>"
$Specification = "<project-or-spec-root>"
$Work = "<work-directory>"
$Skill = Join-Path $Repo ".github\skills\azure-typespec-assessment"

node (Join-Path $Skill "scripts\run-assessment-analysis.mjs") `
  --repo $Repo `
  --base $Base `
  --specification $Specification `
  --output $Work
```

Do not replace this with a full checkout or run the dimension analyzers against different inputs. If there is no changed TypeSpec in scope, stop with the coordinator's no-change result. If every active dimension is blocked, skip Agent judgment and preserve the blocked, `not-assessed` result. If only some dimensions are blocked, judge only the ready items and retain all blocker reasons.

## Optional inference, Azure Guidelines search, and final Agent judgment

Read only:

- `<work-directory>\model-input.json`;
- evidence artifacts explicitly listed in
  `model-input.json.artifactReferences`, resolving paths relative to the work
  directory and reading only entries named by `evidenceSetId` or
  `evidenceRef`;
- [classification guidance](classification.md), including [downstream cases](downstream-breaking-cases.md) and [candidate rules](downstream-candidate-rules.md);
- the [agentic search procedure](agentic-search.md);
- the [official document catalog](reference-document-links.md);
- the [documentation checks](document-quality.md);
- `scripts\inference.schema.json`;
- `scripts\compliance-search-evidence.schema.json`;
- `scripts\assessment-judgment.schema.json`.

If `inferenceRequests` is empty, do not create `inference.json`. Otherwise,
analyze only the supplied unknown hunks and write one exact result per request
to `<work-directory>\inference.json`. Each result is `candidates`,
`no-impact`, or `blocked`. Inferred candidates may use only the request's
source, hunk, operations, facts, and allowed dimensions. Never modify
`model-input.json`.

For every `complianceSearchRequests` entry, resolve its full request through
the referenced `dimensions/compliance-search-requests.json`, score the complete catalog, fetch
the four highest-ranked retrievable documents with `web_fetch`, and write
`<work-directory>\compliance-search-evidence.json`. Preserve failed retrievals
and use the next-ranked catalog entry as specified by the search procedure.
The main Agent writes this file directly; no Node.js script produces it.
`compliance-search-request.mjs` only creates the requests in `model-input.json`,
and `compliance-assessment.mjs` later consumes and validates the evidence.

For every `documentQualityReviewUnits` entry, resolve its canonical unit in
`dimensions/document-quality-input.json` through the declared evidence set.
For each document in a `ready` unit, judge Correctness and Meaning exactly
once. Use only its `@doc` and associated baseline/target declaration source;
no additional searches or generated descriptions. Retain blocked reasons and
do not assess absent/empty/deleted documentation. These checks run even when
the hunk has no REST/downstream impact and inference is unnecessary.

Then write `<work-directory>\assessment-judgment.json` with one concise result
per supplied Semantic review unit, exact deterministic and inferred
REST/downstream candidate coverage, and one Azure Guidelines decision per Semantic
intent, plus the required `documentQualityDecisions`. Do not read raw
AutoRest/TCGC output, compiler logs, unrelated unchanged source, prior answers, or use
catalog descriptions as guidance. Candidate and review-unit evidence omitted
from the bounded file remains available only through the declared canonical
artifact references; do not scan unrelated artifact entries.

## Assemble, validate, and render

```powershell
node (Join-Path $Skill "scripts\assemble-assessment.mjs") `
  --work $Work `
  --judgment (Join-Path $Work "assessment-judgment.json") `
  --output (Join-Path $Work "assessment.json")

node (Join-Path $Skill "scripts\validate-assessment.mjs") `
  (Join-Path $Work "assessment.json")

node (Join-Path $Skill "scripts\render-assessment-html.mjs") `
  (Join-Path $Work "assessment.json") `
  (Join-Path $Work "assessment.html")
```

Rendering accepts an optional, explicitly selected matching graph artifact:
append `--downstream-input (Join-Path $Work "dimensions\downstream-breaking-input.json")`
to the renderer command. The JavaScript API is
`renderAssessmentHtml(assessment, { downstreamInput })`, where `downstreamInput`
is the parsed JSON object. Existing one-argument callers remain supported.
The renderer checks root associations and exact recorded evidence facts before
using raw method-to-type paths, and rejects mismatched snapshots. It does not
discover adjacent inputs or modify assessment data. Without verified raw input,
recorded method associations remain visible with precise paths, locations, and
comparison roles explicitly unavailable; aggregate root locations are not
presented as per-method evidence.

For a verified replay whose reconstructed root IDs differ, additionally pass
`--downstream-assessment <matching-replay-assessment.json>` (API option
`downstreamAssessment`, the parsed object). This requires exact repository and
comparison equality and the same complete downstream findings, deeply equal
except for `rootCauseIds`. The renderer bridges only root associations, verifies
candidate membership and exact raw evidence facts, and keeps authoritative
judgments, method contracts, grouping, and semantic relationships unchanged.
Persist both explicitly selected sidecars with a rendered report if
reproducibility requires them; neither sidecar is discovered automatically.

If assembly rejects schema or coverage, send only its compact errors to the
same Agent for **one correction turn**. Correct
`inference.json`, `compliance-search-evidence.json`, and/or
`assessment-judgment.json`, then rerun the three commands above; do not rerun
preparation, compilation, analyzers, or create a second independent judgment.
If correction still fails, stop and report the blocker.
