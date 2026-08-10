# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 General (All Cases)

Choose the grounding source based on whether the request's case is covered by [reference-document-links.md](reference-document-links.md):

1. **Case found in the reference doc → Agentic Search.** Run [agentic search](agentic-search.md) — you **MUST** call `web_fetch` on the matching URLs and follow their steps. Synthesize the extracted content into a concrete plan. Do **not** call the MCP tool.

2. **Case not found in the reference doc → MCP Tool.** Call `azsdk_typespec_generate_authoring_plan` and build the plan from its result:
   - `request`: user request (verbatim)
   - `additionalInformation`: all context from Steps 1–2
   - `typeSpecProjectRootPath`: project root path

---

## 3.2 Case-Specific Authoring Plan

### Case 3 — API Version Evolution (ARM / Data-plane)

> Version evolution **is covered** by [reference-document-links.md](reference-document-links.md), so use **Agentic Search** (per [3.1 General](#31-general-all-cases)) — you **MUST** call `web_fetch` on the matching versioning doc and follow its steps. Do **not** call the MCP tool `azsdk_typespec_generate_authoring_plan` for this case.

**Decide the path first:** if the current latest version is an **unreleased preview** (treat the latest preview as unreleased unless the user says it already shipped), you **collapse** it into the new version (do step 3). If the current latest version is a **released stable**, add the new version **incrementally** with no collapse (skip step 3). Apply this even when the fetched doc shows an incremental keep-the-preview approach.

1. Create the new version's `examples/<new-version>/` folder by copying the latest retained version's `examples/` into it, and update `api-version` in each `.json` file.
2. Update `readme.md`.
3. **Unreleased preview versions are never retained.** When you add a newer version (preview **or** stable), collapse the previous unreleased preview into it: remove that preview from the `Versions` enum, **rename its `examples/<old-preview>/` folder to `examples/<new-version>/`** (move the files, update `api-version`, so no `examples/<old-preview>/` remains), and **rebase every decorator that referenced the preview to the new version** (keep the decorator) so history relative to retained released versions is preserved. **Do NOT drop these decorators to plain baseline when an older retained *released* version exists** — a preview-only `@added` property that is carried keeps its `@added` **rebased** to the new version (e.g. `@added(Versions.<old-preview>)` → `@added(Versions.<new-version>)`), and every `@removed` / `@renamedFrom` / `@typeChangedFrom` that referenced the preview is likewise retargeted to the new version. Delete a feature outright (remove the property/model **and all its decorators**, no `@removed`) only if it is excluded. Making a carried feature plain baseline (no version decorator) is correct **only** when there is no older retained released version at all. Released **stable** versions are always retained — never collapse a stable; add the new version incrementally with decorators that reference it.

> These steps apply to both ARM and data-plane services. The same versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`) apply regardless of service type.

### Case 4 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.
