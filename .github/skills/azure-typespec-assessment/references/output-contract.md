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

HTML finding cards must not display `high`, `medium`, or `low` severity labels
or severity-colored borders. Severity remains available in `assessment.json`
for validation and machine consumers.

Dimension statuses are derived, not authored:

- semantic: `assessed` or `not-assessed`;
- REST/downstream: `passed`, `failed`, or `not-assessed`;
- Azure Compliance: `passed`, `failed`, or `not-assessed`, derived from
  Semantic intent coverage and applicable fetched guidance;
- Document Quality: `not-assessed` with `Document quality is not assessed.`;
- safety scope: `rest-and-downstream-only`, never compliance or document quality.

A blocked implemented dimension cannot pass. Document Quality cannot pass or
report zero findings as if assessed; it remains explicitly `not-assessed`.
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
not-assessed Document Quality, and complete provenance. Present top-level
assessment blockers only in the appendix under **Potential limits**, not as a
standalone main-report section. Escape all source- and Agent-controlled text.
