# Agentic Workflow — Implementation Execution Log

This log records the implementation of `thinnerplan.plan.md` (the thin-spine re-cut of
`.plan.md`). Each entry: **action**, **justification**, **gate validation**, and any
**deviation** from the plan (with reason, for audit).

Format note: this is the *build* log of the tool itself — distinct from the per-run
`execution-log.jsonl` the tool produces when it runs a workflow.

---

## T0 — Capability spike (gate G0)

**Action.** Created `spike/`, installed `@github/copilot-sdk@1.0.4`, inspected `dist/*.d.ts`
(authoritative API surface), and ran two live spike scripts (`spike.mjs`, `tool-test.mjs`) with
logged-in-user auth against `claude-haiku-4.5`.

**Justification.** The plan makes G0 gate everything: isolation + permissions must be confirmed
and an enforcement path chosen before any build-out. Verifying against live SDK + type defs (not
assumptions) follows the repo rule "base conclusions on authoritative sources."

**Gate validation — G0 = PASS** (see `spike/FINDINGS.md`):
- T0.1 createSession headless + `approveAll` + clean stop — ✅ live.
- T0.2 isolation via single-use nonce — ✅ fresh session replied `NONE`.
- T0.3 `createSession` accepts `hooks.onPreToolUse` and `deny` blocks a tool — ✅ **YES** (live).
- T0.4 ≥3 concurrent sessions — ✅ live.
- T0.5 per-session model override ✅; named built-in-agent spawn ⚠️ not available
  (`SessionConfig.agent` references only `customAgents[]`).
- Custom `write_artifact` tool via `defineTool(name,{…,skipPermission:true})` — ✅ live.

**Deviations from plan.**
- **T0.3 = YES → `FALLBACK.md` dropped entirely** (as the plan's "yes" branch prescribes):
  hook-based `deny` is the §6.1 enforcement; git-diff guard demoted to backstop.
- **T0.5 built-in `rubber-duck` spawn unavailable** → judge critique uses the `critique.md`
  template on an **alternate model** (the plan's explicit fallback). Required capability
  (per-session model override) is confirmed, so judge diversity is preserved.

---

## T1 — Portable core + minimal substrate (gate G1)

**Action.** Scaffolded the tool package (`package.json`, `tsconfig.json`, ESM, vitest — matching
repo TS conventions: 4-space indent, `"type":"module"`, tsc build). Authored the **6 phase
templates** + **2 judge templates** (`prompts/01-research…06-implement.md`, `critique.md`,
`revise.md`). Wrote the minimal substrate:
- `src/types.ts` — shared contract types (PhaseId, SubItem, Stage, RunState, …).
- `src/artifacts.ts` — run-dir resolution, atomic write, local git-ignore (`.git/info/exclude`
  + inner `.gitignore`, never the tracked root `.gitignore`), traversal-guarded `resolveInRunDir`.
- `src/validate.ts` — plain `validateSubitems` + `validatePlan` (no schema lib).
- `src/gates.ts` — `parsePlanStages` extracts/normalizes the ```yaml stages:``` block.
- `src/prompts.ts` — trivial `{{var}}` renderer.

**Justification.** This is the durable IP + the only substrate that must exist before the spine.
Kept dependency-free (only `yaml`) per the thin-spine "write as little machinery as possible".

**Gate validation — G1 = PASS:**
- `npx tsc --noEmit` → exit 0 (clean typecheck).
- `npx vitest run` → **22/22 passing** across 4 files:
  - templates render with run vars, no unresolved `{{…}}` left, read-only contract preserved;
  - validators accept good fixtures and reject empty items / bad enum / non-kebab id / duplicate
    id / dangling `dependsOn` / invalid JSON / missing plan sections / missing gate block;
  - `parsePlanStages` parses multi-stage blocks with defaults and throws on missing commands;
  - artifacts dir is created and ignored via `.git/info/exclude` (idempotent) **without** creating
    a tracked `.gitignore`; atomic write/append and traversal rejection verified.

**Deviations from plan.** None. (Chose `yaml` as the one runtime dep to parse the gate block;
the plan's validators explicitly allow `YAML.parse`.)

---

## T2 — Thin spine (gate G2)

**Action.** Built the spine, all SDK access isolated in `harness.ts`:
- `src/session-options.ts` — `write_artifact` tool (`skipPermission`), `onPreToolUse` read-only
  policy (deny mutating/shell tools), `onErrorOccurred` retry, and redacted JSONL event logging.
- `src/harness.ts` — **the only `@github/copilot-sdk` importer**; wraps `createSession` and
  exposes the stable `Harness.runPhase()` seam.
- `src/orchestrator.ts` — sequences phases 1->6, validates each, **fresh-session retry**, phase-4
  fan-out (dependsOn-ordered, bounded concurrency), `--simple` single-item synthesis, the §3.1
  judge loop (`judgeArtifact`: critique alt-model -> revise author-model -> re-validate w/ rollback),
  staged implement reading the agent-reported `STAGE_RESULT` to halt on a failing gate.
- `src/state.ts` — atomic `state.json` per-phase status for `resume`.

**Justification.** Adapter rule: harness churn touches `harness.ts` only; the orchestrator talks
to phases through `runPhase()`, never the SDK. Gates are agent-run/agent-reported per the user's
stated low-maintenance preference.

**Gate validation — G2 = PASS:**
- Mocked scenario tests (CI-safe): **36/36** across 6 files — fresh-session retry
  (fail-once-then-succeed; `plan` ran twice), agent-reported failing gate halts (exit 1),
  blocking-assumption pause (exit 10), judge loop runs critique+revise and `--no-judge` skips it,
  `--simple` synthesizes a single-item path, full non-simple pipeline with 2-item fan-out, and the
  `onPreToolUse` policy denies mutating tools in read-only phases.
- **Live end-to-end** (real SDK, throwaway git repo, `--simple`, judge ON), run via the built CLI:
  - exit **0**; real code edit (`src/math.js` gained `export function add`), new `test/` file,
    `npm test` green.
  - `plan.md` emitted a valid machine-readable `stages:`/`gate:` block.
  - **read-only enforcement proven**: 6 `policy_deny` events; no source mutated outside the
    implement phase (git diff = only `src/math.js` + untracked `test/`).
  - **judge loop ran live**: `critiques/assumptions.md` + `critiques/plan.md`, 2 `judge_complete`.
  - **gate run by the agent**: `execution-log.md` records `npm test` exit 0 = PASS; orchestrator
    read `STAGE_RESULT: pass`.
  - `execution-log.md` maps every edit to a `plan.md` stage/step with scope justification.

**Deviations from plan.** None of substance. Per gao.md feedback (#2/#5) the structured log is
orchestrator-owned `execution-log.jsonl` with trivial secret redaction built in from the start
(the plan deferred redaction depth but allows a trivial regex now).

---

## T3 — Entry points + packaging (G3) — 2025

**Actions taken.**
- `src/cli.ts` — headless front-door. `run <task...>` (flags `--simple`, `--no-judge`,
  `--judge-model`, `--run-id`, `--out-root`) and `resume [runId]` (resumes the newest incomplete
  run when no id is given). Builds `RunOptions`, constructs `SdkHarness`, calls `runWorkflow`, and
  maps `RunResult.exitCode` straight to `process.exit` (0 success, 10 blocking-assumption pause,
  1/2 failure/usage). `src/index.ts` re-exports `runWorkflow`, `SdkHarness`, and the public types
  as the tool's programmatic API.
- `.github/extensions/agentic-workflow/extension.mjs` — optional interactive front-door. Thin
  shim: registers a `/agentic-workflow` slash command + a `run_agentic_workflow` tool via
  `joinSession`, parses the same flags as the CLI, and **dynamically imports the built
  `dist/index.js`** to delegate. Holds zero orchestration logic — re-point `TOOL_DIST` and it
  still works.
- Packaging: `README.md` (purpose, install/build/test, CLI + extension usage, architecture, where
  used), `ci.yml` (eng/pipelines templates), `.gitignore` (node_modules/dist/.agentic-workflow),
  `.prettierrc.json` (`tabWidth:4`, `printWidth:120` to match the repo `.editorconfig`) and
  `.prettierignore` (keeps prompt templates + spike + markdown pristine).
- Repo wiring: added `/tools/agentic-workflow/ @JennyPng` to `.github/CODEOWNERS` and a row to the
  root `README.md` tool index (status **Not Yet Enabled**).
- Archived the spike: removed `spike/tool-test.mjs` scratch driver; kept `spike.mjs` + `FINDINGS.md`
  as the G0 evidence (its `node_modules`/lockfile stay gitignored).

**Justification.** Two entry points, one engine: both `cli.ts` and `extension.mjs` are dumb shims
over `runWorkflow`/`SdkHarness`, so the durable IP (prompts + contract + orchestrator) has a single
home and the SDK stays isolated in `harness.ts`. The extension delegates by importing the built
artifact rather than re-implementing anything, satisfying the "thin spine" goal.

**Gate validation — G3 = PASS:**
- `npm run build` (tsc) — **ok**, emits `dist/`.
- `npm run format:check` (prettier) — **"All matched files use Prettier code style!"** across the
  source/test/json set (after adding `.prettierrc.json` so prettier honors the repo's 4-space style).
- `npm test` (vitest) — **36/36** in 6 files, unchanged after reformat.
- CLI exit-code contract verified: `run` with no task -> **2**, `resume` with no incomplete run ->
  **2**, unknown command -> **2**, usage prints.
- Extension verified-by-construction: `node --check extension.mjs` passes; `TOOL_DIST` resolves to
  the real `tools/agentic-workflow/dist/index.js`; the imported names (`runWorkflow`, `SdkHarness`)
  and call shapes (`new SdkHarness({workingDirectory})`, `RunResult.{message,exitCode,runDir}`,
  `harness.stop()`) match the `index.ts` exports exactly.

**Deviations from plan.**
- **Added `.prettierrc.json` + `.prettierignore`** (not itemized in the plan). Reason: the plan's G3
  requires `format:check` to pass, but prettier's 2-space default contradicts the repo `.editorconfig`
  (4-space TS). The config pins prettier to the existing house style instead of mass-reformatting to
  2-space; the ignore file keeps the prompt templates byte-for-byte (their whitespace is part of the
  contract) and excludes the spike. Net effect matches the plan's intent (clean `format:check`)
  without altering durable IP.
- **Live interactive trigger of the extension** is verified by construction + syntax/import checks
  rather than a live `/agentic-workflow` invocation, because that requires a hosted interactive
  Copilot CLI session. The delegated engine itself was already proven live end-to-end in G2, so the
  only unexercised surface is the thin `joinSession` registration, whose import path and call shapes
  are statically confirmed above.

---

## Design/implementation gap — noted for audit

`agentic-workflow-design.md` carries edits that introduce **§6.3 "cross-stage context continuity"**
— a richer staged-implement model with a `handoff.md` cross-stage memory artifact, a
`buildContextPack(ctx, stage)` curation step, and a per-stage `context_needed` field in the plan
schema. **This is a forward-looking design refinement and is intentionally NOT implemented in the
thin spine.**

What the implemented thin spine actually does for the staged implement phase: each plan stage runs
in its own fresh sub-session, the orchestrator validates artifacts on disk and reads the
agent-reported `STAGE_RESULT` to halt at the first failing gate. There is **no** `handoff.md`,
`buildContextPack`, or `context_needed` plumbing — cross-stage state is carried by the code on disk
+ `plan.md` + `execution-log.md` only.

Decision (confirmed with the user): keep the design-doc edits as a **forward-looking note** rather
than reverting them or expanding the thinnerplan scope to build the handoff/context-pack machinery.
Anyone reconciling the doc against the code should treat §6.3 (and the `handoff.md` / `context_needed`
/ `buildContextPack` references in §3, §5, the M5 milestone, and the summary) as **planned, not yet
built**.
