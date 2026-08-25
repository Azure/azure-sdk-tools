---
name: azure-typespec-assessment
description: 'Assess only the current Git diff in Azure TypeSpec specifications. Produces source-linked semantic intent, REST breaking changes, REST-compatible downstream breaking changes, and documentation-grounded Azure TypeSpec compliance results. WHEN: "assess TypeSpec changes", "review TypeSpec diff", "check TypeSpec breaking changes", "assess TypeSpec compliance", "explain TypeSpec REST impact".'
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility: "Git, Node.js 22+, TypeSpec project dependencies"
---

# Azure TypeSpec Assessment

Assess changed TypeSpec without editing it.

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
   groups, compact before/after summaries, source-impact links, and candidates
   instead of reconstructing operation details from raw artifact diffs.
5. Write `assessment.json` following the [output contract](references/output-contract.md).
6. Render and validate the report as described in the workflow.

For an impact-only fast assessment, run analysis with `--fast`, review every
candidate in `fast-model-input.json`, write
`fast-assessment-judgment.json` following
`scripts/fast-assessment-judgment.schema.json`, then run:

```bash
node .github/skills/azure-typespec-assessment/scripts/assemble-fast-assessment.mjs \
  fast-model-input.json fast-assessment-judgment.json fast-assessment.html
```

## Rules

- Assess only files in the resolved Git diff.
- Correlate low-level edits into the user's intended API change.
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
  agentic search workflow. Compare guidance directly with changed TypeSpec
  source; do not substitute validation tools, generated artifacts, or hard-coded
  compliance rules.

See [REST-compatible downstream breaking cases](references/rest-compatible-downstream-breaking-cases.md) for representative classifications.
