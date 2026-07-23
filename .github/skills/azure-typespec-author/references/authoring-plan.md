# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 General (All Cases)

Use **both** tools to build an authoring plan, if the retrieved results have conflict, rely on agentic search.

1. **MCP Tool** — call `azsdk_typespec_generate_authoring_plan` with:
   - `request`: user request (verbatim)
   - `additionalInformation`: all context from Steps 1–2
   - `typeSpecProjectRootPath`: project root path

2. **Agentic Search** — run [agentic search](agentic-search.md) with URLs from [reference-document-links.md](reference-document-links.md) and a query from the user's request. Synthesize extracted content into a concrete plan.

> **Fallback**: If agentic search fails (all URLs unreachable or timeout exceeded), proceed with the MCP tool result alone. Do not block the workflow on unreachable external documentation.

---

## 3.2 Case-Specific Authoring Plan

### Case 3 — API Version Evolution (ARM / Data-plane)

> **Must** use Agentic Search (option 2 above) as the primary source to build the plan; do **not** call the MCP tool while agentic search succeeds.
>
> **Fallback**: If agentic search fails (all URLs unreachable or timeout exceeded), fall back to the MCP tool result per the [General Fallback](#31-general-all-cases) rather than dead-ending the workflow. The concrete Case 3 steps below are **mandatory regardless of which grounding path was used**.

1. Copy `.json` files from latest version's `examples/` into new version's `examples/`. Update `api-version` in each file. Delete old version's example folder if old version is no longer existed.
2. Update `readme.md`.

> These steps apply to both ARM and data-plane services. The same versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`) apply regardless of service type.

### Case 4 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.
