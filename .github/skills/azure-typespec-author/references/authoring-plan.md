# Build Authoring Plan

> Prerequisite: Steps 1 (Analyze Project) and 2 (Intake) must be complete.

## 3.1 Choose the plan source

Step 2.1 (Intake) already decided which source applies, based on whether the request matched relevant documents in [reference-document-links.md](reference-document-links.md):

- **Relevant documents found** → build the plan via **agentic search** (Step 3.2).
- **No relevant documents found** → build the plan via the **KB fallback** (Step 3.3).

Use exactly one source. Never build a plan from prior/internal knowledge without one of these two grounding sources.

## 3.2 Agentic search path (relevant documents found)

Build the authoring plan using **agentic search**, based on the URLs already selected in Step 2 (Intake). You **must** run agentic search and fetch those URLs before writing the plan — do not produce a plan from prior knowledge or without fetching:

1. Take the documentation URLs gathered in Step 2.1 (the links from the matched case(s) in [reference-document-links.md](reference-document-links.md)).
2. Run [agentic search](agentic-search.md) with:
   - **URLs**: the URLs from Step 2.1.
   - **Query**: derived from the user's request plus Step 1 analysis output (service type, version, target resource/operation, intent).

   Agentic search **must** be run and fetch **every** one of those URLs. Do not proceed to step 3 until you have fetched content for all of them.
3. Synthesize the **fetched** guidance into a concrete, ordered authoring plan with explicit file/edit-level actions. Every plan step must be grounded in fetched documentation, not assumptions.
4. Record each cited URL alongside the plan step it informs — these will be emitted verbatim in Step 6 (Output Reference Links).

## 3.3 KB fallback path (no relevant documents found)

When Step 2.1 found **no** relevant documents in [reference-document-links.md](reference-document-links.md), fall back to the knowledge base instead of agentic search. Call the `azsdk_typespec_generate_authoring_plan` MCP tool with:

- `request`: the user's request (verbatim).
- `additionalInformation`: all context gathered in Steps 1–2 (project analysis output, service type, versions, intent, target, constraints, and any case-specific intake answers).
- `typeSpecProjectRootPath`: the TypeSpec project root from Step 1.

Use the plan returned by the tool as the authoring plan. Record the sources it cites (if any) for Step 6 (Output Reference Links).

> Prefer the curated catalog whenever it covers the request — only use this KB fallback when Step 2.1 found no relevant documents.

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
