# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

> **MANDATE — read before doing anything in Step 3:** When the request's case is covered by [reference-document-links.md](reference-document-links.md) (this includes **all API version evolution**, cases 001004–001008 and similar), you **MUST** call `web_fetch` on the matching reference URLs and build the plan from the fetched content **before** writing or applying any change. **Never** author the plan from memory, and **do not** call the MCP tool `azsdk_typespec_generate_authoring_plan` for a covered case.

## 3.1 General (All Cases)

Choose the grounding source based on whether the request's case is covered by [reference-document-links.md](reference-document-links.md):

1. **Case found in the reference doc → Agentic Search.** Run [agentic search](agentic-search.md) — you **MUST** call `web_fetch` on the matching URLs and follow their steps. Synthesize the extracted content into a concrete plan. Do **not** call the MCP tool.

2. **Case not found in the reference doc → MCP Tool.** Call `azsdk_typespec_generate_authoring_plan` and build the plan from its result:
   - `request`: user request (verbatim)
   - `additionalInformation`: all context from Steps 1–2
   - `typeSpecProjectRootPath`: project root path

---

## 3.2 Case-Specific Authoring Plan

### Case 1 — Add Resource Type (ARM)

Use the documented ARM resource/operation templates (see [ARM resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/); for extension resources see the [extension resource sample](https://azure.github.io/typespec-azure/docs/samples/resource-manager/resource-types/specific-extension/)). Key templates:

- **Resource model:** top-level → `model X is TrackedResource<XProperties>`; child → `model X is ProxyResource<XProperties>` with `@parentResource(Parent)`; extension → `model X is ExtensionResource<XProperties>`. Add `...ResourceNameParameter<X>`.
- **Read-only props:** `@visibility(Lifecycle.Read) provisioningState?: ProvisioningState;`.
- **Standard operations (tracked/proxy/child):** `ArmResourceRead<X>`, `ArmResourceCreateOrReplaceAsync<X>`, `ArmCustomPatchSync<X, XProperties>`, `ArmResourceDeleteWithoutOkAsync<X>` (or `ArmResourceDeleteSync<X>`), `ArmResourceListByParent<X>`. Top-level tracked also add `ArmListBySubscription<X>`.
- **Extension resource operations:** define them with the `Extension.*` templates — `Extension.Read`, `Extension.CreateOrReplaceAsync`, `Extension.CustomPatchSync`, `Extension.DeleteWithoutOkAsync`, `Extension.ListByTarget` (do **not** use the `ArmResource*` templates for an extension resource).

### Case 2 — Add Resource Operations (ARM)

Use the documented ARM operation templates (see [ARM resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/)). Key templates:

- **CRUDL:** GET `ArmResourceRead<R>`; PUT `ArmResourceCreateOrReplaceAsync<R>`; PATCH `ArmCustomPatchSync<R, PatchModel>` (recommended over tags/lifecycle patch); DELETE `ArmResourceDeleteWithoutOkAsync<R>`; list `ArmResourceListByParent<R>` / `ArmListBySubscription<R>`; check-existence `ArmResourceCheckExistence<R>`.
- **Custom action (POST):** `action is ArmResourceActionSync<R, Request, Response>` (async: `ArmResourceActionAsync`; no response body: `ArmResourceActionNoContentSync<R, Request>`).
- **Add `$top`/`$skip` (and similar) query params to a list:** spread the standard parameter models into the template — e.g. `listBySubscription is ArmListBySubscription<R, { ...Azure.Core.TopQueryParameter; ...Azure.Core.SkipQueryParameter; }>`. Do **not** hand-roll `@query("$top")` / `@query("$skip")` parameters.
- On an **extension** resource, use the `Extension.*` operation templates instead of `ArmResource*`.

### Case 3 — API Version Evolution (ARM / Data-plane)

> Version evolution **is covered** by [reference-document-links.md](reference-document-links.md), so use **Agentic Search** (per [3.1 General](#31-general-all-cases)) — you **MUST** call `web_fetch` on the matching versioning docs and follow their steps. Do **not** call the MCP tool `azsdk_typespec_generate_authoring_plan` for this case. The concrete file-migration steps below are **mandatory**.

1. Copy `.json` files from latest version's `examples/` into new version's `examples/`. Update `api-version` in each file. Delete old version's example folder if old version is no longer existed.
2. Update `readme.md`.

> These steps apply to both ARM and data-plane services. The same versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@typeChangedFrom`) apply regardless of service type.

### Case 4 — Add Data-Plane Operations

Key guidance for data-plane:

1. Use `Azure.Core` resource operation templates (see [intake.md](intake.md) Case 4 for the template table).
2. Define operations inside an `interface` block.
3. Add `/** */` documentation to all operations.
4. Data-plane services use `@azure-tools/typespec-azure-core`, not `@azure-tools/typespec-azure-resource-manager`.
