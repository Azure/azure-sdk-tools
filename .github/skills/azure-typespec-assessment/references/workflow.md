# Complete Read-Only Workflow

## Inputs

Use a work directory that is not assessed source. The deterministic coordinator
owns repository inspection, comparison resolution, changed-file and project
discovery, dependency preflight, and sparse workspace creation.

For every fresh assessment, invoking the coordinator is the first operational
command. Do not first run `git status`, `git diff`, `git fetch`, `git worktree`,
`gh pr`, `gh api`, recursive file searches, dependency checks, or manual
project discovery. If the coordinator returns a blocker, run only diagnostics
needed to resolve that blocker.

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
The baseline question is the only action allowed before the coordinator. Once
the user answers, invoke the coordinator next.

For PR assessment, pass the PR URL or number directly to the coordinator. It
resolves the PR's actual base/head commits, fetches only missing refs, derives
the TypeSpec scope, and performs the sparse checkout internally.

The coordinator captures committed, staged, unstaged, and relevant untracked TypeSpec changes; creates service-scoped sparse base/current worktrees; selects one API version per side; compiles each affected project independently with AutoRest and TCGC using that same version pair; runs the analyzers; records compiler-derived documentation presence; calculates deterministic hunk coverage; writes the bounded `model-input.json` once; and builds the deterministic `agent-workspace`.

For the head, select the newest newly added API version when one exists; otherwise select its latest API version. When the PR adds no version and that head version exists in base, compile both sides with that same version. When head adds a version, select base's latest stable version, or its latest preview when no stable version exists. Record the pair and selection reasons in the manifest and report.

## Deterministic analysis

For local code, set concrete values using the baseline resolved above, then run:

```powershell
$Repo = $PWD
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

For a PR, run directly without separate metadata or checkout commands:

```powershell
$Repo = $PWD
$Work = "<work-directory>"
$Skill = "<azure-typespec-assessment-skill-directory>"

node (Join-Path $Skill "scripts\run-assessment-analysis.mjs") `
  --repo $Repo `
  --pr "<pull-request-url-or-number>" `
  --output $Work
```

For an immutable comparison that is not identified by a PR, pass both commits:

```powershell
node (Join-Path $Skill "scripts\run-assessment-analysis.mjs") `
  --repo $Repo `
  --base "<base-ref-or-commit>" `
  --head "<head-ref-or-commit>" `
  --output $Work
```

`--specification` remains optional for PR and explicit-head modes; the
coordinator derives all changed TypeSpec service roots when it is omitted.

Do not replace this with a full checkout or run the dimension analyzers against different inputs. If there is no changed TypeSpec in scope, stop with the coordinator's no-change result. If every active dimension is blocked, skip Agent judgment and preserve the blocked, `not-assessed` result. If only some dimensions are blocked, judge only the ready items and retain all blocker reasons.

## Optional inference, Azure Guidelines search, and final Agent judgment

When the coordinator prints `awaiting-agent-judgment`, continue immediately.
Do not stop for a progress update or wait for another user turn.

Read `<work-directory>\agent-workspace\agent-index.json` once. It contains the
single bounded model-input path, required output files, drafts, exact coverage
IDs, and completion checklist. Then read `model-input.json` exactly once unless
guarded finalization requests the one permitted correction turn.
`api-version-publication` and `api-version-wide-change` intents are not part of
Agent coverage; guarded finalization restores them as deterministic
informational Semantic intents with canonical affected operations and no
finding relationships. Version-wide classification uses `Versions`
declaration and version-transition/governance evidence, not an operation-count
threshold.

Also read only:

- [classification guidance](classification.md), including [downstream cases](downstream-breaking-cases.md) and [candidate rules](downstream-candidate-rules.md);
- the [agentic search procedure](agentic-search.md);
- the [official document catalog](reference-document-links.md);
- the [documentation checks](document-quality.md);

Do not recursively list the work directory, search report artifacts broadly,
inspect raw compiler output, or repeatedly read schemas and canonical inputs.
Use the prefilled drafts as structural templates; their unresolved placeholders
are intentionally invalid and must never be copied to final output.

If `inferenceRequests` is empty, do not create `inference.json`. Otherwise,
analyze only the supplied unknown hunks and write one exact result per request
to `<work-directory>\inference.json`. Each result is `candidates`,
`no-impact`, or `blocked`. Inferred candidates may use only the request's
source, hunk, operations, facts, and allowed dimensions. Never modify
`model-input.json`.

Resolve all `complianceSearchRequests` through the referenced
`dimensions/compliance-search-requests.json`, combine their query profiles,
score the complete catalog once, fetch the four highest-ranked retrievable
documents once with `web_fetch`, and write
`<work-directory>\compliance-search-evidence.json`. Preserve failed retrievals
and use the next-ranked catalog entry as specified by the search procedure.
The main Agent writes this file directly; no Node.js script produces it.
`compliance-search-request.mjs` only creates the requests in `model-input.json`,
and `compliance-assessment.mjs` later consumes and validates the evidence.

Then write `<work-directory>\assessment-judgment.json` with one concise result
per supplied Semantic review unit, exact deterministic and inferred
REST/downstream candidate coverage, and one Azure Guidelines decision per Semantic
intent. Do not read raw
AutoRest/TCGC output, compiler logs, unrelated unchanged source, prior answers, or use
catalog descriptions as guidance. Candidate and review-unit evidence omitted
from the bounded file remains available only through the declared canonical
artifact references; do not scan unrelated artifact entries.

Documentation Completeness is assembled deterministically from
`dimensions/document-quality-input.json`. The Agent does not read that artifact
or author documentation decisions.

## Guarded finalization

```powershell
node (Join-Path $Skill "scripts\finalize-assessment.mjs") --work $Work
```

The finalizer verifies canonical artifact hashes, validates inference, shared
guideline evidence, and judgment coverage, assembles and validates the complete
assessment, and atomically writes `assessment.json` and `assessment.html`.
It returns failure unless both artifacts are valid. `workflow-state.json`
records preparation, Agent artifact, finalization, completion, blocker,
artifact hash, and phase timing data.

After rendering, start the report server with the host's attached background or
long-lived process mechanism:

```powershell
node (Join-Path $Skill "scripts\serve-assessment.mjs") `
  --file (Join-Path $Work "assessment.html")
```

Do not detach the process at the shell level. Wait for its startup output, then
give the user the printed `http://127.0.0.1:<port>/assessment.html` URL as the
clickable **Assessment report** link. Keep the process running while the report
is being viewed. Also provide the absolute `assessment.json` path for structured
results. Do not use a relative Markdown link or a `file:` URL for either
artifact; editor terminals may resolve those links against the wrong working
directory or fail to launch them.

Rendering accepts an optional, explicitly selected matching graph artifact:
append `--downstream-input (Join-Path $Work "dimensions\downstream-breaking-input.json")`
to the standalone renderer command. Guarded finalization supplies that matching
artifact automatically. The JavaScript API is
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
`assessment-judgment.json`, then rerun guarded finalization; do not rerun
preparation, compilation, analyzers, or create a second independent judgment.
If correction still fails, stop and report the blocker.
