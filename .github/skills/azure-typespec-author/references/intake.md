# Intake

> Prerequisite: Step 1 (Analyze Project) must be complete.

## 2.1 General Intake (All Cases)

1. **Get links** — match the request to one or more cases in the full catalog [reference-document-links.md](reference-document-links.md). Every request **must** map to at least one case — pick the best-matching case(s) and select the document URLs relevant to the request.
   - A request may map to multiple cases (e.g. version-scoped enum change → API Versioning **and** Models and Enums); select docs from every match.
2. **Run agentic search** — run [agentic search](agentic-search.md) with the URLs from step 1, the Step 1 analysis result, and the request to collect information for the Step 3 plan. Agentic search **must** be run and **must** fetch the selected URLs — never skip it or rely on prior knowledge.
3. **Case-specific intake** — only the cases in the table below need extra questions (Step 2.2). For any other case, no extra intake is needed.

| Case | Name                    | Description                                                                  | Service Type     |
| ---- | ----------------------- | --------------------------------------------------------------------------- | ---------------- |
| 1    | Add Resource Type       | Define a new ARM resource with operations                                   | ARM              |
| 2    | Add Resource Operations | Add CRUD or custom actions on an existing resource                          | ARM              |
| 3    | API Versioning          | Add/bump/promote a version, or add/update code scoped to a specific version | ARM / Data-plane |

> Only cases 1–3 need extra intake. All other cases (4 = LRO, 5 = Paging, 6 = Models and Enums, 7 = Decorators, 8 = Warnings) are still selectable for document links from [reference-document-links.md](reference-document-links.md) with no extra intake.

---

## 2.2 Case-Specific Intake

### Case 1 — Add Resource Type (ARM)

Collect: target API version, resource name (PascalCase), hierarchy (top-level or nested + parent), properties (name, type, required/optional).

Defaults: top-level → `TrackedResource`, child → `ProxyResource`. Operations: `createOrReplace` (PUT/async), `get`, `update/patch`, `delete` (async), list by parent. Top-level adds list by subscription.

> Use `createOrReplace` (not `createOrUpdate`). Use `ArmCustomPatch` for PATCH.
> Top-level tracked resources MUST have `listByResourceGroup` and `listBySubscription`.

### Case 2 — Add Resource Operations (ARM)

Collect: target resource, operation type (CRUD or custom), operation name (custom actions), request/response models (custom actions).

Defaults: never async → GET, LIST, HEAD. Default async → PUT, DELETE. Default sync → PATCH. Always ask user → POST/action.

> Use `createOrReplace` (not `createOrUpdate`). Use `ArmCustomPatch` for PATCH.
> For async POST, use ARM combined headers: `LroHeaders = ArmCombinedLroHeaders<FinalResult = ExportResult>`.

### Case 3 — API Versioning (ARM / Data-plane)

This case covers two scenarios. First decide which applies:

- **3a — Add / bump / promote a version**: introduce a new API version (preview or stable) and carry features forward.
- **3b — Version-scoped change**: add or update code (resource, operation, model, property, enum member, default, optionality) that must apply to a **specific existing version** rather than all versions — done with versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@madeOptional`, `@madeRequired`, `@typeChangedFrom`), typically via `@@`-augment statements.

#### Scenario 3a — Add / bump / promote a version

Collect from user:

1. **Target version** (e.g. `2026-01-01-preview` or `2026-01-01`)
2. **Features to exclude, Do not assume the user wants to carry over all features** — follow this procedure exactly:
   1. Read the latest version's `.tsp` files and enumerate all resources, operations, models, and properties.
   2. Present the list to the user as a numbered checklist.
   3. Ask: _"All features will be carried over to the new version. Are there any you want to exclude? List by number, or say 'none'."_
   4. Wait for the user's response before proceeding.

## 2.3 Confirm

Display and wait for user confirmation:

```
Case:           [Name]
Target Version: [version]
Changes:        [summary]
Defaults:       [applied defaults]
```
