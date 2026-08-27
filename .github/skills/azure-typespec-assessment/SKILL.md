---
name: azure-typespec-assessment
description: 'Read-only assessment of an existing current Git diff in Azure TypeSpec specifications. Produces source-linked semantic intent, REST and downstream impact, and documentation-grounded compliance results. WHEN: "assess the current TypeSpec diff", "review existing TypeSpec changes", "explain the REST impact of current changes", "assess current-diff compliance".'
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility: "Git, Node.js 22+, TypeSpec project dependencies"
---

# Azure TypeSpec Assessment

Assess changed TypeSpec without editing it.

## Activation boundary

Continue only when the requested outcome is a read-only assessment of an
already-existing current Git diff. An explicit assessment request after edits
qualifies.

If the primary request is to create, add, modify, fix, or mitigate TypeSpec,
stop before asking assessment options or running commands. That is an authoring
request and requires `azure-typespec-author`. This skill never edits `.tsp`
files or invokes the authoring skill.

## Workflow

1. Resolve assessment options. Unless already specified by the user, call
   `ask_user` once to select:
   - **Mode:** `complete` (default) or `fast` impact-only.
   - **Baseline branch:** free text, default `main`.
     Normalize `main` to the tracked remote ref (normally `origin/main`).
2. Read the [assessment workflow](references/workflow.md).
3. For complete mode, run
   `node .github/skills/azure-typespec-assessment/scripts/run-assessment-analysis.mjs --base <baseline>`.
   For fast mode, add `--fast`.
4. Review the bounded `model-input.json` using the
   [classification rules](references/classification.md). Use its operation
   groups, semantic review units, compact before/after summaries,
   source-impact links, compliance review items, and candidates instead of
   reconstructing operation details from raw artifact diffs. Write exactly one
   semantic intent per review unit and exactly one compliance decision per
   review item.
5. Write `assessment-judgment.json` following
   `scripts/assessment-judgment.schema.json`.
6. Assemble and validate `assessment.json`, then render `assessment.html`
   following the [output contract](references/output-contract.md) and workflow.

For an impact-only fast assessment, run analysis with `--fast`, review every
candidate in `fast-model-input.json`, write
`fast-assessment-judgment.json` following
`scripts/fast-assessment-judgment.schema.json`, then run:

```bash
node .github/skills/azure-typespec-assessment/scripts/assemble-fast-assessment.mjs \
  fast-model-input.json fast-assessment-judgment.json assessment.html
```

## Rules

- Assess only files in the resolved Git diff.
- Correlate low-level edits into the user's intended API change.
- Preserve the deterministic semantic granularity: one resource family plus
  caller-observable behavior per review unit. Only pure unchanged-contract
  propagation may share a version-lineage intent.
- Enumerate every affected operation and its complete REST behavior.
- Write semantic understanding for TypeSpec authors: use TypeSpec concepts,
  explain operation-level REST behavior, and keep emitter internals such as
  TCGC out of that section.
- Treat generated artifacts as evidence, not the sole classifier.
- Every result must link to TypeSpec source lines.
- Fast assessments cover only REST, SDK/downstream, and compliance impacts.
  Every approved impact must include actual and expected behavior, evidence,
  affected operations, and changed TypeSpec source.
- Assess compliance from fetched authoritative TypeSpec Azure documentation,
  following [the compliance procedure](references/compliance.md) and its local
  agentic search workflow. Decide every bounded declaration-document review
  item and create one declaration-specific finding for each failed decision.
  Compare guidance directly with changed TypeSpec source; do not substitute
  validation tools, generated artifacts, or hard-coded compliance rules.

See [REST-compatible downstream breaking cases](references/rest-compatible-downstream-breaking-cases.md) for representative classifications.
