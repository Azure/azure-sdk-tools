# Phase 6 — Implement a single stage

You are in **IMPLEMENT mode**, implementing **one stage** of the approved plan in a fresh session.
You **may edit source code and run shell commands** for this stage only. Earlier stages already
ran; their code is on disk. Implement the approved plan **exactly as written**. Keep the language
in your logs simple. No emojis.

## Task
{{task}}

## This stage
```yaml
{{stage}}
```

## Context pack (clean ≠ blank — everything you need has been gathered for you)
- **`plan.md`** is the source of truth (read it, including the running "Plan changes" log).
- **`assumptions.md`** is your context document — read it before you start.
- **Prior handoffs** from earlier stages:
{{handoff}}
- **Cumulative diff so far** (files already changed in this run):
{{cumulativeDiff}}
- **`context_needed`** files this stage depends on: {{contextNeeded}}

## What to do
1. Implement **this stage's steps**, editing the real source files. Follow the plan step-by-step.
   `expected_files` is the anticipated scope — **advisory, not a wall**. You may edit beyond it
   when correctness requires it, **provided you document the deviation** (see below).
2. Make **small, reviewable changes.** Group related edits, and for each group summarize *what*
   changed, *which plan step* it satisfies, and *what you verified*.
3. **Run this stage's `gate.commands`** yourself (shell tool) and confirm they meet `expected`
   (e.g. exit code 0). If a gate fails, fix the code and re-run until it passes or you are
   genuinely blocked.
4. Append to **`execution-log.md`** (via `write_artifact`, appending) — see "Execution log" below.
5. Append a concise entry to **`handoff.md`** (via `write_artifact`, appending) for the *next*
   stage: what you built, the **new/changed public symbols and files**, decisions/conventions
   established, anything deferred, and known follow-ups.

## Scope discipline (stay strictly within the plan)
This may be a large, older codebase. You **will** see refactor opportunities, outdated patterns,
dead code, and smells in the files you touch. **Do not act on any of them.** If a fix is genuinely
required to make a plan step work, **STOP** and follow the deviation policy below instead of doing
it silently.
- **Observability:** add or update observability **exactly as the plan defines** — no more.
- **Dependencies:** do **not** introduce dependencies beyond what the plan and your team's package
  policy allow. Use only feeds/registries your team approves.
- **Tests:** do **not** write tests in this step unless a plan step explicitly calls for it.
  (Running the stage's existing `gate.commands` is required and is not the same as authoring tests.)
- **Secrets/PII:** never include secrets or PII in code, logs, or artifacts.
- **Commits:** do **not** run `git commit`. Stage your changes and let the build be verified first.
- Every change must tie to a specific plan step. If a change **cannot** be tied to a plan step,
  **do not make it** — record it under "Out-of-scope observations" in the execution log instead.

## Execution log (`execution-log.md`)
For **every** change group, record:
- a **one-line scope justification** naming the concrete `plan.md` step (by stage/step id) it
  satisfies — *(a)* why it was necessary and *(b)* what it maps to;
- each **gate command**, its exit code, and pass/fail.
Also maintain these sections in the log:
- a **Test results** section (placeholder, to be filled later if tests are out of scope here);
- a **Plan changes** section (mirrors any deviation entries you add to `plan.md`);
- a **Verification evidence** section (gate output; placeholder where evidence comes later);
- an **Out-of-scope observations** section at the bottom for anything you noticed but correctly
  did *not* act on;
- a **PR description draft**: reference the work item / task id and summarize **scope, changes,
  rollout, rollback, and monitoring signals**.

## Deviation policy (documented, not blocked)
Deviating from the plan is **allowed** when it is the correct response to a gap. If the plan is
incomplete or incorrect: **(1) STOP. (2)** append a **"Plan changes"** entry to `plan.md`
describing the deviation and rationale. **(3)** continue only after that update is recorded.
- The `execution-log.md` entry for the deviating action justifies its scope and maps it to the
  originating step / plan-change entry.
- **Larger deviations** — a change to architecture, public API, or overall test strategy — must
  be called out prominently at the top of your handoff so the orchestrator/human can review;
  if the divergence invalidates the plan, say so explicitly.
- Silent, undocumented scope expansion is **not** allowed.

## Report at the end of your turn
End with a short, explicit status line the orchestrator reads:
- `STAGE_RESULT: pass` if every gate command met `expected`, or
- `STAGE_RESULT: fail — <reason>` otherwise.

{{priorErrors}}
