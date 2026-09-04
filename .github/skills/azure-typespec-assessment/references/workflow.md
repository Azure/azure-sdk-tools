# Complete Read-Only Workflow

## Inputs

Obtain the baseline ref and specification/project root once. Default the repository to the current Git root and the baseline to `origin/main` only when appropriate. Use a work directory that is not assessed source.

The coordinator captures committed, staged, unstaged, and relevant untracked TypeSpec changes; creates service-scoped sparse base/current worktrees; selects one API version per side; compiles each affected project independently with AutoRest and TCGC using that same version pair; runs all three analyzers; calculates deterministic hunk coverage; and writes the bounded `model-input.json` once.

For the head, select the newest newly added API version when one exists; otherwise select its latest API version. When the PR adds no version and that head version exists in base, compile both sides with that same version. When head adds a version, select base's latest stable version, or its latest preview when no stable version exists. Record the pair and selection reasons in the manifest and report.

## Deterministic analysis

Set concrete values, then run:

```powershell
$Repo = (git rev-parse --show-toplevel)
$Base = "origin/main"
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

## Optional inference, Compliance search, and final Agent judgment

Read only:

- `<work-directory>\model-input.json`;
- evidence artifacts explicitly listed in
  `model-input.json.artifactReferences`, resolving paths relative to the work
  directory and reading only entries named by `evidenceSetId` or
  `evidenceRef`;
- [classification guidance](classification.md), including [downstream cases](downstream-breaking-cases.md);
- the [agentic search procedure](agentic-search.md);
- the [official document catalog](reference-document-links.md);
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

Then write `<work-directory>\assessment-judgment.json` with one concise result
per supplied Semantic review unit, exact deterministic and inferred
REST/downstream candidate coverage, and one Compliance decision per Semantic
intent. Do not read raw
AutoRest/TCGC output, compiler logs, unchanged source, prior answers, or use
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

If assembly rejects schema or coverage, send only its compact errors to the
same Agent for **one correction turn**. Correct
`inference.json`, `compliance-search-evidence.json`, and/or
`assessment-judgment.json`, then rerun the three commands above; do not rerun
preparation, compilation, analyzers, or create a second independent judgment.
If correction still fails, stop and report the blocker.
