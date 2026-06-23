# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 General (All Cases)

Use **both** tools to gather comprehensive guidance, then synthesize into a concrete plan:

### Step A: Retrieve AI-Guided Knowledge (All Cases)

Call `azsdk_typespec_retrieve_knowledge` with:
- `request`: the user's request (verbatim)
- `typeSpecProjectRootPath`: the project root path

Extract the `context` field from the tool response. This provides AI-generated authoring guidance based on the TypeSpec project.

### Step B: Fetch Reference Documentation (All Cases)

Run [agentic search](agentic-search.md) using URLs from [reference-document-links.md](reference-document-links.md) relevant to your case (identified in Step 2). Extract specific guidance for your scenario.

### Step C: Synthesize and Resolve Conflicts

Combine guidance from Steps A and B:
- If results agree → use the guidance directly
- If results conflict → **prioritize AI-Guided knowledge** (MCP retrievalare is authoritative; reference docs are supportive)
- If guidance is incomplete → refine your query and repeat Step B

Document your final plan with references to supporting documents.

---

## 3.2 Case-Specific Authoring Plan

### Case 1 — Add Resource Type (ARM)

**Tools:** Use agentic search (Step 3.1.B). Reference docs: [Resource modeling guide](https://azure.github.io/typespec-azure/docs/howtos/resource-manager/01-resource-modeling/), [Resource lifecycle patterns](https://azure.github.io/typespec-azure/docs/howtos/resource-manager/02-resource-lifecycle/).

**Plan steps:**
1. Define the resource as either `TrackedResource` (top-level) or `ProxyResource` (nested).
2. Add default CRUD operations: `createOrReplace` (PUT/async), `get` (GET), `update` (PATCH/sync), `delete` (DELETE/async).
3. For top-level resources: add `listByResourceGroup` and `listBySubscription` list operations.
4. For nested resources: add `listByParent` list operation.
5. Define required and optional properties based on intake requirements.
6. Apply ARM-specific decorators (e.g., `@armResourceOperations` on operations, `@visibility` for API evolution).

**Key defaults:**
- Use `createOrReplace`, never `createOrUpdate`
- Use `ArmCustomPatch` for PATCH operations
- Async by default: PUT, DELETE; sync by default: PATCH; depends on user intent: POST/actions

### Case 2 — Add Resource Operations (ARM)

**Tools:** Use agentic search (Step 3.1.B). Reference docs: [Resource operations guide](https://azure.github.io/typespec-azure/docs/howtos/resource-manager/02-resource-lifecycle/), [Custom operations and actions](https://azure.github.io/typespec-azure/docs/howtos/resource-manager/05-custom-operations-and-actions/).

**Plan steps:**
1. Identify the target resource and confirm the operation type (standard CRUD or custom action).
2. For standard CRUD operations, apply defaults from Case 1.
3. For custom POST/action operations:
   - Collect request and response model schemas from intake
   - Determine if the operation is async (return LRO) or sync (return result directly)
   - For async POST: use ARM combined headers: `LroHeaders = ArmCombinedLroHeaders<FinalResult = ExportResult>`
4. Add the operation to the resource's `.tsp` file with appropriate decorators.
5. Update `@route` decorators for correct URL patterns (e.g., `@route("{name}/action")` for custom actions).

**Key defaults:**
- GET, LIST, HEAD operations are never async
- PUT, DELETE operations are async by default (confirm with user)
- PATCH operations are sync by default
- Custom POST/action operations require user confirmation on async/sync intent

### Case 3 — API Version Evolution (ARM)

**Tools:** Must use agentic search only (Step 3.1.B). Do not call MCP tool. Reference docs: [Versioning overview](https://azure.github.io/typespec-azure/docs/howtos/versioning/01-about-versioning/), [preview → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/02-preview-after-preview/), [preview → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/), [stable → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/04-preview-after-stable/), [stable → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/05-stable-after-stable/), [Evolving APIs](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/).

**Plan steps:**
1. Identify the version path (e.g., preview → preview, preview → stable, stable → stable) and fetch guidance from the corresponding reference doc.
2. Copy `.tsp` model files from the latest version directory into the new version directory. Apply versioning decorators (`@added`, `@removed`, `@renamedFrom`, etc.) based on what changed.
3. Copy `.json` example files from the latest version's `examples/` directory into the new version's `examples/` directory. Update the `api-version` field in each file to match the new version.
4. Delete the old version's `examples/` folder if that version is deprecated or no longer supported.
5. Update the `readme.md` to include the new version and update version-specific metadata.
6. Verify that all resources, operations, and properties are intentionally carried forward or explicitly versioned out using decorators.

**Key notes:**
- Do not assume all features carry forward — explicitly confirm with user which features to include (see intake.md Case 3)
- Apply versioning decorators only to items that changed (added, removed, renamed, etc.)
- Examples are version-specific; each version gets its own `examples/` folder
