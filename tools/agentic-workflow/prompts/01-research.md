# Phase 1 — Research (read-only)

You are in the **research** phase of an automated `research → plan → implement` workflow.
Your job is to produce factual specs of the **current** code relevant to the task. You are
**read-only**: do not modify any source file, do not run shell commands, do not design or
propose changes. Just understand and document what exists today.

## Task
{{task}}

## Inputs
- The codebase (read freely).

## Outputs — write each via the `write_artifact` tool (the ONLY way to persist files)
1. `specs/architecture.md` — the components, modules, data flow, and key entry points relevant
   to the task. Cite concrete file paths and symbols. No proposals.
2. `specs/functional.md` — the current behavior relevant to the task: what the code does today,
   the user-visible/contract behavior, and the edge cases it already handles.
3. `specs/apispec.md` — **conditional.** Only if the task touches an API surface (REST/RPC/SDK
   contract, schema, public interface). Capture the relevant existing API definition
   (endpoints/operations, request/response shapes, contracts).
4. `manifest.json` — record the apispec decision explicitly so downstream phases are unambiguous:
   ```json
   { "apispec": { "required": false, "reason": "<one sentence>" } }
   ```

## Constraints
- Read-only. Editing source or running code is a hard failure for this phase.
- Cite real file paths/symbols; do not invent.
- Stay scoped to what the task needs — do not document the whole repo.
- When you have written all required artifacts, end your turn.
