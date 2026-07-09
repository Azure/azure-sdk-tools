# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 Choose the plan source

Intake (Step 2) has already run **agentic search** over the catalog URLs. Now decide, based on what that search actually surfaced, how to build the plan:

- **Agentic search returned relevant guidance** that covers the request → build the plan from it (Step 3.2). This is the normal path.
- **Agentic search did not surface relevant guidance** (the request is not covered by the curated catalog, so the fetched docs don't address it) → build the plan via the **KB fallback** (Step 3.3).

Use exactly one source. Never build a plan from prior/internal knowledge without one of these two grounding sources.

## 3.2 Agentic search path (relevant guidance found)

Build the authoring plan from the agentic-search results gathered in Step 2 (Intake) — the fetched content of the URLs selected from [reference-document-links.md](reference-document-links.md):

1. Confirm agentic search was run and fetched **every** selected URL. If it was not, run [agentic search](agentic-search.md) now with:
   - **URLs**: the URLs from Step 2.1.
   - **Query**: derived from the user's request plus Step 1 analysis output (service type, version, target resource/operation, intent).
2. Synthesize the **fetched** guidance into a concrete, ordered authoring plan with explicit file/edit-level actions. Every plan step must be grounded in fetched documentation, not assumptions.
3. Record each cited URL alongside the plan step it informs — these will be emitted verbatim in Step 6 (Output Reference Links).

## 3.3 KB fallback path (no relevant guidance found)

When the agentic search from Step 2 did **not** surface guidance relevant to the request (the curated catalog does not cover it), fall back to the knowledge base. Call the `azsdk_typespec_generate_authoring_plan` MCP tool with:

- `request`: the user's request (verbatim).
- `additionalInformation`: all context gathered in Steps 1–2 (project analysis output, service type, versions, intent, target, constraints, and any case-specific intake answers).
- `typeSpecProjectRootPath`: the TypeSpec project root from Step 1.

Use the plan returned by the tool as the authoring plan. Record the sources it cites (if any) for Step 6 (Output Reference Links).

> Prefer the curated catalog whenever agentic search covers the request — only use this KB fallback when it does not.

---

## 3.4 Case-Specific Notes

### Case 3 — API Versioning

In addition to the agentic-search plan above:

**Scenario 3a — Add / bump / promote a version:**

1. Copy `.json` files from the latest version's `examples/` into the new version's `examples/`. Update `api-version` in each file. Delete an old version's example folder if that version is no longer in the `Versions` enum.
2. Update `readme.md`.

**Scenario 3b — Version-scoped change:**

1. Scope the change to the target version(s) with versioning decorators (`@added`, `@removed`, `@renamedFrom`, `@madeOptional`, `@madeRequired`, `@typeChangedFrom`), preferably via `@@`-augment statements, so it does not silently apply to all versions.
2. Do not add or delete version example folders; only update example `.json` files for affected versions if the change alters the request/response shape.
