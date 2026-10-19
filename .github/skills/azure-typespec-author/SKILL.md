---
name: azure-typespec-author
license: MIT
metadata:
  version: "1.0.0"
description: "Authors Azure TypeSpec API specifications. MUST BE USED for all .tsp changes except naming-only client.tsp SDK overrides handled by azsdk-common-typespec-naming. WHEN: api versions, resources, operations, models, properties, decorators, visibility, constraints, breaking changes, LRO, suppressions, operationId, spread models for ARM or data-plane. DO NOT USE FOR: naming-only client.tsp overrides, SDK generation/releases, or single MCP calls. INVOKES: azure-sdk-mcp:azsdk_typespec_retrieve_knowledge, azure-sdk-mcp:azsdk_run_typespec_validation."
compatibility: "azure-sdk-mcp server with azure-sdk-mcp:azsdk_typespec_retrieve_knowledge and azure-sdk-mcp:azsdk_run_typespec_validation tools"
---

# Azure TypeSpec Author

This skill authors and modifies Azure TypeSpec (`.tsp`) API specifications for ARM resource-manager and data-plane services, covering versioning, resources, operations, models, decorators, constraints, and other schema changes that must follow the repository's TypeSpec authoring workflow.

## Triggers

USE FOR: TypeSpec/tsp changes except naming-only client.tsp SDK overrides — api versions (add, bump, preview, stable, promote), resources, operations, models, properties, decorators, visibility, constraints, breaking changes, LRO, suppressions, operationId, spread model
WHEN: "add TypeSpec API version", "modify .tsp file", "change TypeSpec decorators", "update TypeSpec models or operations", "author Azure TypeSpec"
DO NOT USE FOR: naming-only client.tsp SDK overrides, SDK generation, releasing SDK packages, or single MCP tool calls

The `azure-typespec-author` skill **must** be invoked immediately in all modes (including plan mode) for any task that involves creating and modifying TypeSpec (`.tsp`) files except for `client.tsp` under the specification directory in this repository. **This skill MUST be used regardless of how simple the task appears** — there are no "simple" TypeSpec edits. Even trivial-seeming changes (adding a single enum value, one property, one operation) require the full workflow because versioning decorators, validation, and compliance checks are mandatory.

For naming-only `client.tsp` requests, invoke `azsdk-common-typespec-naming` directly; it applies authorized SDK overrides through scoped direct edits, not the customization tool. Other SDK customizations remain with the existing customization workflow. This exception does not bypass naming guidance.

This includes but is not limited to:

- Adding, bumping, or promoting API versions (preview, stable) for ARM or data-plane services
- Adding or modifying resources, operations, models, properties, or decorators
- Changing visibility, constraints, breaking changes, LRO patterns, or suppressions
- Defining or updating operationId, spread models, or extension resources
- Converting Swagger to TypeSpec (post-conversion edits)

## MCP Tools

| Tool                                              | Purpose                                                                                                                                                                                              |
| ------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `azure-sdk-mcp:azsdk_typespec_retrieve_knowledge` | Retrieve knowledge for requests **not** covered by the nine categories in [reference-document-links.md](references/reference-document-links.md). Covered cases use agentic search (`web_fetch`) instead. |
| `azure-sdk-mcp:azsdk_run_typespec_validation`     | Validate TypeSpec                                                                                                                                                                                    |

**Prerequisite:** `azure-sdk-mcp` server must be running.

Install `azsdk-common-typespec-naming` alongside this skill. Invoke it by name for naming guidance; do not copy its rules into this skill. If unavailable, report the naming coverage gap rather than inventing a replacement.

## Rules

- **Do NOT skip this skill for "simple" declaration tasks** — there are no simple TypeSpec source edits. A single property addition can require `@added` decorators, version gating, and validation. The only `.tsp` exception is a naming-only `client.tsp` SDK override routed directly to `azsdk-common-typespec-naming`.
- **Always follow the full workflow** — even seemingly simple changes (e.g. adding a default value) can require complex versioning decorator changes. Never skip steps.
- **Mandatory for ordinary `.tsp` edits** — even a single `?` change can be breaking. Do not invoke this skill for the naming-only `client.tsp` exception.
- **Minimal, scoped edits** — only change what the request requires.
- **Keep source and client plans separate** — Step 3 plans ordinary TypeSpec source changes. Step 4 offers an explicit opt-out from the [client configuration workflow](references/client-configuration.md), which is recommended and included by default for SDK-visible changes. The authoring skill owns declaration edits; the naming skill owns `client.tsp` edits through its apply workflow.
- **Always validate** — run every steps in [validation](references/validation.md) after every edit.
- **Always cite references** — provide links that justify the approach.
- **Preserve source intake confirmations** — follow the feature-exclusion and
  confirmation steps in [intake.md](references/intake.md). Client configuration has its own
  opt-out default and does not replace source intake.
- **Do not call `azsdk_typespec_generate_authoring_plan`** — build the source
  plan from the selected references or `azsdk_typespec_retrieve_knowledge`.
- **Use agentic search for cataloged cases** — when the request matches a case
  in [reference-document-links.md](references/reference-document-links.md),
  select its URLs and use `web_fetch` as described in
  [agentic-search.md](references/agentic-search.md). Use
  `azsdk_typespec_retrieve_knowledge` only when the catalog does not cover the
  request.
- **Preserve dependency setup** — never delete `node_modules`, `packages`,
  restored tools, or fixture links during validation cleanup, even if they were
  installed or restored in the current turn. They are environment setup, not
  compiler output.
- **Follow both plans exactly** — source changes in Step 5 must follow the
  TypeSpec source plan from Step 3; client configuration changes must follow the
  separate plan from Step 4. Do not improvise from unrelated project patterns.

## Steps

> Analyze → Intake → Plan TypeSpec source → Optional client configuration → Apply → Validate → Output reference links

- [ ] Step 1 — Analyze project
- [ ] Step 2 — Intake
- [ ] Step 3 — Build TypeSpec source plan
- [ ] Step 4 — Build client configuration plan unless opted out
- [ ] Step 5 — Apply changes
- [ ] Step 6 — Validate
- [ ] Step 7 — Output reference links

### Step 1: Analyze Project

See [analyze-project.md](references/analyze-project.md).

### Step 2: Intake

See [intake.md](references/intake.md).

### Step 3: Build TypeSpec Source Plan

See [authoring-plan.md](references/authoring-plan.md).

### Step 4: Optional Client Configuration

See [client-configuration.md](references/client-configuration.md). Invoke
`azsdk-common-typespec-naming` for SDK-visible changes unless the user explicitly
opts out. Do not pause source authoring to confirm the recommended default, and
do not invoke the naming skill when the user opts out.

### Step 5: Apply Changes

Make minimal `.tsp` declaration edits following the source plan from Step 3. If
Step 4 produced a client configuration plan, use
`azsdk-common-typespec-naming` in apply mode; it directly edits scoped naming
overrides and necessary imports/loading without calling the customization tool.
Do not bypass that workflow. Complete the required source intake confirmations
before applying changes.

### Step 6: Validate

See [validation.md](references/validation.md). Always run 5.1 general validation (5.1.1 TypeSpec validation and 5.1.2 `tsp compile .`); run 5.2 case-specific validation whenever its case matches.

**Output the validation results as a checklist.** Report one line per check, each marked ✅ (pass) or ❌ (fail) with a short note. Fix every ❌ and re-run until all checks pass. For API Versioning (Case 3) the checklist **must** cover every §5.2 Case 3 check — including: the new version's `examples/` folder exists; no example folder remains for a version absent from the `Versions` enum; no decorator references a version absent from the enum; every carried-over feature is present with its decorators rebased onto the new version (not reverted); every excluded feature is fully removed.

### Step 7: Output Reference Links

Output all referenced document URLs from Step 3. This gives the user direct links to the documentation that informed the changes.

## Reference Files

| File                                                                  | Purpose                                    |
| --------------------------------------------------------------------- | ------------------------------------------ |
| [analyze-project.md](references/analyze-project.md)                   | Step 1: project analysis                   |
| [intake.md](references/intake.md)                                     | Step 2: general + case-specific intake     |
| [authoring-plan.md](references/authoring-plan.md)                     | Step 3: build TypeSpec source plan         |
| [client-configuration.md](references/client-configuration.md)         | Step 4: optional client configuration plan |
| [agentic-search.md](references/agentic-search.md)                     | Procedure: fetch URLs → extract guidance   |
| [reference-document-links.md](references/reference-document-links.md) | Catalog of external guide URLs             |
| [validation.md](references/validation.md)                             | Step 6: validate → compile → verify        |

## Examples

- "Add a new preview API version 2026-01-01-preview for widget resource manager"
- "Add an ARM resource named Asset with CRUD operations"
- "Add a new property to the Widget model"
- "Add a list operation for the WidgetSuite resource using Azure.Core templates"
- "Add a new preview API version to a data-plane service"
- "Create a data-plane resource interface with full CRUD and list operations"
