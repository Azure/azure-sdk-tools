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

## Final data

Deterministic assembly joins Agent-confirmed decisions to complete facts and changed-source evidence, then writes `assessment.json`. The internal decision value `approve` means “retain this detected candidate as a finding”; it never means API review approval. Validation must reject duplicate, unknown, missing, unsupported, incomplete, or success-shaped results.

Every confirmed REST finding must contain actual and expected behavior, rationale, severity, affected operation, deterministic evidence, and exact changed TypeSpec source. Every confirmed downstream finding requires the same fields plus an SDK symbol or cross-language definition ID. User-facing output must say detected or confirmed, never approved. Semantic items require title, summary, affected operations, and changed source.

Downstream SDK method and SDK type cards must not repeat `Changed TypeSpec`
source links. Keep that evidence in `assessment.json`, Semantic intents, and
the appendix; retain only related Semantic intent links in the cards.
Method cards use direct, mixed, or indirect cause labels. Red impact links
are reserved for confirmed REST/downstream impacts; guideline links are separate.
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
- Document Quality and Agent Friendliness: `not-assessed` with
  `Document Quality and Agent Friendliness is not assessed.`;
- safety scope: `rest-and-downstream-only`, never Azure Guidelines or document quality.

A blocked implemented dimension cannot pass. Document Quality and Agent
Friendliness cannot pass or report zero findings as if assessed; it remains
explicitly `not-assessed`.
A completed Azure Guidelines search with no governing guidance is represented by an
intent-level `no-applicable-guidance` decision. It counts as assessed and does
not create a blocker. `not-assessed` is reserved for missing evidence,
retrieval failures, blocked Semantic analysis, or otherwise incomplete
the Azure Guidelines assessment.

## HTML

`assessment.html` must show comparison identity, overall code quality as
`passed|failed|not-assessed`, REST/downstream code-safety findings, semantic
intents, active Azure Guidelines status
and coverage, retained document evidence, fetched guidance and changed
TypeSpec, collapsed finding cards, retrieval blockers, explicit
not-assessed Document Quality and Agent Friendliness, and complete provenance.
After overall code quality, summary cards and main sections must order the five
dimensions as REST breaking changes, downstream breaking changes, Azure
Guidelines, Document Quality and Agent Friendliness, and Semantic intents. The
Azure Guidelines card uses its distinct visual guideline-issue count as the
primary numeric value; status remains represented by its icon and color.
The Azure Guidelines summary card counts distinct visual guideline issues;
the section metadata retains the underlying finding count.
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
member name or path. SDK rows derive concise before/after values and location
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

Semantic summaries expose static `Impacts (N)` links, counting only REST and
downstream targets. Guideline links are separate. Relationship labels and
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
