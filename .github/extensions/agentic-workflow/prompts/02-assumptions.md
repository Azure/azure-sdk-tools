# Phase 2 — Assumptions (read-only)

You are in the **assumptions** phase. Surface every assumption I might be making about
this work, the codebase, and the constraints. Do not propose a plan, do
not propose code changes, and do not classify the work yet.

## Task
{{task}}

## Run directory
Your workflow run directory is `{{runDir}}`. Read prior artifacts from there and write your output
there using your **normal file tools**. Artifact paths below are relative to the run directory.

## Inputs
- `specs/architecture.md`, `specs/functional.md`, and `specs/apispec.md` if present (read them from
  the run directory).

## Output — write under the run directory with your normal file tools
`assumptions.md` — one assumption per line, each with a short **rationale** and a **confidence**
(`high` / `medium` / `low`).

Cover at least the following categories:

1. Assumptions about the work item
   - What the work item is asking for.
   - What it is not asking for.
   - Acceptance criteria, whether stated or inferred.
   - Priority, deadline, and known stakeholders.

2. Assumptions about the codebase
   - Which areas, modules, or services are likely affected.
   - Which areas are out of scope.
   - Existing patterns, conventions, and idioms in the relevant area.

3. Assumptions about behavior
   - What the current behavior is.
   - What the expected behavior is after the work is complete.
   - What behavior must remain unchanged.

4. Assumptions about constraints
   - Build, test, and deployment constraints.
   - Performance, reliability, security, and compliance constraints.
   - Compatibility constraints with consumers, callers, or other services.

5. Assumptions about validation
   - How I will know the work is done.
   - What evidence I will need to show my reviewer.
   - What tests already exist that protect the affected area.

6. Open questions and unknowns
   - Things I cannot determine from the work item or codebase alone.
   - Things that require a decision from me or my manager.

Scope discipline:
- Every assumption you list must be tied to this specific work item. Do
  not list assumptions about unrelated parts of the codebase, even if
  you notice issues there.
- This is a large, older codebase. You will see refactor opportunities,
  dead code, outdated patterns, and other smells that have nothing to
  do with this work item. Do not weave them into the assumptions.
  Capture them in a separate "Out-of-scope observations" section at the
  end of assumptions.md so they are not lost, but keep them clearly
  marked as out of scope.
- For each assumption, include a one-line justification that ties it to
  the work item, an acceptance criterion, or a specific area the work
  item touches. If you cannot justify it against the work item, do not
  include it.

Use clear
headings, short bullet points, and call out each assumption explicitly.
For open questions, list them at the end so I can resolve them before
moving on.

Keep the language simple and
straightforward.

### Blocking clarifications
If any assumption is **low-confidence AND affects correctness, security, or API behavior**, mark
it explicitly with a leading `blocking: true` token on that line, e.g.:

```
- blocking: true | We assume tokens are validated upstream | rationale: no validation found in scope | confidence: low
```

When any `blocking: true` assumption exists, do **not** invent an answer: report `needs_input`
(see below) with the blocking question so the human can resolve it. Use this sparingly and only
when it genuinely gates correctness.

## Constraints
- Read-only; no source edits, no shell.
- Be concrete and tied to the specs/codebase, not generic.
- End your turn once `assumptions.md` is written.

## Report at the end of your turn
End with exactly one status line the runner reads:
- `PHASE_RESULT: pass` if `assumptions.md` is written with no blocking clarification,
- `PHASE_RESULT: needs_input — <question>` if a `blocking: true` assumption needs a human decision,
- `PHASE_RESULT: fail — <reason>` if you could not produce the artifact.

{{priorErrors}}
