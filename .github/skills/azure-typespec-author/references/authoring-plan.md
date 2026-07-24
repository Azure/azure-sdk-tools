# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 General (All Cases)

Build the authoring plan by grounding it in authoritative guidance. **Always run agentic search first; use the MCP tool only as a follow-up when search is insufficient.**

1. **Agentic Search** (primary — **always run, never skip**) — run [agentic search](agentic-search.md) with URLs from [reference-document-links.md](reference-document-links.md) and a query from the user's request. **You MUST call `web_fetch` in every case**, even when you believe you already know the answer — do not author from model knowledge without first grounding in the docs. Synthesize the extracted content into a concrete plan.

2. **MCP Tool** (follow-up fallback — **not for Case 3**) — **after** running agentic search, if — and only if — the search did not surface guidance sufficient to build a concrete plan (case not covered by [reference-document-links.md](reference-document-links.md)) **or** agentic search failed technically (all URLs unreachable or timeout exceeded), **additionally** call `azsdk_typespec_generate_authoring_plan` with:
   - `request`: user request (verbatim)
   - `additionalInformation`: all context from Steps 1–2
   - `typeSpecProjectRootPath`: project root path

   Build the plan from the returned result and cite its references in Step 6.

> **Rules**: Never skip agentic search — `web_fetch` runs in every case. Do **not** call the MCP tool when agentic search already covers the case. If agentic search cannot produce a plan (case not covered **or** search failed), the MCP tool may be called rather than authoring from model knowledge alone — **except for [Case 3 — API Version Evolution](#case-3--api-version-evolution-arm--data-plane), which is fully covered by the versioning docs and MUST be grounded with agentic search only (never call the MCP tool)**. If both sources produce guidance and they conflict, rely on agentic search.

---

## 3.2 Case-Specific Authoring Plan

### Case 3 — API Version Evolution (ARM / Data-plane)

> **Grounding:** Build the plan with **agentic search only**. You **MUST** call `web_fetch` over the versioning URLs in [reference-document-links.md](reference-document-links.md) and follow the steps in the fetched docs. You **MUST NOT** call the MCP tool `azsdk_typespec_generate_authoring_plan` for this case — the versioning docs fully cover it.

1. Copy `.json` files from latest version's `examples/` into new version's `examples/`. Update `api-version` in each file. Delete old version's example folder if old version is no longer existed.
2. Update `readme.md`.

> These steps apply to both ARM and data-plane services. The same versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`) apply regardless of service type.

### Case 4 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.
