# Thin-Spine Plan — Agentic `research → plan → implement`

> A deliberately minimal re-cut of `.plan.md`, driven by one constraint: **agent harnesses
> improve rapidly, so write as little bespoke machinery as possible.** Keep the durable value
> (the methodology) in portable plain-text assets, enforce only the guarantees the harness
> can't yet give for free, and lean on the Copilot extension/SDK surface for everything else.
> When the harness grows a capability, we **delete** code — we don't rewrite it.

---

## 0. The one idea

Split the system into two layers and treat them completely differently:

| Layer | What it is | Strategy |
| --- | --- | --- |
| **Durable core (the product)** | The 6 phase templates + 2 judge templates + the artifact/phase contract + the machine-readable gate block | Plain markdown + plain data. **Zero framework coupling.** Survives any harness. |
| **Thin spine (the liability)** | Orchestration: spawn fresh session per phase, run gates, capture log, enforce policy | The smallest possible TypeScript, with **all harness calls behind one adapter file**. Lean on the extension/SDK surface; defer everything speculative. |

If a better harness (or a native "workflow/pipeline" primitive) ships tomorrow, we re-point a
~200-line driver at it and keep ~90% of the value.

---

## 1. What we keep, delegate, and defer

**Keep (durable, or the only guarantees worth bespoke code):**
- The **6 phase templates** + **2 judge templates** (`prompts/01-research … 06-implement.md`,
  `critique.md`, `revise.md`) — the actual IP.
- The **artifact contract** (each phase reads/writes files under `.agentic-workflow/<run-id>/`).
- **Fresh session per phase** — the one thing a skill/single-session can't do (design §2).
- The plan's **machine-readable gate block** (`stages[]` + `gate{commands,expected}`) — the
  agent reads it and runs/validates its own stage gates inside the implement session.

**Delegate to the Copilot extension / SDK surface (don't hand-roll):**
- **Per-phase read-only enforcement** → `onPreToolUse` returning `permissionDecision: "deny"`
  (replaces the entire `FALLBACK.md` read-only-checkout machinery and demotes the git-diff
  guard to a cheap backstop).
- **Sanctioned artifact writes** → a `write_artifact` custom tool (`skipPermission: true`).
- **Execution log** → `session.on("tool.execution_complete" | "assistant.message" | …)`.
- **Autonomous permissions** → `approveAll`.
- **Transient error retries** → `onErrorOccurred` → `{ errorHandling: "retry" }`.
- **Gate running + validation** → the **agent** runs the stage's `gate.commands` (shell tool)
  inside its own implement session and reports pass/fail in `execution-log` + `handoff.md`. We
  prefer this low-maintenance path over a code-enforced gate runner; the orchestrator just reads
  the agent's reported result to decide whether to continue.
- **Interactive trigger** → a thin `.github/extensions/` front-door (slash command + tool).

**Defer — add ONLY if the harness still doesn't do it when we actually need it:**
- Content hashing + resume **revalidation** engine (start with "re-run from last completed phase").
- Run-lock / dirty-worktree coordination (add only when real concurrency appears).
- Redaction **depth** (ship a trivial regex; harness may own this soon).
- The full 7-command CLI + gate-set **resolution engine** (ship `run`/`resume` + `--simple` only).
- Token-budget ceiling, multi-repo scoping, autopilot sandbox, retention/telemetry.
- `FALLBACK.md` read-only-checkout (expected to be obsoleted by `onPreToolUse`).

---

## 2. Architecture (thin spine)

```
portable assets/                         ← durable IP, harness-agnostic
  prompts/01..06.md  +  critique.md  revise.md
  validate.ts               (a few hand-written checks: subitems + plan block)

src/
  orchestrator.ts           ← sequences phases, reads/writes artifacts, reads agent gate results
  harness.ts                ← *** the ONLY file that imports @github/copilot-sdk ***
                              wraps createSession / send / events / hooks / tools
  session-options.ts        ← shared hooks+tools (onPreToolUse policy, write_artifact,
                              event→log capture); passed into createSession AND reusable
                              by the extension front-door
  artifacts.ts              ← resolve run dir, atomic write, git-ignore (no hashing/lock yet)
  gates.ts                  ← parse plan stages from plan.md (the agent runs the commands)
  cli.ts                    ← `run [--simple] [--no-judge] [--judge-model] | resume [run-id]`  (headless, PRIMARY entry)

.github/extensions/agentic-workflow/
  extension.mjs             ← OPTIONAL interactive front-door: registers a slash command
                              + `run_agentic_workflow` tool that calls the orchestrator
```

**Adapter rule (the whole hedge):** harness churn touches **`harness.ts` only**. The
orchestrator talks to phases through a stable internal interface (`runPhase(opts) →
{ artifacts, log }`), never to the SDK directly.

**Two entry points, one engine:**
- `cli.ts` is the real product — headless, fits the "one command" goal (design §1), TypeScript.
- `extension.mjs` is a thin convenience shim so a user *inside* Copilot CLI can trigger the
  same headless run conversationally (and get hot-reload during dev). It contains **no
  orchestration logic** — it imports and calls the built orchestrator.

---

## 3. The one enforced guarantee (the only bespoke rigor)

1. **Fresh context per phase.** Every phase (and every implement-stage / fan-out item) is a
   brand-new `createSession()`; retries spawn a *new* session seeded with the prior errors,
   never a continued transcript.

That's it. **Gates are run and validated by the agent itself** (it executes `gate.commands` in
its session and reports the result) — we deliberately prefer this low-maintenance path over a
code-enforced gate runner, accepting that a misreporting agent could pass a failing gate. The
orchestrator simply reads the reported result. Everything else is best-effort and leans on the
harness, in exchange for near-zero maintenance.

### 3.1 Reflexive critique (LLM-as-judge per artifact)

After **any** phase's artifact validates, the orchestrator runs a 2-session judge loop — this is
just the **same fresh-session + template primitive applied reflexively**, so it adds capability
without a new infra type. On by default; `--no-judge` disables it.

1. **Critique (fresh session, *alternate* model).** `createSession({ model: judgeModel })`,
   read-only (writes only `critiques/<artifact>.md` via `write_artifact`). It reads the artifact
   and emits **high-signal feedback only** — bugs, gaps, logic/design flaws, contract violations —
   **no style nits**. Using a *different* model than the author is the point: it doesn't share the
   author's blind spots.
2. **Adjudicate/revise (fresh session, author's model).** Reads the **original artifact + the
   critique**, **decides which points to apply** (it's the owner, not a rubber stamp), rewrites the
   artifact via `write_artifact`, and logs accepted/rejected points to the execution log. The
   revised artifact is then **re-validated** — a bad revision triggers the normal fresh-session
   retry.

**Built-in rubber-duck:** Copilot ships a `rubber-duck` agent tuned for exactly this — high-signal
feedback, explicitly *no* style/formatting comments. **If the SDK can spawn a named built-in agent**
(`createSession({ agent: "rubber-duck", model: judgeModel })` — the "custom agents via SDK" surface,
**verified in T0.5**), use it as the critique session; otherwise the `critique.md` template encodes
the same ethos. Either way, pin the **alternate** model for diversity where the API allows.

**Cost:** this ~triples sessions per artifact, so it's flag-gated (`--no-judge`) and the judge model
is configurable (`--judge-model`). This is the one place we *add* sessions for quality — justified
because catching a bad spec/plan early is far cheaper than implementing on top of it.

---

## 4. Milestones (4, not 8)

### T0 — Capability spike *(gates everything; keep it to a day)*
Verify, in a throwaway `spike/`:
- **T0.1** `createSession()` runs headless, streams, stops cleanly; `approveAll` works.
- **T0.2** **Isolation** via single-use nonce (session A reveals it, fresh session B can't).
- **T0.3 (decisive)** **Does `createSession()` accept `hooks` (esp. `onPreToolUse`)?**
  - **yes →** hook-based `deny` is our §6.1 enforcement; `FALLBACK.md` is dropped entirely.
  - **no →** hooks live only in `joinSession`; fall back to the **post-phase git-diff guard**
    as primary enforcement (still cheap, no checkout machinery).
- **T0.4** Concurrency: ≥3 simultaneous sessions for phase-4 fan-out.
- **T0.5** **Can `createSession()` spawn a named built-in agent** (e.g. `agent: "rubber-duck"`) and
  **override the model per session**? Decides whether the §3.1 critique uses the built-in
  rubber-duck or the `critique.md` template; the alternate-model override is required either way.
- **⛔ G0:** isolation + permissions confirmed, and an enforcement path chosen (hooks *or*
  git-diff guard). One short `spike/FINDINGS.md`. No `FALLBACK.md` unless T0.3 = no *and* a
  diff guard proves insufficient.

### T1 — Portable core + minimal substrate *(after G0)*
- **T1.1** The **6 phase templates** + **2 judge templates** (`critique.md`, `revise.md`) in
  `prompts/` (the IP; iterate continuously after this).
- **T1.2** A handful of **plain validation functions** (`validate.ts`) for `subitems.json` +
  the plan block — `JSON.parse`/`YAML.parse` then check the few required fields and return
  readable errors. **No schema library.** Nothing else gets validation until a real format breaks.
- **T1.3** `artifacts.ts`: resolve `.agentic-workflow/<run-id>/`, atomic write, git-ignore via
  `.git/info/exclude` + inner `.gitignore`. **No hashing, no run-lock yet.**
- **⛔ G1:** templates render with run vars; validators accept/reject fixtures; artifacts dir is
  created and git-ignored without touching tracked `.gitignore`.

### T2 — Thin spine *(after G1 — the core)*
- **T2.1** `harness.ts`: wrap `createSession` with `session-options.ts` (the shared
  `onPreToolUse` policy + `write_artifact` tool + event→`execution-log.jsonl` capture +
  `onErrorOccurred` retry). Stable `runPhase()` interface out.
- **T2.2** `orchestrator.ts`: sequence phases 1→6 reading/writing artifacts; fan-out phase 4
  with bounded concurrency; **fresh-session retry** on validation/policy failure. Under
  `--simple`, skip phases 1/3/4 and synthesize a single-item `subitems.json` so phases 5–6
  see an identical contract.
- **T2.3** `gates.ts`: parse plan stages from `plan.md`. The **agent runs `gate.commands`** in
  its implement session and reports pass/fail in `execution-log`/`handoff.md`; the orchestrator
  reads that and stops on a reported failure (no bespoke gate runner).
- **T2.4** Staged implement: one fresh sub-session per stage, `handoff.md` carries forward.
- **T2.5** Judge loop (§3.1): a `judgeArtifact(path, authorModel)` helper — spawn critique (alt
  model, or built-in `rubber-duck` per T0.5) → spawn adjudicate/revise (author model) →
  re-validate. Wrap every validated artifact; honor `--no-judge` / `--judge-model`.
- **⛔ G2 (key):** a real sample task runs end-to-end on a branch; every edit maps to a plan
  step; each validated artifact is critiqued by an alt-model session and revised; the agent runs
  its stage gates and a failing gate is reported and halts the run; non-impl phases mutate
  nothing outside `.agentic-workflow/` (proven by hook-deny *or* the diff guard).

### T3 — Entry points + package *(after G2)*
- **T3.1** `cli.ts`, two commands:
  - **`run [task] [--simple] [--no-judge] [--judge-model <m>]`** (default) — drives the headless
    pipeline. Prints the **human-readable run-id** it creates (e.g. `20260629-1310-add-auth` =
    timestamp + task slug). `--simple` skips the optional phases — research (1), classify (3), and
    per-item research (4) — synthesizing a single-item path and running assumptions → plan →
    implement. `--no-judge` disables the §3.1 critique loop; `--judge-model` sets the alternate
    critique model.
  - **`resume [run-id]`** — re-runs from the last completed phase (no hash engine). `run-id`
    is **optional**: with one incomplete run, resume it; with several, **refuse and list the
    candidates** (id + task + last completed phase) so the user picks; an explicit id resumes
    that run.
  - Exit codes: `0` done / `10` paused / `1` fail / `2` usage.
- **T3.2 (leverage the extension)** `.github/extensions/agentic-workflow/extension.mjs`: a
  slash command + `run_agentic_workflow` tool that invokes the orchestrator from inside an
  interactive Copilot CLI session (hot-reloadable). Thin shim, no logic.
- **T3.3** `README.md`, `ci.yml` (`tools - agentic-workflow - ci`), CODEOWNERS row
  (`/tools/agentic-workflow/ @JennyPng`), root README index, archive `spike/`.
- **⛔ G3:** `npm run build && npm test` green; `run` completes a sample headlessly; the
  extension front-door triggers the same run interactively. **Done.**

---

## 5. How the extension surface is leveraged (summary)

| Need | Bespoke code in `.plan.md` | Thin-spine: lean on harness |
| --- | --- | --- |
| Read-only non-impl phases | `FALLBACK.md` worktrees + patch-apply + git-diff guard | `onPreToolUse` → `deny` (guard = backstop only) |
| Sanctioned writes | custom `write_artifact` tool | same, `skipPermission: true` |
| Execution log | orchestrator event plumbing | `session.on(...)` events |
| Transient retries | bespoke backoff module (M6.1) | `onErrorOccurred` → `retry` |
| Gate running/validation | orchestrator runs commands + reads exit codes | agent runs them in-session, reports result |
| Artifact critique (judge) | n/a | built-in `rubber-duck` agent if SDK-spawnable, else `critique.md` on an alt model |
| Permissions | resolve exact option (M0.4) | `approveAll` |
| Interactive trigger | n/a | `.github/extensions/` slash command + tool |

Caveat: hooks on `createSession` are **verified in T0.3**. If unavailable there, the
`onPreToolUse` items degrade to the git-diff guard, and the extension front-door still stands
as the interactive trigger.

---

## 6. Validation (lean)

- **Unit:** validators accept/reject fixtures; `gates.ts` parses stages from `plan.md`; `artifacts`
  atomic write + git-ignore (and non-git no-op); `onPreToolUse` policy denies a write in a
  non-impl phase (or diff-guard catches it).
- **Scenario (mocked):** fresh-session retry (fail-once-then-succeed); agent-reported failing
  gate halts the run; `resume` continues from last completed phase; judge loop runs
  critique→revise on alt model and `--no-judge` skips it.
- **E2E (opt-in, real SDK, throwaway branch):** one small task to completion; log maps every
  edit to a plan step; gates pass; exit `0`. Not in CI unless a cheap-model lane exists.
- **CI:** `npm run build` + mocked tests + format check only.

---

## 7. Success proof

A run is "done" when all phases complete, every gate passed, `execution-log.jsonl` maps each
edit to a `plan.md` step, and exit `0` — **and** the spine stays under a few hundred lines with
the entire SDK footprint isolated in `harness.ts`. The second clause is the point of this cut:
the day the harness ships a native multi-phase primitive, the spine collapses into config.
