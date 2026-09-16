---
name: azure-typespec-assessment
description: 'Assess TypeSpec Git diffs for semantic intent, REST and SDK breaking changes, Azure Guidelines, and documentation completeness. WHEN: "assess TypeSpec changes", "review TypeSpec diff", "check TypeSpec breaking changes", "assess TypeSpec against Azure Guidelines", "explain TypeSpec REST impact", "review TypeSpec documentation". DO NOT USE FOR: modifying TypeSpec or as a subworkflow of azure-typespec-author.'
license: Apache-2.0
---

# Azure TypeSpec Assessment

Assess only the user-selected TypeSpec scope, normally before a PR exists. Never modify the user's assessed source, branch, index, or working tree; isolated temporary sparse worktrees and artifacts are allowed only under the chosen work directory.

## Before starting a local assessment

Check whether the user explicitly supplied or confirmed the baseline for this
assessment. If not, your first assessment action must be to ask:
**"Compare against origin/main, or a different branch or commit?"**
Use the host's user-question tool when available and wait for the answer.
Do not start preflight, compute a merge base, create work artifacts, or run
analysis before confirmation. Permission to assess the code is not permission
to choose its baseline; merely announcing `origin/main` is not confirmation.
Use a supplied baseline without asking again. Existing-report replay does not
need this question; for PR assessment, use the PR's actual target baseline.

For every fresh assessment, the coordinator is the first operational command.
For a PR, run `run-assessment-analysis.mjs --pr <url-or-number> --repo <repo>
--output <work>` immediately. For local code, run it immediately with
`--base`, `--specification`, `--repo`, and `--output` once the baseline is
known. Do not run separate Git status/diff/fetch/worktree, PR metadata,
dependency, or project-discovery commands first. Diagnose only a concrete
blocker returned by the coordinator.

Follow the [complete workflow](references/workflow.md). Apply the [classification rules](references/classification.md), including the detailed [downstream cases](references/downstream-breaking-cases.md), perform the [Azure Guidelines search](references/agentic-search.md) against the [official document catalog](references/reference-document-links.md), check [documentation completeness](references/document-quality.md), and produce exactly the [required outputs](references/output-contract.md).

## Boundaries

- V1 is standalone and opt-in: run only when the user explicitly requests an assessment or review. Do not invoke this skill from `azure-typespec-author`, or automatically before or after its authoring and validation workflow. Integration is deferred to a future version.
- Run complete mode only: merge-base through `HEAD`, staged, unstaged, and relevant untracked changes.
- Derive semantic intents from changed TypeSpec source. Use AutoRest only to map those intents to REST operations and assess REST compatibility; use TCGC only for downstream SDK analysis.
- Check deterministic hunk coverage in `model-input.json`. Resolve only its declared `artifactReferences` and `evidenceSetId` entries when full evidence is needed. Skip inference when all hunks have candidates or explicit deterministic classifications. For `unknown` hunks only, write and validate `inference.json` before final judgment.
- In the final bounded Agent phase, summarize each Semantic intent once, judge every deterministic and inferred REST/downstream candidate, rank the catalog once across all Semantic intents, fetch four official documents once, record the shared search evidence, then assess each intent against that shared document set. Do not assess each operation or invent URLs, operations, symbols, sources, or guidance.
- Treat a completed search with no governing guidance as `no-applicable-guidance`; count it as assessed and do not create a blocker. Reserve `not-assessed` for an incomplete or blocked Azure Guidelines assessment.
- Check documentation deterministically from compiler results. A changed declaration is complete when the compiler returns a nonempty effective document, including inherited documentation. Missing or empty documentation creates a finding. Never compare documentation text with code or send documentation to the Agent.
- Report Azure Guidelines and Documentation Completeness independently with explicit coverage. Documentation with no changed compiler declarations is `not-applicable`; unavailable compiler evidence remains `not-assessed`.
- Overall safety covers REST and downstream SDK impact only.
- Retain blockers as **Potential limits** in the report appendix and stop after assessment. Do not author fixes or remediate TypeSpec.
