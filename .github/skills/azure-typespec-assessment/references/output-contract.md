# Output Contract

## Compliance search evidence

Write `compliance-search-evidence.json` conforming to
`scripts\compliance-search-evidence.schema.json`. There must be one entry per
`complianceSearchRequests` item. Each entry preserves the unchanged query
profile, the complete scored catalog ranking, four fetched catalog documents
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
and one Compliance decision per Semantic intent. Applicable Compliance
decisions cite fetched guidance sections and synthesize their expected pattern.
Use `no-applicable-guidance` when search completed but no fetched section
governs the intent; use `not-assessed` only for incomplete or blocked
Compliance.
All IDs and URLs must come from the bounded inputs or validated inference
output. Every `applicable-fail` decision must also provide a concise finding
title and `high`, `medium`, or `low` severity for structured assessment data.

## Final data

Deterministic assembly joins Agent-confirmed decisions to complete facts and changed-source evidence, then writes `assessment.json`. The internal decision value `approve` means “retain this detected candidate as a finding”; it never means API review approval. Validation must reject duplicate, unknown, missing, unsupported, incomplete, or success-shaped results.

Every confirmed REST finding must contain actual and expected behavior, rationale, severity, affected operation, deterministic evidence, and exact changed TypeSpec source. Every confirmed downstream finding requires the same fields plus an SDK symbol or cross-language definition ID. User-facing output must say detected or confirmed, never approved. Semantic items require title, summary, affected operations, and changed source.

Downstream SDK method and SDK type cards must not repeat `Changed TypeSpec`
source links. Keep that evidence in `assessment.json`, Semantic intents, and
the appendix; retain only related Semantic intent links in the cards.
Their `SDK method` and `SDK type` tags must use the blue informational tag
style; red is reserved for REST-breaking tags and failure indicators.

HTML finding cards must not display `high`, `medium`, or `low` severity labels
or severity-colored borders. Severity remains available in `assessment.json`
for validation and machine consumers.

Dimension statuses are derived, not authored:

- semantic: `assessed` or `not-assessed`;
- REST/downstream: `passed`, `failed`, or `not-assessed`;
- Azure Compliance: `passed`, `failed`, or `not-assessed`, derived from
  Semantic intent coverage and applicable fetched guidance;
- Document Quality and Agent Friendliness: `not-assessed` with
  `Document Quality and Agent Friendliness is not assessed.`;
- safety scope: `rest-and-downstream-only`, never compliance or document quality.

A blocked implemented dimension cannot pass. Document Quality and Agent
Friendliness cannot pass or report zero findings as if assessed; it remains
explicitly `not-assessed`.
A completed Compliance search with no governing guidance is represented by an
intent-level `no-applicable-guidance` decision. It counts as assessed and does
not create a blocker. `not-assessed` is reserved for missing evidence,
retrieval failures, blocked Semantic analysis, or otherwise incomplete
Compliance.

## HTML

`assessment.html` must show comparison identity, overall code quality as
`passed|failed|not-assessed`, REST/downstream code-safety findings, semantic
intents, active Compliance status
and coverage, four ranked documents per intent, fetched guidance beside changed
TypeSpec, expanded failures, collapsed passes, retrieval blockers, explicit
not-assessed Document Quality and Agent Friendliness, and complete provenance.
After overall code quality, summary cards and main sections must order the five
dimensions as REST breaking changes, downstream breaking changes, Azure
Guidelines, Document Quality and Agent Friendliness, and Semantic intents. The
Azure Guidelines card uses its distinct visual guideline-issue count as the
primary numeric value; status remains represented by its icon and color.
The numbered Azure Guidelines section and summary card count distinct visual
guideline issues. HTML may present multiple findings in one guideline-issue
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

REST breaking findings must use the shared contract-card hierarchy: contract
identity and `REST contract` tag in the summary; a styled
`Contract area | Before | After` table; a highlighted
`Why this is breaking` callout; a nested affected-operation list collapsed by
default; and human-readable Semantic intent links in the footer. Do not render
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
render `HTTP signature and represented payload contract unchanged.` as both
the operation statement and change outcome.

All REST, Semantic operation, SDK method, and SDK type contract tables use the
same two-line contract-area cell: a human-readable area kind above the concrete
member name or path. SDK rows derive concise before/after values from retained
TCGC facts and use related operation evidence for wire origin only when
unambiguous. Do not render internal rule identifiers such as
`model-property-removed` as members or use full finding sentences as contract
values.
