# Output Contract

## Agent judgment

Write one `assessment-judgment.json` conforming to `scripts\assessment-judgment.schema.json`:

```json
{
  "schemaVersion": 1,
  "semanticIntents": [
    {
      "reviewUnitId": "semantic-...",
      "title": "...",
      "summary": "...",
      "sourceChangeIds": ["source-..."],
      "operationIds": ["operation-..."]
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
  "overallConfidence": "high",
  "blockers": []
}
```

Coverage must be exact: one semantic intent per supplied review unit and one decision per supplied REST/downstream candidate. Arrays may be empty only when the corresponding input array is empty. Approved decisions require `high`, `medium`, or `low` severity; rejected decisions omit severity. All IDs must come from `model-input.json`.

## Final data

Deterministic assembly joins approved decisions to complete facts and changed-source evidence, then writes `assessment.json`. Validation must reject duplicate, unknown, missing, unsupported, incomplete, or success-shaped results.

Every approved REST finding must contain actual and expected behavior, rationale, severity, affected operation, deterministic evidence, and exact changed TypeSpec source. Every approved downstream finding requires the same fields plus an SDK symbol or cross-language definition ID. Semantic items require title, summary, affected operations, and changed source.

Dimension statuses are derived, not authored:

- semantic: `assessed` or `not-assessed`;
- REST/downstream: `passed`, `failed`, or `not-assessed`;
- Azure Compliance: `planned` with `Deferred from the MVP.`;
- Document Quality: `planned` with `Planned by the design document.`;
- safety scope: `rest-and-downstream-only`, never compliance or document quality.

A blocked implemented dimension cannot pass. Deferred dimensions cannot pass or report zero findings as if assessed.

## HTML

`assessment.html` must show comparison identity, confidence, scoped safety, Preview Notice, REST findings, downstream findings, semantic intents, planned dimensions, blockers, and an appendix containing changed files/projects, compiler artifacts/status, timing, input accounting, and provenance. Escape all source- and Agent-controlled text.
