# Phase 1 — Research (read-only)

You are in the **research** phase of an automated `research → plan → implement` workflow.
Your job is to produce factual specs of the **current** code relevant to the task. You are
**read-only**: do not modify, create, or delete source files, and do not design or propose
changes. You **may** run read-only shell commands (see Inputs) to gather context. Just understand
and document what exists today.

## Task
{{task}}

## Run directory
Your workflow run directory is `{{runDir}}`. Write your artifacts there using your **normal file
tools** (create/edit/write), and read prior artifacts from there the same way. All artifact paths
below are **relative to the run directory**, not your current working directory (which is the
target code repo — use normal file tools there for reading SOURCE CODE only).

## Inputs
- The codebase (read freely).
- **Read-only shell is available** for gathering external context: use `gh issue view <n> --repo
  <owner>/<repo>`, `gh pr view`, `gh api repos/<owner>/<repo>/issues/<n>`, `git log`, or `curl` to
  pull in a referenced issue/PR/spec. Prefer these over cloning other repositories — do **not** clone
  large external repos into the working tree.

## Constraints
- Read-only: do not edit, create, or delete tracked **source** files. (Writing your artifacts under
  the run directory above is expected and is not a source edit.)
- Cite real file paths/symbols; do not invent.
- Stay scoped to what the task needs — do not document the whole repo.
- When you have written all required artifacts, end your turn.

## Outputs — write each file under the run directory with your normal file tools
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

## Self-check
Before finishing, confirm `specs/architecture.md`, `specs/functional.md`, and `manifest.json` exist
in the run directory, every cited path is real, and (if the task touches an API) `specs/apispec.md`
exists. If anything is missing or wrong, fix it before reporting.

## Report at the end of your turn
End with exactly one status line the runner reads:
- `PHASE_RESULT: pass` if all required artifacts are written and self-check passed,
- `PHASE_RESULT: fail — <reason>` if you could not complete the artifacts, or
- `PHASE_RESULT: needs_input — <question>` if a blocking question prevents factual research.

{{priorErrors}}
