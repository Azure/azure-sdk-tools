# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 General (All Cases)

Build the authoring plan by grounding it in authoritative guidance. **Agentic search is the primary source; the MCP tool is the fallback used whenever the reference docs do not cover the case.**

1. **Agentic Search (primary)** — run [agentic search](agentic-search.md) with URLs from [reference-document-links.md](reference-document-links.md) and a query derived from the user's request and Step 1–2 context. Synthesize the extracted content into a concrete plan.

2. **MCP Tool (fallback)** — call `azsdk_typespec_generate_authoring_plan` when **any** of the following holds:
   - **Case not found** — agentic search does not surface guidance covering the case (the reference docs lack a relevant pattern for the request).
   - **Insufficient guidance** — the extracted content is ambiguous, incomplete, or not concrete enough to produce a plan for Step 4.
   - **Search failed** — agentic search fails technically (all URLs unreachable or timeout exceeded).

   Call it with:
   - `request`: user request (verbatim)
   - `additionalInformation`: all context from Steps 1–2
   - `typeSpecProjectRootPath`: project root path

   Use the returned plan (and its references) as the basis for Step 4. **Do not skip this fallback** — when the reference docs do not cover the case, the MCP tool MUST be called rather than authoring from model knowledge alone.

> **Conflict resolution**: If both sources produce guidance and they conflict, rely on agentic search (primary documentation) over the MCP tool.

---

## 3.2 Case-Specific Authoring Plan

### Case 3 — API Version Evolution (ARM / Data-plane)

> **Primary**: use Agentic Search (§3.1 option 1) to build the plan. If agentic search does not cover the version-evolution case (case not found or guidance insufficient), fall back to the MCP tool per §3.1 option 2 — do not author from model knowledge alone.

1. Copy `.json` files from latest version's `examples/` into new version's `examples/`. Update `api-version` in each file. Delete old version's example folder if old version is no longer existed.
2. Update `readme.md`.

> These steps apply to both ARM and data-plane services. The same versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`) apply regardless of service type.

### Case 4 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.
