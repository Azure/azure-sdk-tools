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
- In the same bounded Agent phase, summarize each Semantic intent once, rank and fetch four official documents per intent, record search evidence, then assess Compliance once per intent. Do not assess each operation or invent candidates, IDs, URLs, operations, symbols, sources, or guidance.
- Treat insufficient Compliance guidance as `not-assessed`; retain its rationale and generate the report instead of blocking assembly.
- Report Azure Compliance as `passed`, `failed`, or `not-assessed`. Report Document Quality as a separate `not-assessed` dimension.
- Overall safety covers REST and downstream SDK impact only.
- Report blockers and stop after assessment. Do not author fixes or remediate TypeSpec.
