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

1. Read the [assessment workflow](references/workflow.md).
2. Run `node .github/skills/azure-typespec-assessment/scripts/prepare-assessment.mjs`.
3. Analyze the evidence using the [classification rules](references/classification.md).
4. Write `assessment.json` following the [output contract](references/output-contract.md).
5. Render `assessment.md` with `node .github/skills/azure-typespec-assessment/scripts/render-assessment.mjs assessment.json assessment.md`.
6. Run `node .github/skills/azure-typespec-assessment/scripts/validate-assessment.mjs assessment.json assessment.md`.

## Rules

- Assess only files in the resolved Git diff.
- Correlate low-level edits into the user's intended API change.
- Enumerate every affected operation and its complete REST behavior.
- Treat generated artifacts as evidence, not the sole classifier.
- Every result must link to TypeSpec source lines.
- Run the repository-native TypeSpec Validation CLI for every affected head
  project. Treat a failed or unavailable validation as blocking evidence; a
  pass proves only the checks implemented by that tool.
- Assess compliance from fetched authoritative TypeSpec Azure documentation,
  following [the compliance procedure](references/compliance.md), which reuses
  the author skill's agentic search workflow. Do not substitute an OpenAPI
  validation tool or maintain hard-coded compliance rules.
- Do not modify TypeSpec; use `azure-typespec-author` for fixes.

See [REST-compatible downstream breaking cases](references/rest-compatible-downstream-breaking-cases.md) for representative classifications.
