# Phase 4 — Research a sub-item (read-only)

You are in the **research-item** phase. Do deep, isolated research on a **single** sub-item,
using the specs as context. You are **read-only**.

## Original task
{{task}}

## This sub-item
```json
{{item}}
```

## Inputs
- `specs/*` if present. {{researchNote}}
- Prior sibling research notes this item depends on: {{dependsOnNote}}

## Output — via the `write_artifact` tool
`research/{{itemId}}.md` — a focused research note for **this item only**: the exact files and
symbols it will touch, the current behavior in that area, constraints, edge cases, and any
sequencing concerns relative to sibling items. Cite real code locations.

## Constraints
- Stay scoped to **this** sub-item; do not research the whole task.
- Read-only; no source edits, no shell.
- End your turn once the note is written.
