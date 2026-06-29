# Phase 6 — Implement a single stage

You are implementing **one stage** of the plan in a fresh session. You **may edit source code and
run shell commands** for this stage only. Earlier stages already ran; their code is on disk.

## Task
{{task}}

## This stage
```yaml
{{stage}}
```

## Context pack (clean ≠ blank — everything you need has been gathered for you)
- **`plan.md`** is the source of truth (read it, including the running "Plan changes" log).
- **Prior handoffs** from earlier stages:
{{handoff}}
- **Cumulative diff so far** (files already changed in this run):
{{cumulativeDiff}}
- **`context_needed`** files this stage depends on: {{contextNeeded}}

## What to do
1. Implement **this stage's steps**, editing the real source files. `expected_files` is the
   anticipated scope — **advisory, not a wall**. You may edit beyond it when correctness requires
   it, **provided you document the deviation** (see below).
2. **Run this stage's `gate.commands`** yourself (shell tool) and confirm they meet `expected`
   (e.g. exit code 0). If a gate fails, fix the code and re-run until it passes or you are
   genuinely blocked.
3. Append to **`execution-log.md`** (via `write_artifact`, appending) — for **every** action:
   (a) **justify the scope** (why it was necessary) and (b) **map it to a concrete step** in
   `plan.md` by stage/step id. Record each gate command, its exit code, and pass/fail.
4. Append a concise entry to **`handoff.md`** (via `write_artifact`, appending) for the *next*
   stage: what you built, the **new/changed public symbols and files**, decisions/conventions
   established, anything deferred, and known follow-ups.

## Deviation policy (documented, not blocked)
Deviating from the plan is **allowed** when it is the correct response to a gap. The requirement
is transparency:
- Append a **"Plan changes"** entry to `plan.md` describing what changed and why.
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
