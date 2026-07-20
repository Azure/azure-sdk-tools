---
name: azure-typespec-author
license: MIT
metadata:
  version: "1.0.0"
description: "Authors and modifies Azure TypeSpec (.tsp) API specifications. USE FOR: any TypeSpec/tsp change — api versions (add, bump, preview, stable, promote), resources, operations, models, properties, decorators, visibility, constraints, breaking changes, LRO, suppressions, operationId, spread model. Covers ARM resource-manager and data-plane services. DO NOT USE FOR: SDK generation, releasing SDK packages, or single MCP tool calls. INVOKES: azure-sdk-mcp:azsdk_run_typespec_validation."
compatibility: "azure-sdk-mcp server with azsdk_run_typespec_validation tool (and azsdk_typespec_generate_authoring_plan for the KB fallback)"
---

# Azure TypeSpec Author

This skill authors and modifies Azure TypeSpec (`.tsp`) API specifications for ARM resource-manager and data-plane services, covering versioning, resources, operations, models, decorators, constraints, and other schema changes that must follow the repository's TypeSpec authoring workflow.

| Tool                                          | Purpose           |
| --------------------------------------------- | ----------------- |
| `azure-sdk-mcp:azsdk_run_typespec_validation` | Validate TypeSpec |

**Prerequisite:** `azure-sdk-mcp` server must be running.

> The authoring plan is built primarily via **agentic search** (fetching curated documentation URLs from [reference-document-links.md](references/reference-document-links.md) client-side). When a request is **not covered** by that curated catalog, the skill falls back to the knowledge base via the `azsdk_typespec_generate_authoring_plan` MCP tool.

# When to invoke the azure-typespec-author skill

The `azure-typespec-author` skill **must** be invoked immediately in all modes (including plan mode) for any task that involves creating and modifying TypeSpec (`.tsp`) files except for `client.tsp` under the specification directory in this repository. This includes but is not limited to:

- Adding, bumping, or promoting API versions (preview, stable)
- Adding or modifying resources, operations, models, properties, or decorators
- Changing visibility, constraints, breaking changes, LRO patterns, or suppressions
- Defining or updating operationId, spread models, or extension resources
- Converting Swagger to TypeSpec (post-conversion edits)

## MCP Tools

| Tool                                                   | Purpose                                                   |
| ------------------------------------------------------ | --------------------------------------------------------- |
| `azure-sdk-mcp:azsdk_typespec_generate_authoring_plan` | Generate grounded authoring plan (**KB fallback** — only when the request is not covered by the curated catalog) |
| `azure-sdk-mcp:azsdk_run_typespec_validation`          | Validate TypeSpec                                         |

**Prerequisite:** `azure-sdk-mcp` server must be running.

## Rules

- **Always follow the full workflow** — even seemingly simple changes (e.g. adding a default value) can require complex versioning decorator changes. Never skip steps.
- **Mandatory for ALL `.tsp` edits** — even a single `?` change can be breaking.
- **Minimal, scoped edits** — only change what the request requires.
- **Always validate** — run every steps in [validation](references/validation.md) after every edit.
- **Always cite references** — provide links that justify the approach.
- **Follow the authoring plan exactly** — code changes in Step 4 MUST follow the authoring plan generated in Step 3. Do not deviate by referring to existing code patterns in the TypeSpec project; the authoring plan is the single source of truth for what to change.

## Steps

> Analyze → Intake → Plan → Apply → Validate → Output reference links → Telemetry

- [ ] Step 1 — Analyze project
- [ ] Step 2 — Intake
- [ ] Step 3 — Build authoring plan
- [ ] Step 4 — Apply changes
- [ ] Step 5 — Validate
- [ ] Step 6 — Output reference links
- [ ] Step 7 — Emit session telemetry (best-effort, on by default)

### Step 1: Analyze Project

See [analyze-project.md](references/analyze-project.md).

### Step 2: Intake

See [intake.md](references/intake.md).

### Step 3: Build Authoring Plan

See [authoring-plan.md](references/authoring-plan.md). Intake always runs **agentic search** over the curated documentation in [reference-document-links.md](references/reference-document-links.md); the plan is built from those results, and only if agentic search surfaces no relevant guidance does it fall back to the knowledge base (`azsdk_typespec_generate_authoring_plan`).

### Step 4: Apply Changes

Make minimal `.tsp` edits following the plan from Step 3. Confirm uncertainties with the user first.

### Step 5: Validate

See [validation.md](references/validation.md). Run 5.1 (TypeSpec validation) and 5.2 (`tsp compile .`) always; 5.3 (example verification) for API Versioning (Case 3) only.

### Step 6: Output Reference Links

Output all referenced document URLs from Step 3. This gives the user direct links to the documentation that informed the changes.

### Step 7: Emit Session Telemetry (best-effort, on by default)

At the end of every authoring task, report **one anonymized** telemetry record for this session to the hosted feedback agent so the self-evolution loop can learn from real usage. This step runs **by default**. To disable it, set the environment variable `AZURE_TYPESPEC_AUTHOR_FEEDBACK=0` — when set to `0`, skip Step 7 entirely.

This is **best-effort and non-blocking**: the helper always exits 0, uses a short timeout, and no-ops if `az login`/deps/network are unavailable. If it fails or is skipped, ignore it silently — never surface an error to the user and never block on it.

Run once, at the very end of the task:

```
python .github/skills/azure-typespec-author/foundry-agent-hackathon/feedback-agent/send_feedback.py \
  --prompt "<anonymized request gist — no file contents, secrets, or PII>" \
  --outcome <success|failure|partial> \
  --skill-triggered <true|false> \
  --asked-clarifying-questions <true|false> \
  --tool-call-errors <count> \
  --retries <count>
```

The target Foundry project defaults to the hackathon project but is overridable via the `FEEDBACK_PROJECT_ENDPOINT` / `FEEDBACK_AGENT_NAME` environment variables. See [feedback-agent/README.md](foundry-agent-hackathon/feedback-agent/README.md) for details.

## Reference Files

| File                                                                  | Purpose                                     |
| --------------------------------------------------------------------- | ------------------------------------------- |
| [analyze-project.md](references/analyze-project.md)                   | Step 1: project analysis                                 |
| [intake.md](references/intake.md)                                     | Step 2: general + case-specific intake                   |
| [authoring-plan.md](references/authoring-plan.md)                     | Step 3: build authoring plan via agentic search, or KB fallback |
| [agentic-search.md](references/agentic-search.md)                     | Procedure: fetch URLs → extract guidance                 |
| [reference-document-links.md](references/reference-document-links.md) | Catalog of external guide URLs, categorized by case      |
| [validation.md](references/validation.md)                             | Step 5: validate → compile → verify                      |

## Examples

- "Add a new preview API version 2026-01-01-preview for widget resource manager"
- "Add an ARM resource named Asset with CRUD operations"
- "Add a new property to the Widget model"
