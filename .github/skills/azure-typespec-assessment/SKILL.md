---
name: azure-typespec-assessment
description: 'Assess the current Azure TypeSpec Git diff in complete, read-only mode for semantic intent, REST breaking changes, and language-neutral downstream SDK breaks; compliance and document quality remain planned. WHEN: "assess TypeSpec changes", "review TypeSpec diff", "check TypeSpec breaking changes", "explain TypeSpec REST impact".'
license: Apache-2.0
---

# Azure TypeSpec Assessment

Assess only the user-selected TypeSpec scope. Never modify the user's assessed source, branch, index, or working tree; isolated temporary sparse worktrees and artifacts are allowed only under the chosen work directory.

Follow the [complete workflow](references/workflow.md). Apply the [classification rules](references/classification.md), including the detailed [downstream cases](references/downstream-breaking-cases.md), and produce exactly the [required outputs](references/output-contract.md).

## Boundaries

- Run complete mode only: merge-base through `HEAD`, staged, unstaged, and relevant untracked changes.
- Derive semantic intents from changed TypeSpec source. Use AutoRest only to map those intents to REST operations and assess REST compatibility; use TCGC only for downstream SDK analysis.
- Perform one bounded Agent judgment after deterministic analysis. Do not invent candidates, IDs, operations, symbols, sources, or evidence.
- Report Azure Compliance as `Planned / Not assessed` and Document Quality as `Planned`; never imply they passed.
- Overall safety covers REST and downstream SDK impact only.
- Report blockers and stop after assessment. Do not author fixes or remediate TypeSpec.
