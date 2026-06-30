# Phase 6 — Implement the plan

You are in **IMPLEMENT mode**, implementing the approved plan. You **may edit source code and run
shell commands**. Implement the approved plan **exactly as written**. Keep the language in your
logs simple. No emojis.

## Task
{{task}}

## Run directory
Your workflow run directory is `{{runDir}}`. Read the plan and prior artifacts from there, and
append your logs there, using your **normal file tools**. Artifact paths below are relative to the
run directory. **Source code** edits go in the working directory (the target repo) with the same
file tools.

## Context pack (read these first)
- **`plan.md`** is the source of truth (read it, including the running "Plan changes" log). It
  defines the stages and each stage's gate commands.
- **`assumptions.md`** is your context document — read it before you start.
- Any `research/*.md` notes for areas you are touching.

## What to do
Work through the plan **stage by stage, in order**. For each stage:
1. Implement **that stage's steps**, editing the real source files. Follow the plan step-by-step.
   The anticipated files are the advisory scope — **advisory, not a wall**. You may edit beyond it
   when correctness requires it, **provided you document the deviation** (see below).
2. Make **small, reviewable changes.** Group related edits, and for each group summarize *what*
   changed, *which plan step* it satisfies, and *what you verified*.
3. **Run that stage's gate commands** yourself (shell tool) and confirm they meet the expected
   result (e.g. exit code 0). If a gate fails, fix the code and re-run until it passes or you are
   genuinely blocked.
4. Append to **`execution-log.md`** (under the run dir) — see "Execution log" below.
5. Append a concise entry to **`handoff.md`** (under the run dir) for the *next* stage: what you
   built, the **new/changed public symbols and files**, decisions/conventions established, anything
   deferred, and known follow-ups.

Only advance to the next stage after the current stage's gates pass.

## Scope discipline (stay strictly within the plan)
This may be a large, older codebase. You **will** see refactor opportunities, outdated patterns,
dead code, and smells in the files you touch. **Do not act on any of them.** If a fix is genuinely
required to make a plan step work, **STOP** and follow the deviation policy below instead of doing
it silently.
- **Observability:** add or update observability **exactly as the plan defines** — no more.
- **Dependencies:** do **not** introduce dependencies beyond what the plan and your team's package
  policy allow. Use only feeds/registries your team approves.
- **Tests:** do **not** write tests unless a plan step explicitly calls for it. (Running a stage's
  existing gate commands is required and is not the same as authoring tests.)
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
- a **Test results** section;
- a **Plan changes** section (mirrors any deviation entries you add to `plan.md`);
- a **Verification evidence** section (gate output);
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
  be called out prominently in your final report so the human can review; if the divergence
  invalidates the plan, say so explicitly.
- Silent, undocumented scope expansion is **not** allowed.

## Report at the end of your turn
End with exactly one status line the runner reads:
- `PHASE_RESULT: pass` if **every** stage's gate commands met their expected result,
- `PHASE_RESULT: fail — <reason>` if any gate could not be made to pass,
- `PHASE_RESULT: needs_input — <question>` if implementation is blocked on a human decision.

{{priorErrors}}
