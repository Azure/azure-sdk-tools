# Output Contract

## Azure Guidelines search evidence

Write `compliance-search-evidence.json` conforming to
`scripts\compliance-search-evidence.schema.json`. There must be one entry per
`complianceSearchRequests` item. Resolve the complete request from the
referenced Azure Guidelines request artifact. Each entry preserves its unchanged
query profile, the complete scored catalog ranking, four fetched catalog documents
or an explicit catalog-exhaustion blocker, score components, retrieval
provenance, declaration applicability, relevant guidance, and failed
replacement attempts. Catalog descriptions select documents but never serve
as guidance.

## Optional inference

`model-input.json` contains `deterministicCoverage` for every Semantic review
unit and one `inferenceRequests` item per `unknown` hunk. Do not create
`inference.json` when the request array is empty.

When requests exist, write `inference.json` conforming to
`scripts\inference.schema.json`. Cover every request exactly once with
`candidates`, `no-impact`, or `blocked`. Inferred candidates must remain within
the request's IDs and allowed REST/downstream dimensions. They require final
Agent judgment like deterministic candidates.

## Agent judgment

Write one `assessment-judgment.json` conforming to `scripts\assessment-judgment.schema.json`:

```json
{
  "schemaVersion": 1,
  "semanticIntents": [
    {
      "reviewUnitId": "semantic-...",
      "title": "...",
      "summary": "..."
    }
  ],
  "restDecisions": [
    {
      "candidateId": "rest-...",
      "decision": "approve",
      "severity": "high",
      "rationale": "..."
    }
  ],
  "downstreamDecisions": [
    {
      "candidateId": "downstream-...",
      "decision": "reject",
      "rationale": "..."
    }
  ],
  "complianceDecisions": [
    {
      "reviewUnitId": "semantic-...",
      "applicableGuidance": [
        {
          "canonicalDocumentUrl": "https://...",
          "guidanceSection": "..."
        }
      ],
      "sourceChangeIds": ["source-..."],
      "hunkIds": ["hunk-..."],
      "declarationIds": ["declaration-..."],
      "decision": "applicable-fail",
      "title": "Widget does not use the documented resource template",
      "severity": "medium",
      "expected": "Exact fetched excerpt.",
      "actual": "Changed TypeSpec behavior.",
      "rationale": "..."
    }
  ],
  "overallConfidence": "high",
  "documentQualityDecisions": [
    {
      "reviewUnitId": "semantic-...",
      "documentId": "document-...",
      "check": "description",
      "decision": "fail",
      "title": "Response description incorrectly says the body is empty",
      "expected": "Describe the optional status carried by the response body.",
      "docQuote": "Empty success response.",
      "rationale": "The associated response-body model declares a status property."
    }
  ],
  "blockers": []
}
```

Coverage must be exact: one concise semantic result per supplied review unit,
one decision per supplied deterministic or inferred REST/downstream candidate,
and one Azure Guidelines decision per Semantic intent. Applicable Azure Guidelines
decisions cite fetched guidance sections and synthesize their expected pattern.
Use `no-applicable-guidance` when search completed but no fetched section
governs the intent; use `not-assessed` only for incomplete or blocked
the Azure Guidelines assessment.
All IDs and URLs must come from the bounded inputs or validated inference
output. Every `applicable-fail` decision must also provide a concise finding
title and `high`, `medium`, or `low` severity for structured assessment data.

For `documentQualityReviewUnits`, resolve the canonical artifact through its
dedicated evidence set. For input `schemaVersion: 3` (or historical v2), cover each document in
every `ready` unit exactly once with `check: "description"`, following the
[documentation rules](document-quality.md). `fail` requires `title`,
`expected`, `rationale`, and a nonempty exact target `docQuote`; `pass` and
`not-assessed` require rationale. Do not include severity. Units that are
`not-applicable` or `blocked` require no Agent document decisions. Missing
decisions for new inputs are errors; legacy inputs without the documentation
field may omit this decision array.
V3 reviews local descriptions only. Inherited-only descriptions count as
documented but are excluded from `documents` and Agent decisions. Unit-level
`inheritedDocumentIds` and coverage `inheritedDocumentCount` preserve this distinction.
Model summaries include `inheritedDocumentCount` when nonzero, not inherited text.
An inherited baseline snapshot may accompany a new local override; it adds
`documentationOrigin: "inherited"` and preserves compiler-resolved text.
Other origin values or origin markers in v1/v2 are invalid.
For v2/v3 input, model metadata `documentQualityAssessmentVersion` equals the
input version and `documentQualityCriterion` retains the same description question.
The enclosing report's top-level `schemaVersion` remains 1.

## Final data

Deterministic assembly joins Agent-confirmed decisions to complete facts and changed-source evidence, then writes `assessment.json`. The internal decision value `approve` means “retain this detected candidate as a finding”; it never means API review approval. Validation must reject duplicate, unknown, missing, unsupported, incomplete, or success-shaped results.

Every confirmed REST finding must contain actual and expected behavior, rationale, severity, affected operation, deterministic evidence, and exact changed TypeSpec source. Every confirmed downstream finding requires the same fields plus an SDK symbol or cross-language definition ID. User-facing output must say detected or confirmed, never approved. Semantic items require title, summary, affected operations, and changed source.

Downstream SDK method and SDK type cards must not repeat `Changed TypeSpec`
source links. Keep that evidence in `assessment.json`, Semantic intents, and
the appendix; retain only related Semantic intent links in the cards.
Method cards use direct, mixed, or indirect cause labels. Red impact links
are reserved for confirmed REST/downstream impacts and failed documentation
findings; guideline links are separate.
Downstream data and cards must not contain affected REST operations, HTTP
routes, REST-derived counts, or REST/downstream suppression records. Direct
method findings are assembled into `methodGroups`; type findings are assembled
into `typeImpacts` with deterministic TCGC `affectedMethods`.

HTML finding cards must not display `high`, `medium`, or `low` severity labels
or severity-colored borders. Severity remains available in `assessment.json`
for validation and machine consumers.

Dimension statuses are derived, not authored:

- semantic: `assessed` or `not-assessed`;
- REST/downstream: `passed`, `failed`, or `not-assessed`;
- Azure Guidelines: `passed`, `failed`, or `not-assessed`, derived from
  Semantic intent coverage and applicable fetched guidance;
- Doc Correctness (`documentQuality`): `passed`, `failed`, `not-assessed`,
  or `not-applicable`, with `assessmentVersion: 3` for v3 input and separate semantic-unit,
  document, and check coverage (one check per eligible description);
- safety scope: `rest-and-downstream-only`, never Azure Guidelines or document quality.

A blocked implemented dimension cannot pass. Documentation is `failed` when
there are confirmed failures, otherwise `not-assessed` when coverage is
incomplete, otherwise `not-applicable` if no descriptions are eligible,
otherwise `passed`. Retain partial coverage even when failures are
confirmed. A documentation unit with no eligible target `@doc` is explicitly
`not-applicable`; it counts as assessed scope but not as an assessed document.
Legacy documentation dimensions without input remain `not-assessed`.
Historical v1 results keep their separate correctness/meaning checks and absent
assessment version; v2 results retain `assessmentVersion: 2` and their original
coverage. V2/v3 inputs require matching source evidence versions. Do not relabel
legacy inputs or silently add inherited coverage; recollect evidence for v3.
Recorded inherited-only coverage is unreviewed, not passed, failed, blocked,
or missing; it is not rendered as a separate HTML group. Baseline inherited text, when relevant
to a local-description finding, remains separate from exact declaration source.
A completed Azure Guidelines search with no governing guidance is represented by an
intent-level `no-applicable-guidance` decision. It counts as assessed and does
not create a blocker. `not-assessed` is reserved for missing evidence,
retrieval failures, blocked Semantic analysis, or otherwise incomplete
the Azure Guidelines assessment.

## HTML

`assessment.html` must show comparison identity, overall finding count with a
finding-based status icon, REST/downstream code-safety findings, semantic
intents, active Azure Guidelines status
and coverage, retained document evidence, fetched guidance and changed
TypeSpec, collapsed finding cards, retrieval blockers, explicit
Doc Correctness status and coverage, and complete provenance.
Overall code quality is a non-clickable summary card. The five dimension cards
follow in this order: Semantic intents, Azure Guidelines, REST breaking changes,
downstream breaking changes, and Doc Correctness. Main sections with findings
precede those without findings; within each group, use the dimension-card order.
Semantic intents are information only, always in the no-findings group. Show an
information icon beside its title, with intent, operation, and action counts below;
do not display Pass, Fail, or N/A status tags for Semantic intents. Preserve the
recorded review state in JSON. The appendix remains last.
Each card's heading contains only its icon and title on the same line, not a
number or Pass/Fail/N/A text. Quality cards show the recorded finding count below
the heading. Overall sums REST, downstream, Azure Guidelines, and Doc Correctness
findings, excluding intents. Preserve status icons, accessible labels, and colors.
Count underlying findings, not grouped operations, SDK methods, or guideline issue
cards. Exclude legacy downstream entries that only repeat approved REST findings.
HTML may present multiple findings in one guideline-issue
card only when their canonical guidance document-section sets and normalized
expected behavior are identical. Grouping is presentation-only: JSON findings
and stable finding anchors remain unchanged, shared expected behavior and
guidance are rendered once, and each affected Semantic intent retains its own
actual behavior, changed-code evidence, and human-readable intent link. The
card's affected-intent detail and JSON retain the underlying intent-level
finding cardinality. Matching titles alone must not cause grouping.
Immediately below the header, render a compact `Preview Notice` details element
that is collapsed by default. Its one-line summary should occupy approximately
46 pixels vertically. The expanded body must preserve the complete approved
two-paragraph disclaimer and use two columns on wide screens and one column on
narrow screens.
Present top-level assessment blockers only in the appendix under
**Potential limits**, not as a standalone main-report section. The appendix
must include a clickable pull request link when a PR number is available,
deriving the URL from `repository.remoteUrl` when no dedicated pull-request URL
is present. Escape all source- and Agent-controlled text.

All five dimensions share a heading, description, and right-aligned metadata.
Finding and intent cards share typography, right-aligned status/cause labels,
and collapsed-by-default summaries.

REST breaking findings are operation-first: operation identity, HTTP method,
path, version, and human-readable affected-intent links in the summary; a styled
`Contract area | Before | After` table; a highlighted
`Why this is breaking` callout. Preserve findings with unavailable operation
mapping in explicit fallback cards. Do not render
severity labels, severity-colored borders, or Changed TypeSpec links in these
cards.
Semantic operation cards use the same `Contract area | Before | After` table
and removal/addition styling. They reuse confirmed REST finding rows associated
with both the operation ID and current Semantic intent. If no fine-grained
confirmed row is available, they structurally compare normalized before/after
operation facts and render the narrowest changed parameter, request schema,
response status/body/header, paging, LRO, method, or path areas. Do not render
identical top-level summaries when a deeper changed path is available. If the
normalized comparison produces no changed contract row, omit the table and
render the unchanged outcome without a redundant duplicate statement.

All REST, Semantic operation, SDK method, and SDK type contract tables use the
same two-line contract-area cell: a human-readable area kind above the concrete
member name or path. Omit rows whose displayed before and after values are
identical; omit the table if no rows remain. Keep underlying findings and
evidence unchanged. SDK rows derive concise before/after values and location
from retained TCGC facts and method-to-type reference paths. Allowed SDK
locations are `(path)`, `(query)`, `(header)`, and `(body input)` beside
numbered caller inputs, with a `Return type (body output)` row. Constant headers
appear in a note, not the numbered caller inputs. Missing facts and locations
must be labeled unavailable, never inferred from a root-wide location union.
Do not render a
`model-property-removed` row when downstream judgment concludes that the member
was compatibly preserved by an explicit response model. Prefer concrete members
and concise recorded contract values; retain expected/actual prose when no
structured value is available.

Downstream cards are method-first, merging direct method changes with indirect
type causes, using target normalized method names. Show representative graph
paths with baseline/target roles only when verified raw evidence is explicitly
supplied. Keep unmapped confirmed types visible. Do not add enum-specific or
shared-cause banners; enum transitions remain in per-method evidence.

Semantic summaries expose static `Impacts (N)` links, counting REST, downstream,
and failed documentation targets. Failed documentation links use the existing
red impact style and retain stable finding destinations. Passing, incomplete,
and appendix navigation links are not failure impacts. Guideline links are
separate. Relationship labels and
backgrounds do not toggle the card; anchors reveal their target's enclosing
details. On expansion, complete Changed TypeSpec source appears first and is
expanded. Affected operations follow in a collapsed group: at most ten operation
cards, followed by compact descriptions retaining every remaining operation ID,
HTTP method, version, and path.

Azure Guidelines summaries retain the gap and affected-intent links. The body
contains two full-width sections: Expected (distinct expected text and official
references/examples) and Actual (each finding's recorded diff once, source
links, and non-duplicate actual explanation). Grouped findings retain per-intent
anchors. Do not duplicate these sections with a comparison table or a second
source-evidence block. Preserve pass/fail/not-assessed and no-applicable-guidance
states without severity labels.

Use **Doc Correctness** for this dimension's heading, summary/navigation labels,
and failed impact prefix (`Doc Correctness: ...`).
The subtitle asks whether the description accurately explains its associated
TypeSpec code and retains exclusions for examples, external documentation, and
agent execution. This is description quality, not runtime agent evaluation.
Keep `documentQuality`, schema/evidence fields, the historical rubric, and
main-section and finding anchors unchanged.
Retain original recorded source, description, and judgment evidence verbatim.

Keep the summary card and main-section coverage to the finding count and assessed
description count, for example **0 findings** and **9 descriptions assessed**.
Do not include unassessed counts or partial-review wording in the overview.
Display Doc Correctness as **Pass** when no findings are recorded and **Fail**
otherwise; Overall code quality similarly passes only when no main dimension has
findings. These display statuses do not imply complete coverage or alter recorded
assessment statuses. Label unavailable legacy assessment counts explicitly instead
of inventing zero. Full coverage, original statuses, inherited-description counts,
exclusions, and documentation blockers remain in `assessment.json`, not the HTML.

The main documentation section contains only failed finding cards grouped by
intent and compact coverage. Do not render Doc Correctness details in the appendix
or links to the removed documentation appendix.
Failed intent groups open initially; individual findings remain collapsed.
Their summaries prominently show short object identities with qualified-name
hover text, the recorded issue and check label, and no severity. Do not repeat
the owning intent's link inside its finding cards; retain links to other affected
intents and stable finding anchors.

On expansion, show **Current description** first: the exact compiler-resolved
target string, with only a nonempty exact `docQuote` match highlighted.
Preserve whitespace and escape every string, including highlighted text.
Follow with recorded `rationale` under **Why this needs attention** and recorded
`expected` under **Suggested change**. The latter is actionable guidance, not
a literal proposed description. Do not fabricate replacement prose, code
summaries, or new judgment fields. Missing current text is explicitly unavailable,
never substituted with a baseline string or legacy `actual` prose.

Collapse supporting TypeSpec and source evidence. Retain baseline/current
snapshots, exact declarations, full source paths, and inherited-origin labels;
a single snapshot uses full width. Include related type definitions only through
unambiguous compiler-recorded references from the same intent and revision,
with exact source ownership. Do not infer references from prose or duplicate
declarations already contained in the selected declaration.

Omit passed, incomplete, and neutral documentation groups, detailed coverage,
recorded summaries, and non-finding description browsers from the entire HTML
report. Complete judgments and retained evidence remain in `assessment.json`.
Zero pending decisions does not imply complete scope; no eligible descriptions
retains its neutral recorded audit state even though the finding-based overview
passes. Label historical two-check results legacy. Human-facing labels say **description**,
not `@doc`; the normative criterion, historical artifacts, and literal evidence
remain unchanged.

`renderReportSections` returns main `html` and an empty `appendixHtml` for
compatibility. The general report appendix remains unchanged. Hash navigation opens
all enclosing details for appendix, finding, and intent links. Failed documentation
links contribute to semantic `Impacts (N)` but never to scoped REST/downstream safety.
Recorded documentation findings participate in overall code quality; coverage
limitations remain recorded in the assessment data.
