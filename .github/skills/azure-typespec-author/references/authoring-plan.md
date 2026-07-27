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

> Version evolution **is covered** by [reference-document-links.md](reference-document-links.md), so use **Agentic Search** (per [3.1 General](#31-general-all-cases)) — you **MUST** call `web_fetch` on the matching versioning doc and follow its steps. Do **not** call the MCP tool `azsdk_typespec_generate_authoring_plan` for this case. The concrete steps below are **mandatory**.

**3.2.C3.a — Select the matching sub-case** from the Step 1 result (status of the latest *existing* version vs. the requested *new* version) and `web_fetch` the corresponding entry in [reference-document-links.md](reference-document-links.md):

| Latest existing version | New version | Reference doc entry to `web_fetch`                         |
| ----------------------- | ----------- | --------------------------------------------------------- |
| preview                 | preview     | Add preview after preview (`02-preview-after-preview`)     |
| preview                 | stable      | Add stable after preview (`03-stable-after-preview`) — **promotion** |
| stable                  | preview     | Add preview after stable (`04-preview-after-stable`)       |
| stable                  | stable      | Add stable after stable (`05-stable-after-stable`)         |

**3.2.C3.b — Mandatory file migration (ALL sub-cases):**

1. Create the new version's `examples/<new-version>/` folder by copying every `.json` from the latest version's `examples/` and updating `api-version` in each copied file. **Do this even when the new version has no API-surface changes and needs no versioning decorators** (e.g. "add stable after stable, carry all features") — the new version still requires its own examples folder. Skip only for XML-based specs.
2. Update `readme.md` to add the new version (and its package tag).

**3.2.C3.c — Promotion (preview → stable) additional mandatory steps:**

When the latest existing version is **preview** and the new version is **stable**, the preview is being promoted/collapsed into the new stable. In addition to 3.2.C3.b:

1. **Remove the superseded preview version.** Delete the preview entry from the `Versions` enum and delete its `examples/<preview-version>/` folder.
2. **Rebase or reverse preview-scoped decorators.** Every decorator argument that referenced the removed preview version (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`, `@madeOptional`, `@madeRequired`, `@returnTypeChangedFrom`, …) must be **rebased to the new stable version** (for features carried into the stable) or reversed per the `03-stable-after-preview` guide. **No occurrence of the removed preview version identifier** (e.g. `v2024_10_01_preview`) may remain in any `.tsp` file, and the preview api-version string (e.g. `2024-10-01-preview`) must not remain in `main.tsp`.
3. **Apply excluded features.** Any feature the user chose to exclude must be fully removed from the promoted stable (a plain property/type, not left behind as decorator-bridged scaffolding).

> These steps apply to both ARM and data-plane services. The same versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`) apply regardless of service type.

### Case 4 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.
