# Plan Critique

The plan is strong on milestone gating and explicit success proof, but several guarantees are still under-specified enough that implementation could drift or become unsafe.

## High-priority gaps

1. **Generated gate commands are the biggest unresolved safety risk.**
   The plan notes this in open questions, but it should be resolved before M5, not "before M5.6". A model-generated `plan.md` can currently cause the orchestrator to run arbitrary shell. Define an allowlist, cwd, env, timeout, output limits, interactive-command blocking, and approval semantics before implementing `parsePlanStages`.

2. **Audit/log ownership is too trusting.**
   Phase 6 agents append `execution-log.md`, `handoff.md`, and plan-change entries themselves. That lets the same actor that made an edit also self-report why it was safe. Prefer an orchestrator-owned structured log, e.g. JSONL, where the agent can propose entries but the orchestrator records actual diffs, gates, exit codes, and file changes.

3. **`manifest.json` ownership is ambiguous.**
   M1 makes `manifest.json` part of orchestrator state, but M3 says prompt `01` records decisions in `manifest.json`. Agents should not directly write core state. Better: agents emit decision artifacts; orchestrator validates and updates `manifest.json`.

4. **The git-diff guard needs a precise definition.**
   "`git diff` and untracked files empty" is not enough. `git diff` misses untracked files; ignored files may be hidden; `.agentic-workflow/` is intentionally ignored. Define the exact command/algorithm, exclude only the artifact root, include untracked source files, and compare against a pre-phase snapshot.

5. **Redaction arrives too late.**
   M2 streams events into `execution-log.md`, but redaction is deferred to M6. If SDK output, tool args, shell output, or env leaks appear in logs before M6, the damage is already done. Build log redaction into M2 logging from the start, then harden in M6.

6. **The read-only checkout / overlay fallback is design-critical but underspecified.**
   If native tool scoping is unavailable, the fallback must define worktree creation, artifact handoff, patch transfer, cleanup, symlink behavior, nested repos, ignored files, and how implementation diffs are applied back. This should become a concrete M0 output, not just a yes/no branch.

## Ambiguities to tighten

- **Exit codes conflict:** failing gates are described as `10`/`1` in multiple places, while M7 says `10 = paused`, `1 = failure`. Decide whether a failed gate is pause-for-human or hard failure.
- **Plan machine block format:** `stages:` / `gate:` needs exact delimiters and syntax, likely a fenced YAML or JSON block with schema versioning.
- **Approval model:** MVP says mandatory approval before implement, M7 adds `approve`, `resume`, `--yes`, and gate-set overrides. Define where approval is stored and what exactly resumes after approval.
- **Fresh context proof:** planting a fact in session A and asking session B is useful but weak. Use a nonce that is never written to artifacts/repo and define what isolation means relative to SDK memory, external tools, and local files.
- **Resume invalidation:** if an upstream artifact changes, the plan says downstream reads the edited content, but it does not define which completed phases become stale or must re-run.
- **Phase-4 dependencies:** `dependsOn` controls ordering, but it is unclear whether dependent item research receives prior item research as input.
- **Budget ceiling:** cost/quota is listed as open, but fan-out starts in M4. A hard budget should exist before parallel execution.

## Opportunities

- Add **schema versions** to every machine artifact: `state.json`, `manifest.json`, `subitems.json`, plan block, handoff, and execution log.
- Use a **typed schema source of truth** such as Zod/TypeBox plus generated JSON Schema to avoid TS types and JSON schemas drifting.
- Make **prompt-template regression tests** part of M3/M8, not future work. Even lightweight golden-structure tests would protect the core IP.
- Add a **repo/branch-level lock or dirty-worktree preflight** before implementation. Per-run locks do not prevent two runs from editing the same branch.
- Separate **mocked deterministic tests** from **real SDK smoke tests**. Gates should not depend on live quota unless explicitly marked opt-in.
- Consider making the MVP "review mode" the default. Fully unattended autopilot should require explicit opt-in once gate-command safety, budgets, and redaction are proven.
