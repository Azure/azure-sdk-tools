# Bounded Judgment Classification

The deterministic scripts identify review units, facts, and candidates. The Agent interprets them; it does not discover additional changes.

## Evidence boundaries

| Dimension  | Allowed evidence                                                      | Judgment                                                                                                                    |
| ---------- | --------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| Semantic   | Changed source plus before/after AutoRest facts in `model-input.json` | Explain author intent and caller-visible API behavior for every review unit. Use TypeSpec/API terms, not emitter internals. |
| REST       | AutoRest wire-contract candidate facts                                | Approve only when the current wire contract can invalidate an existing caller or response consumer; otherwise reject.       |
| Downstream | TCGC SDK-surface candidate facts and changed customization decorators | Approve only when language-neutral generated public/runtime SDK behavior can break existing SDK users; otherwise reject.    |

REST and downstream decisions are independent. A REST-compatible change can still break generated SDK users. Never use TCGC to classify REST compatibility or AutoRest to infer SDK surface.

## Decision rules

For every candidate, compare its `actual` current behavior with its `expected` compatibility behavior and explain the caller-visible consequence.

- `approve`: supplied facts establish the incompatibility. Include severity.
- `reject`: facts show additive/compatible behavior, no public/runtime impact, or insufficient causal evidence. Do not include a severity.
- Preserve exact candidate IDs. Never add a finding, source, operation, SDK symbol, or fact.

Use the candidate's default severity as a starting point, then apply:

| Severity | Meaning                                                                                                     |
| -------- | ----------------------------------------------------------------------------------------------------------- |
| `high`   | Existing requests, responses, or common generated-code use can fail or become invalid without migration.    |
| `medium` | A public behavior or SDK shape requires a meaningful consumer change but impact is narrower or conditional. |
| `low`    | Compatibility impact is real but constrained to uncommon paths, metadata, or specialized consumers.         |

## Semantic and confidence rules

Produce exactly one title and summary per semantic review unit. Include only operation/source IDs allowed by that unit. Describe version propagation explicitly when that is the unit's change kind.

Set `overallConfidence` to the lowest confidence warranted by ready dimensions and evidence quality:

- `high`: complete, direct before/after evidence;
- `medium`: interpretation is required but evidence is sufficient;
- `low`: material ambiguity remains.

List concrete blockers already supported by the input. Blocked dimensions remain `not-assessed`; do not turn missing evidence into a pass. Azure Compliance and Document Quality receive no judgment in the MVP and remain planned.
