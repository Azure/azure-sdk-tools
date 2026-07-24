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

> **Grounding (mandatory):** Build the plan with **agentic search only**. You **MUST** call `web_fetch` over the versioning URLs in [reference-document-links.md](reference-document-links.md) (`02-preview-after-preview`, `03-stable-after-preview`, `04-preview-after-stable`, `05-stable-after-stable`, `06-evolving-apis`) — never skip it. You **MUST NOT** call the MCP tool `azsdk_typespec_generate_authoring_plan` for this case; the versioning docs fully cover it. Never author version changes from model knowledge without first fetching the matching doc.

**Step A — Classify the transition.** Find the latest existing version in the `Versions` enum and whether it is `preview` or `stable`, and whether the new version is `preview` or `stable`. Pick the operation:

| Latest existing | New version | Operation | Doc |
| --------------- | ----------- | --------- | --- |
| preview | preview | **RENAME** the latest preview to the new preview (do **not** keep the old preview) | `02-preview-after-preview` |
| stable | preview | **ADD** a new preview version; keep the stable version | `04-preview-after-stable` |
| preview | stable | **RENAME** the latest preview to the new stable (promote; drop `@previewVersion`) | `03-stable-after-preview` |
| stable | stable | **ADD** a new stable version; keep prior versions | `05-stable-after-stable` |

**Step B — Apply the version-enum change.**
- **RENAME operations** (preview→preview, preview→stable): change the enum member **name and value** from the old version to the new version. Then update **every** decorator across all `.tsp` files that references the old version — `@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`, `@returnTypeChangedFrom`, `@madeOptional`, `@madeRequired` — to reference the new version. For preview→stable, also remove the `@previewVersion` decorator from the promoted member.
- **ADD operations** (stable→preview, stable→stable): add a **new** enum member after the latest one (keep all existing members). Add `@previewVersion` to the new member only for a preview version. Gate newly added features with `@added(<newVersion>)`.

**Step C — Apply requested feature exclusions / reversals** (per `06-evolving-apis`, retrieved via `web_fetch`). When the intake step identified features to exclude, or when renaming forward means a preview-only change should not carry into the new version, **reverse** the decorator pattern:
- Type added with `@added(oldVersion)` that should not appear → delete the type.
- `@typeChangedFrom(oldVersion, T)` that does not apply → set the property type back to `T` and remove the decorator.
- `@renamedFrom(oldVersion, "name")` that does not apply → restore the original `name` and remove the decorator.
- `@madeOptional` / `@madeRequired` that does not apply → reverse the optionality and remove the decorator.
- A property using the `@removed`/`@renamedFrom` (old) + `@added` (new) pattern to change a **default value or decoration** (e.g. an `oldAge`/`age` pair) that should not carry forward → **remove both the old and new properties and restore the single original property** without the change.

**Step D — Migrate examples and metadata.**
1. For **RENAME** operations, rename the latest version's `examples/<oldVersion>/` folder to `examples/<newVersion>/` and update `api-version` in every `.json` file (the old folder must no longer exist). For **ADD** operations, copy the latest version's `examples/` into a new `examples/<newVersion>/` folder and update `api-version` in each file (keep the old folder).
2. Update `readme.md` (and any `tspconfig`/service metadata) to reference the new version.

> These steps apply to both ARM and data-plane services. The same versioning decorators apply regardless of service type.

### Case 4 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.
