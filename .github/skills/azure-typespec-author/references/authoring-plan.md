# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 General (All Cases)

### 3.1.1 Retrieve knowledge

Choose the grounding source based on whether the request's case is covered by [reference-document-links.md](reference-document-links.md):

1. **Case found in the reference doc → Agentic Search.** Run [agentic search](agentic-search.md) — you **MUST** call `web_fetch` on the matching URLs and follow their steps.

2. **Case not found in the reference doc → MCP Tool.** Call `azsdk_typespec_retrieve_knowledge` with:
   - `request`: user request (verbatim)
   - `typeSpecProjectRootPath`: project root path

For a request containing both covered and uncovered changes, use the MCP tool for the uncovered changes. Do not treat a related topic or keyword match as coverage.

### 3.1.2 Generate Authoring Plan

Synthesize the retrieved knowledge in step 3.1.1 into a concrete plan.

Document your final plan with references to supporting documents, and ensure the plan follows the retrieved context above.

---

## 3.2 Case-Specific Authoring Plan

### Case 1 — API Versioning

> API Versioning **is covered** by [reference-document-links.md](reference-document-links.md), so use **Agentic Search** (per [3.1.1 Retrieve knowledge](#311-retrieve-knowledge)) — you **MUST** call `web_fetch` on the matching versioning doc and follow its steps. Do **not** call the MCP tool `azsdk_typespec_retrieve_knowledge` or `azsdk_typespec_generate_authoring_plan` for this case.

1. Create the new version's `examples/<new-version>/` folder by copying the latest retained version's `examples/` into it, and update `api-version` in each `.json` file.
2. Update `readme.md`.

> These steps apply to both ARM and data-plane services. The same versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`) apply regardless of service type.

### Case 2 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.

### SDK Naming — Supplementary Case

Apply alongside the matching case above when the request concerns SDK naming or
adds/changes names for known SDK targets. For unrelated REST work, do not require
SDK-language intake; record naming as not assessed when targets are unknown.
Naming-only `client.tsp` requests go directly to `azsdk-common-typespec-naming`,
not through this authoring plan.

1. Invoke `azsdk-common-typespec-naming` in **guidance mode** with the service type,
   confirmed SDK targets, changed declarations, and release compatibility evidence.
   It selects only applicable profiles; currently C# ARM is covered. No SDK generation
   or linter diagnostic is required to consult the rules.
2. Include its decisions, scopes, exceptions, uncovered targets and blockers in the
   plan. Resolve blocked decisions before the affected edits; preserve shipped SDK
   names, wire contracts and other-language overrides. Do not expand into unrelated cleanup.
3. Keep ownership explicit: this authoring skill edits ordinary TypeSpec declarations.
   The shared naming skill supplies naming decisions and delegates authorized SDK-only
   overrides to `azsdk_customized_code_update` with `SpecInputs`. That customization tool
   owns `client.tsp` imports, placement and edits; the authoring skill never writes them
   directly or duplicates the customization implementation.
4. After apply, retain the normal [validation](validation.md) workflow. Report generated
   SDK names as unverified without generated API evidence.
