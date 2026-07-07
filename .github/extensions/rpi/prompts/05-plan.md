# Phase 5 — Plan (read-only)

You are in the **plan** phase. Produce a highly detailed, structured implementation plan. This is
the highest-leverage artifact in the run. You are **read-only** — you plan, you do not implement.

## Task
{{task}}

## Run directory
Your workflow run directory is `{{runDir}}`. Read prior artifacts from there and write your output
there using your **normal file tools**. Artifact paths below are relative to the run directory.

## Inputs
- `specs/*`, `assumptions.md`, and all `research/*.md` notes that exist (read from the run dir).

## Output — write `plan.md` under the run directory with your normal file tools
`plan.md`, containing these sections **in order**:

0. **Research reconciliation** — reconcile the independent phase-4 notes: overlaps,
   contradictions, duplicated work, and the single coherent strategy chosen. (If there is only
   one sub-item, say so; do not invent overlaps.)
1. **Decisions and rationale** — the choices made and why.
2. **End-to-end approach** — the overall strategy, and explicitly how the success criterion will
   be *proved*.
3. **Step-by-step implementation plan** — ordered, concrete, file-by-file steps, grouped into
   **stages**. Define each stage **in prose** with:
   - a stage id and short title,
   - the ordered steps (each with a step id and description),
   - the anticipated files/scope (advisory),
   - existing files the stage depends on for context,
   - a **gate**: one or more concrete shell commands the implement phase will run to verify the
     stage (e.g. `npm test -- foo`), and the expected result (typically exit code 0).
4. **Stop/go gates** — explicit points where work pauses for validation.
5. **Validation plan** — tests to run, tests to add, observability checks.
6. **Rollout strategy**.
7. **Rollback plan**.
8. **Risks and mitigations**.
9. **Definition of done** — concrete, checkable completion criteria.
10. **Open questions**.
11. **Out-of-scope observations**.
12. **Plan changes** — leave empty (`_none yet_`); phase 6 appends here when implementation
    deviates from the plan.

## Stage sizing
Split work into **cohesive, loosely-coupled** stages — each independently completable and
verifiable by its gate. Tightly-coupled changes that only make sense together belong in **one**
stage. Every stage MUST have a gate with at least one concrete command. You define and own the
stage breakdown in prose — there is no machine-readable block to emit and no external validator;
the implement phase reads your prose plan and runs the gate commands you specify.

## Constraints
- Read-only; no source edits, no shell.
- Every stage has an id and at least one gate command.
- End your turn once `plan.md` is written.

## Self-check
Confirm `plan.md` exists with all sections 0–12 present, at least one stage defined, and every
stage carries an explicit gate command. Fix gaps before reporting.

## Report at the end of your turn
End with exactly one status line the runner reads:
- `PHASE_RESULT: pass` if `plan.md` is complete and self-check passed,
- `PHASE_RESULT: fail — <reason>` otherwise,
- `PHASE_RESULT: needs_input — <question>` if planning is blocked on a decision.

{{priorErrors}}
