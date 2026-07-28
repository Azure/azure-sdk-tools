# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 Retrieve knowledge

Select and execute the appropriate step based on the case identified in Step 2:

- **Case 3 (API Version Evolution):** Execute **Step B** — use reference documentation via agentic search.
- **Other cases:** Execute **Step A** — use `azsdk_typespec_retrieve_knowledge` for AI-guided guidance.

### Step A: Retrieve AI-Guided Knowledge (All cases except Case 3)

Call `azsdk_typespec_retrieve_knowledge` with:
- `request`: the user's request (verbatim)
- `typeSpecProjectRootPath`: the project root path

Extract the `context` field from the tool response. This provides AI-generated authoring guidance based on the TypeSpec project.

### Step B: Fetch Reference Documentation (Case 3 — API Version Evolution)

Run [agentic search](agentic-search.md) using URLs from [reference-document-links.md](reference-document-links.md) relevant to your case (identified in Step 2). Extract specific guidance for your scenario.

## 3.2 Generate Authoring Plan

synthesize the result into a concrete plan derived from the retrieved context in step 3.1.

Document your final plan with references to supporting documents, and ensure the plan follows the retrieved context above.

> **Fallback**: If agentic search fails (all URLs unreachable or timeout exceeded), proceed with the MCP tool result alone. Do not block the workflow on unreachable external documentation.

---

## 3.2 Case-Specific Authoring Plan

### Case 3 — API Version Evolution (ARM / Data-plane)

**Tools:** Use agentic search (Step 3.1.B). Reference docs: [Resource modeling guide](https://azure.github.io/typespec-azure/docs/howtos/resource-manager/01-resource-modeling/), [Resource lifecycle patterns](https://azure.github.io/typespec-azure/docs/howtos/resource-manager/02-resource-lifecycle/).

1. Copy `.json` files from latest version's `examples/` into new version's `examples/`. Update `api-version` in each file. Delete old version's example folder if old version is no longer existed.
2. Update `readme.md`.

> These steps apply to both ARM and data-plane services. The same versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`) apply regardless of service type.

### Case 4 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.
