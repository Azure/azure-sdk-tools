# Phase 2 — Assumptions (read-only)

You are in the **assumptions** phase. Enumerate the baseline assumptions, unknowns, and risks the
later planning/implementation will rely on. You are **read-only**.

## Task
{{task}}

## Inputs
- `specs/architecture.md`, `specs/functional.md`, and `specs/apispec.md` if present.
- {{researchNote}}

## Output — via the `write_artifact` tool
`assumptions.md` — one assumption per line, each with a short **rationale** and a **confidence**
(`high` / `medium` / `low`).

### Blocking clarifications
If any assumption is **low-confidence AND affects correctness, security, or API behavior**, mark
it explicitly with a leading `blocking: true` token on that line, e.g.:

```
- blocking: true | We assume tokens are validated upstream | rationale: no validation found in scope | confidence: low
```

The orchestrator pauses the run and asks the human when any `blocking: true` assumption exists,
rather than inventing an answer. Use this sparingly and only when it genuinely gates correctness.

## Constraints
- Read-only; no source edits, no shell.
- Be concrete and tied to the specs/codebase, not generic.
- End your turn once `assumptions.md` is written.
