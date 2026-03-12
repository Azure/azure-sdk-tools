# Instruction File → Skill Mapping

This document maps the original Copilot instruction files to the new waza-based skills,
and describes the evaluation suites created for each skill.

---

## Instruction File Tree

All instruction files are reachable from `.github/copilot-instructions.md`:

```
.github/copilot-instructions.md
├── .github/instructions/typespec-project.instructions.md
├── .github/instructions/language-emitter.instructions.md
├── .github/instructions/sdk-generation.instructions.md
│   ├── eng/common/instructions/azsdk-tools/typespec-to-sdk.instructions.md
│   │   ├── eng/common/instructions/azsdk-tools/local-sdk-workflow.instructions.md
│   │   └── eng/common/instructions/azsdk-tools/create-release-plan.instructions.md
│   │       ├── eng/common/instructions/azsdk-tools/sdk-details-in-release-plan.instructions.md
│   │       └── eng/common/instructions/azsdk-tools/verify-namespace-approval.instructions.md
│   ├── eng/common/instructions/azsdk-tools/typespec-docs.instructions.md
│   └── eng/common/instructions/azsdk-tools/customizing-client-tsp.md  (knowledge file)
├── .github/instructions/github-codingagent.instructions.md
├── .github/instructions/armapi-review.instructions.md
├── .github/instructions/openapi-review.instructions.md
└── .github/instructions/github-actions.instructions.md
```

Additional instruction files exist but are **not linked** from the tree:

- `eng/common/instructions/copilot/sdk-release.instructions.md`

---

## Mapping: Instruction Files → Skills

> **Note:** Only skills included in this PR are listed below. Instruction files that
> map to skills not yet in this PR (e.g., typespec-authoring, typespec-customization)
> are omitted — they will be covered in follow-up PRs.

### Shared Domain Skills (`azsdk-common-*` prefix)

| Skill | Type | Source Instruction Files | Description |
| --- | --- | --- | --- |
| **azsdk-common-generate-sdk-locally** | Utility | `local-sdk-workflow.instructions.md`, `language-emitter.instructions.md` | Local SDK generation, build, test workflow |
| **azsdk-common-prepare-release-plan** | Utility | `create-release-plan.instructions.md`, `sdk-details-in-release-plan.instructions.md`, `verify-namespace-approval.instructions.md` | Create/manage release plan work items |
| **azsdk-common-apiview-feedback-resolution** | Utility | `typespec-to-sdk.instructions.md` (APIView sections) | Retrieve and resolve APIView review comments |
| **azsdk-common-pipeline-troubleshooting** | Utility | `typespec-to-sdk.instructions.md` (pipeline sections) | Diagnose and fix CI/SDK generation pipeline failures |
| **azsdk-common-sdk-release** | Utility | `sdk-release.instructions.md` | Check release readiness and trigger SDK release pipelines |

### Internal Meta-Skills (no prefix)

| Skill | Type | Source Instruction Files | Description |
| --- | --- | --- | --- |
| **markdown-token-optimizer** | Meta | N/A | Optimize markdown documents for token efficiency |
| **sensei** | Meta | N/A | Score and improve skills through the Ralph loop pattern |
| **skill-authoring** | Meta | N/A | Guide creation of new waza-compliant skills |

### Instruction Files Not Converted to Skills (in this PR)

| Instruction File | Reason |
| --- | --- |
| `typespec-project.instructions.md` | Maps to typespec-authoring skill (not in this PR — follow-up) |
| `typespec-docs.instructions.md` | Maps to typespec-authoring skill (not in this PR — follow-up) |
| `customizing-client-tsp.md` | Maps to typespec-customization skill (not in this PR — follow-up) |
| `armapi-review.instructions.md` | Out of scope (ARM API review, not SDK workflow) |
| `openapi-review.instructions.md` | Out of scope (OpenAPI review, not SDK workflow) |
| `github-actions.instructions.md` | Out of scope (GitHub Actions development, not SDK workflow) |

---

## Skill Files Structure

Each skill follows this structure under `.github/skills/`:

```
<skill-name>/
├── SKILL.md              # Skill definition (frontmatter + steps)
└── references/           # Detailed reference docs (keeps SKILL.md under 500 tokens)
    └── *.md

evals/<skill-name>/
├── eval.yaml             # Eval config (graders, timeouts, model)
├── fixtures/             # Domain-appropriate test fixtures
│   └── <file>
└── tasks/                # Individual eval task definitions
    └── *.yaml
```

### Reference Documents

| Skill | Reference Files |
| --- | --- |
| azsdk-common-generate-sdk-locally | `sdk-repos.md` |
| azsdk-common-prepare-release-plan | `release-plan-details.md` |
| azsdk-common-apiview-feedback-resolution | `feedback-resolution-steps.md` |
| azsdk-common-pipeline-troubleshooting | `failure-patterns.md` |

---

## Evaluation Suites

All evals use the `copilot-sdk` executor with `claude-sonnet-4.6` model.

### Common Grader Pattern

Every eval includes three graders:

1. **keyword** — Checks that skill-specific terms appear in output
2. **regex** — Checks that relevant MCP tool names or concepts are referenced
3. **code** — Asserts minimum output length (`len(output) > 50`, or `> 100` for workflow)

### Eval Details by Skill

#### azsdk-common-generate-sdk-locally (4 tasks, 300s timeout)

| Task ID | Name | Tests |
| --- | --- | --- |
| `sdk-local-basic-001` | Generate Python SDK Locally | Happy path: complete local generation for Python |
| `sdk-local-edge-001` | Build Failure Recovery | Error handling: build failures during local generation |
| `sdk-local-negative-001` | Should Not Trigger | Negative: does not activate for pipeline-based generation |
| `sdk-local-full-001` | Full Local Generation Workflow | Domain-specific: full generate → build → test → prepare PR cycle |

**Keywords:** generate, build, SDK · **Regex:** `azsdk_package_generate_code|azsdk_package_build_code|azsdk_verify_setup`
**Fixture:** `tspconfig.yaml` — sample TypeSpec config

#### azsdk-common-apiview-feedback-resolution (4 tasks, 300s timeout)

| Task ID | Name | Tests |
| --- | --- | --- |
| `apiview-basic-001` | Retrieve and Review APIView Comments | Happy path: retrieves and summarizes APIView comments |
| `apiview-edge-001` | APIView Requires TypeSpec Change | Edge case: feedback requiring TypeSpec modifications |
| `apiview-negative-001` | Should Not Trigger | Negative: does not activate for project creation |
| `apiview-no-feedback-001` | No APIView Feedback Found | Domain-specific: handles empty feedback gracefully |

**Keywords:** APIView, feedback, comment · **Regex:** `azsdk_apiview_get_comments|azsdk_typespec_delegate_apiview_feedback|APIView`
**Fixture:** `apiview-comment.json` — sample APIView comment

#### azsdk-common-pipeline-troubleshooting (4 tasks, 300s timeout)

| Task ID | Name | Tests |
| --- | --- | --- |
| `pipeline-basic-001` | Analyze Pipeline Build Failure | Happy path: diagnoses a failing pipeline |
| `pipeline-edge-001` | Multiple Languages Failed | Edge case: failures across multiple language pipelines |
| `pipeline-negative-001` | Should Not Trigger | Negative: does not activate for TypeSpec authoring |
| `pipeline-repro-001` | Reproduce Pipeline Failure Locally | Domain-specific: guides local reproduction of pipeline failure |

**Keywords:** pipeline, build, failure · **Regex:** `azsdk_analyze_pipeline|pipeline|build failure|CI`
**Fixture:** `pipeline-error.log` — sample pipeline error log

After the initial waza-based creation, sensei was used to score and improve all skills through the Ralph loop pattern (READ → SCORE → CHECK → IMPROVE → TEST → TOKENS).

### Improvements Applied

| Improvement | Before | After | Skills Affected |
| --- | --- | --- | --- |
| Routing triggers | `TOOLS/COMMANDS:` header | `INVOKES:` + `FOR SINGLE OPERATIONS:` | All shared domain skills |
| Trigger phrases | Prose descriptions | Quoted trigger phrases (`"create TypeSpec project"`) | All shared domain skills |
| Token reduction | 505 / 520 tokens | 377 / 393 tokens | azsdk-common-generate-sdk-locally, azsdk-common-prepare-release-plan |
| Cross-skill routing | `DO NOT USE FOR:` with names only | `DO NOT USE FOR:` with `(use <skill-name>)` redirects | All shared domain skills |

### Sensei Score Summary (Post-Improvement)

| Skill | Tokens | Compliance | Spec | MCP | Stars |
| --- | --- | --- | --- | --- | --- |
| azsdk-common-generate-sdk-locally | 420 | High | 8/8 ✅ | 1/4 | 🌟 complexity |
| azsdk-common-prepare-release-plan | 421 | High | 8/8 ✅ | 3/4 | — |
| azsdk-common-apiview-feedback-resolution | 464 | High | 8/8 ✅ | 2/4 | 🌟 complexity |
| azsdk-common-pipeline-troubleshooting | 476 | High | 8/8 ✅ | 2/4 | 🌟 complexity |
| azsdk-common-sdk-release | 464 | High | 8/8 ✅ | 4/4 ✅ | — |

### Remaining Advisory Notes

- All skills show `⚠️ spec-version` — this is a false positive; `metadata.version: "1.0.0"` is present but sensei looks for a different YAML path.
- Shared domain skills have MCP Integration at 1-3/4 (advisory only, doesn't affect compliance). sdk-release achieves 4/4 MCP Integration.

---

## Summary

| Metric | Count |
| --- | --- |
| Instruction files analyzed | 19 |
| Skills included in this PR | 8 (5 shared domain + 3 meta) |
| Instructions not mapped | 6 (3 follow-up + 3 out-of-scope) |

> **Note:** This mapping documents the skills included in this PR only.
> Skills like `typespec-authoring` and `typespec-customization` were excluded because they are
> not shared across repos and will be handled in follow-up PRs.
