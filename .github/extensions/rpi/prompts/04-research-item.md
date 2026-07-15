# Phase 4 — Research the sub-items (read-only)

You are in the **research-item** phase. Do deep, isolated research on **each** sub-item produced by
the classify phase, using the specs as context. You are **read-only**.

## Original task
{{task}}

## Run directory
Your workflow run directory is `{{runDir}}`. Read prior artifacts from there and write your output
there using your **normal file tools**. Artifact paths below are relative to the run directory.

## Inputs
- `subitems.json` (read it from the run directory) — the list of sub-items to research.
- `specs/*` and `assumptions.md` if present.

## Work
Read `subitems.json`. For **every** item in `items`, produce a focused research note. Process items
in dependency order (an item's `dependsOn` siblings first) so each note can reference the prior
notes it depends on.

## Output — write one note per sub-item under the run directory
For each item with id `<id>`, write `research/<id>.md`: the exact files and symbols it will touch,
the current behavior in that area, constraints, edge cases, and any sequencing concerns relative to
sibling items. Cite real code locations. Keep each note scoped to **its** sub-item only.

## Constraints
- Cover every item in `subitems.json` — do not skip any.
- Read-only; no source edits, no shell.
- End your turn once all notes are written.

## Self-check
Confirm there is one `research/<id>.md` for every `id` in `subitems.json`. Fix gaps before
reporting.

## Report at the end of your turn
End with exactly one status line the runner reads:
- `PHASE_RESULT: pass` if a note exists for every sub-item,
- `PHASE_RESULT: fail — <reason>` if you could not complete every note,
- `PHASE_RESULT: needs_input — <question>` if research is blocked on a decision.

{{priorErrors}}
