---
name: azure-typespec-assessment
description: 'Assess the current Azure TypeSpec Git diff for semantic intent, REST and downstream SDK breaking changes, and documentation-grounded Azure compliance. WHEN: "assess TypeSpec changes", "review TypeSpec diff", "check TypeSpec breaking changes", "assess TypeSpec compliance", "explain TypeSpec REST impact".'
license: Apache-2.0
---

# Azure TypeSpec Assessment

Assess only the user-selected TypeSpec scope, normally before a PR exists. Never modify the user's assessed source, branch, index, or working tree; isolated temporary sparse worktrees and artifacts are allowed only under the chosen work directory.

Follow the [complete workflow](references/workflow.md). Apply the [classification rules](references/classification.md), including the detailed [downstream cases](references/downstream-breaking-cases.md), perform the [compliance search](references/agentic-search.md) against the [official document catalog](references/reference-document-links.md), and produce exactly the [required outputs](references/output-contract.md).

## Boundaries

- Run complete mode only: merge-base through `HEAD`, staged, unstaged, and relevant untracked changes.
- Derive semantic intents from changed TypeSpec source. Use AutoRest only to map those intents to REST operations and assess REST compatibility; use TCGC only for downstream SDK analysis.
- Check deterministic hunk coverage in `model-input.json`. Resolve only its declared `artifactReferences` and `evidenceSetId` entries when full evidence is needed. Skip inference when all hunks have candidates or explicit deterministic classifications. For `unknown` hunks only, write and validate `inference.json` before final judgment.
- In the final bounded Agent phase, summarize each Semantic intent once, judge every deterministic and inferred REST/downstream candidate, rank and fetch four official documents per intent, record search evidence, then assess Compliance once per intent. Do not assess each operation or invent URLs, operations, symbols, sources, or guidance.
- Treat a completed search with no governing guidance as `no-applicable-guidance`; count it as assessed and do not create a blocker. Reserve `not-assessed` for incomplete or blocked Compliance.
- Report Azure Compliance as `passed`, `failed`, or `not-assessed`. Report Document Quality and Agent Friendliness as a separate `not-assessed` dimension.
- Overall safety covers REST and downstream SDK impact only.
- Retain blockers as **Potential limits** in the report appendix and stop after assessment. Do not author fixes or remediate TypeSpec.
