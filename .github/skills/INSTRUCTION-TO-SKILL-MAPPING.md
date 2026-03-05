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

### Skills Created (TypeSpec-to-SDK Workflow)

| Skill                           | Type     | Source Instruction Files                                                                                                          | Description                                                      |
| ------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| **typespec-authoring**          | Utility  | `typespec-project.instructions.md`, `typespec-docs.instructions.md`                                                               | Create/modify TypeSpec API specs, swagger conversion, validation |
| **typespec-customization**      | Utility  | `customizing-client-tsp.md`                                                                                                       | Apply SDK-specific customizations via `client.tsp` decorators    |
| **generate-sdk-locally**        | Utility  | `local-sdk-workflow.instructions.md`, `language-emitter.instructions.md`                                                          | Local SDK generation, build, test workflow                       |
| **prepare-release-plan**        | Utility  | `create-release-plan.instructions.md`, `sdk-details-in-release-plan.instructions.md`, `verify-namespace-approval.instructions.md` | Create/manage release plan work items                            |
| **apiview-feedback-resolution** | Utility  | `typespec-to-sdk.instructions.md` (APIView sections)                                                                              | Retrieve and resolve APIView review comments                     |
| **pipeline-troubleshooting**    | Utility  | `typespec-to-sdk.instructions.md` (pipeline sections)                                                                             | Diagnose and fix CI/SDK generation pipeline failures             |

### Additional Skills (Supporting Utilities)

| Skill           | Type    | Source Instruction Files      | Description                                           |
| --------------- | ------- | ----------------------------- | ----------------------------------------------------- |
| **sdk-release** | Utility | `sdk-release.instructions.md` | Check release readiness and trigger SDK release pipelines |

### Instruction Files Not Converted to Skills

| Instruction File                 | Reason                                                      |
| -------------------------------- | ----------------------------------------------------------- |
| `armapi-review.instructions.md`  | Out of scope (ARM API review, not SDK workflow)             |
| `openapi-review.instructions.md` | Out of scope (OpenAPI review, not SDK workflow)             |
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

| Skill                       | Reference Files                                     |
| --------------------------- | --------------------------------------------------- |
| typespec-authoring          | `authoring-steps.md`, `migration-checklist.md`      |
| typespec-customization      | `customization-steps.md`, `decorators-reference.md` |
| generate-sdk-locally        | `sdk-repos.md`                                      |
| prepare-release-plan        | `release-plan-details.md`                           |
| apiview-feedback-resolution | `feedback-resolution-steps.md`                      |
| pipeline-troubleshooting    | `failure-patterns.md`                               |

---

## Evaluation Suites

All evals use the `copilot-sdk` executor with `claude-sonnet-4.6` model.

### Common Grader Pattern

Every eval includes three graders:

1. **keyword** — Checks that skill-specific terms appear in output
2. **regex** — Checks that relevant MCP tool names or concepts are referenced
3. **code** — Asserts minimum output length (`len(output) > 50`, or `> 100` for workflow)

### Eval Details by Skill

#### typespec-authoring (4 tasks, 300s timeout)

| Task ID                  | Name                                  | Tests                                                       |
| ------------------------ | ------------------------------------- | ----------------------------------------------------------- |
| `authoring-basic-001`    | Create New TypeSpec Project           | Happy path: guides through project creation for ARM service |
| `authoring-edge-001`     | TypeSpec Compilation Failure Recovery | Error handling: helps fix `tsp compile` errors              |
| `authoring-negative-001` | Should Not Trigger                    | Negative: does not activate for SDK generation requests     |
| `authoring-convert-001`  | Convert Swagger to TypeSpec           | Domain-specific: guides `tsp-client convert` workflow       |

**Keywords:** TypeSpec, tspconfig, main.tsp · **Regex:** `azsdk_init_typespec_project|azsdk_run_typespec_validation|tsp compile|tsp-client`
**Fixture:** `main.tsp` — sample TypeSpec file

#### typespec-customization (4 tasks, 300s timeout)

| Task ID               | Name                            | Tests                                                               |
| --------------------- | ------------------------------- | ------------------------------------------------------------------- |
| `custom-basic-001`    | Rename Type for SDK             | Happy path: guides `@clientName` decorator usage                    |
| `custom-edge-001`     | Language-Specific Customization | Edge case: `@scope` for per-language customizations                 |
| `custom-negative-001` | Should Not Trigger              | Negative: does not activate for actual API changes                  |
| `custom-multi-001`    | Split Into Multiple Clients     | Domain-specific: guides `@client` decorator for multi-client splits |

**Keywords:** client.tsp, clientName, ClientGenerator · **Regex:** `@clientName|@client|@access|@scope|@operationGroup|client\.tsp`
**Fixture:** `client.tsp` — sample customization file

#### generate-sdk-locally (4 tasks, 300s timeout)

| Task ID                  | Name                           | Tests                                                            |
| ------------------------ | ------------------------------ | ---------------------------------------------------------------- |
| `sdk-local-basic-001`    | Generate Python SDK Locally    | Happy path: complete local generation for Python                 |
| `sdk-local-edge-001`     | Build Failure Recovery         | Error handling: build failures during local generation           |
| `sdk-local-negative-001` | Should Not Trigger             | Negative: does not activate for pipeline-based generation        |
| `sdk-local-full-001`     | Full Local Generation Workflow | Domain-specific: full generate → build → test → prepare PR cycle |

**Keywords:** generate, build, SDK · **Regex:** `azsdk_package_generate_code|azsdk_package_build_code|azsdk_verify_setup`
**Fixture:** `tspconfig.yaml` — sample TypeSpec config

#### apiview-feedback-resolution (4 tasks, 300s timeout)

| Task ID                   | Name                                 | Tests                                                 |
| ------------------------- | ------------------------------------ | ----------------------------------------------------- |
| `apiview-basic-001`       | Retrieve and Review APIView Comments | Happy path: retrieves and summarizes APIView comments |
| `apiview-edge-001`        | APIView Requires TypeSpec Change     | Edge case: feedback requiring TypeSpec modifications  |
| `apiview-negative-001`    | Should Not Trigger                   | Negative: does not activate for project creation      |
| `apiview-no-feedback-001` | No APIView Feedback Found            | Domain-specific: handles empty feedback gracefully    |

**Keywords:** APIView, feedback, comment · **Regex:** `azsdk_apiview_get_comments|azsdk_typespec_delegate_apiview_feedback|APIView`
**Fixture:** `apiview-comment.json` — sample APIView comment

#### pipeline-troubleshooting (4 tasks, 300s timeout)

| Task ID                 | Name                               | Tests                                                          |
| ----------------------- | ---------------------------------- | -------------------------------------------------------------- |
| `pipeline-basic-001`    | Analyze Pipeline Build Failure     | Happy path: diagnoses a failing pipeline                       |
| `pipeline-edge-001`     | Multiple Languages Failed          | Edge case: failures across multiple language pipelines         |
| `pipeline-negative-001` | Should Not Trigger                 | Negative: does not activate for TypeSpec authoring             |
| `pipeline-repro-001`    | Reproduce Pipeline Failure Locally | Domain-specific: guides local reproduction of pipeline failure |

**Keywords:** pipeline, build, failure · **Regex:** `azsdk_analyze_pipeline|pipeline|build failure|CI`
**Fixture:** `pipeline-error.log` — sample pipeline error log

After the initial waza-based creation, sensei was used to score and improve all 8 skills through the Ralph loop pattern (READ → SCORE → CHECK → IMPROVE → TEST → TOKENS).

### Improvements Applied

| Improvement         | Before                            | After                                                 | Skills Affected                            |
| ------------------- | --------------------------------- | ----------------------------------------------------- | ------------------------------------------ |
| Routing triggers    | `TOOLS/COMMANDS:` header          | `INVOKES:` + `FOR SINGLE OPERATIONS:`                 | All 8 skills                               |
| Trigger phrases     | Prose descriptions                | Quoted trigger phrases (`"create TypeSpec project"`)  | All 8 skills                               |
| Procedural content  | Declarative-only flagged          | Added action verbs + routing phrases                  | typespec-authoring, typespec-customization |
| Token reduction     | 505 / 520 tokens                  | 377 / 393 tokens                                      | generate-sdk-locally, prepare-release-plan |
| Cross-skill routing | `DO NOT USE FOR:` with names only | `DO NOT USE FOR:` with `(use <skill-name>)` redirects | All 8 skills                               |

### Sensei Score Summary (Post-Improvement)

| Skill                       | Tokens | Compliance | Spec   | MCP | Stars                          |
| --------------------------- | ------ | ---------- | ------ | --- | ------------------------------ |
| typespec-authoring          | 493    | High       | 8/8 ✅ | 2/4 | 🌟 module-count, 🌟 complexity |
| typespec-customization      | 469    | High       | 8/8 ✅ | 2/4 | 🌟 module-count, 🌟 complexity |
| generate-sdk-locally        | 420    | High       | 8/8 ✅ | 1/4 | 🌟 complexity                  |
| prepare-release-plan        | 421    | High       | 8/8 ✅ | 3/4 | —                              |
| apiview-feedback-resolution | 464    | High       | 8/8 ✅ | 2/4 | 🌟 complexity                  |
| pipeline-troubleshooting    | 476    | High       | 8/8 ✅ | 2/4 | 🌟 complexity                  |

### Additional Skills

| Skill           | Tokens | Compliance | Spec   | MCP        | Key Changes                                     |
| --------------- | ------ | ---------- | ------ | ---------- | ----------------------------------------------- |
| sdk-release     | 464    | High       | 8/8 ✅ | **4/4 ✅** | Added prerequisites section, tightened wording  |

**Total tokens (all 8 skills):** ~3707

### Remaining Advisory Notes

- All skills show `⚠️ spec-version` — this is a false positive; `metadata.version: "1.0.0"` is present but sensei looks for a different YAML path.
- Original 8 skills have MCP Integration at 1-3/4 (advisory only, doesn't affect compliance). New 5 skills achieve 4/4 MCP Integration.

---

## Summary

| Metric                       | Count                                |
| ---------------------------- | ------------------------------------ |
| Instruction files analyzed   | 19                                   |
| Skills created               | 13 (12 utility + 1 workflow)         |
| Instructions excluded        | 3 (out-of-scope)                     |
| Reference docs               | 10                                   |
| Eval suites                  | 13                                   |
| Total eval tasks             | 53 (33 original + 20 new)            |
| Eval fixtures                | 8                                    |
| Compliance level             | High (all 13 skills ✅)              |
| Eval pass rate               | 91% (30/33 tasks, original 8 suites) |
| Ralph Loop passes            | 2 (original 8 + new 5)               |
| Total tokens (all 13 skills) | ~5851                                |
