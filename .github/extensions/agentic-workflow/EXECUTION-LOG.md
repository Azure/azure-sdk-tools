# Execution Log — CLI-Extension Phased Workflow

Implementation of `cli-extension-impl.md`. Records every action taken, the justification, and any
deviation from the plan. The plan's design principle (lean on the agent, minimize deterministic
code) was followed throughout: the result is `extension.mjs` + `prompts/` with no `lib/` of
validators, gate parsers, artifact tools, or a state machine.

## Step 0 — Prerequisites

- Created `.github/extensions/agentic-workflow/package.json` (`type: module`, dependency
  `@github/copilot-sdk ^1.0.4`).
- `npm install` → installed SDK v1.0.4 (confirmed `node_modules/@github/copilot-sdk/package.json`
  version 1.0.4).
- Verified import: `node -e "import('@github/copilot-sdk/extension')…"` → `ok`.
- **Verification of SDK surface (v1.0.4 .d.ts):** confirmed every API the plan/design name before
  using it — `joinSession` (extension.d.ts), `CustomAgentConfig {name,model,tools:string[]|null,
  prompt}` (types.d.ts:1157), `customAgents`/`infiniteSessions` on the join config
  (1626/InfiniteSessionConfig), `session.rpc.agent.select
  ({name})` (rpc.d.ts), `session.sendAndWait(prompt|MessageOptions,timeout)` returning
  `AssistantMessageEvent` with text at `.data.content` (session.d.ts:126, session-events.d.ts:2634),
  `session.ui.{confirm,select,input,elicitation}` gated on `session.capabilities.ui?.elicitation`
  (types.d.ts SessionUiApi), `session.setModel` (session.d.ts:268), `session.rpc.commands.enqueue
  ({command})` (rpc.d.ts:14553), `session.abort()` (254), `session.log` (280), and
  `commands: CommandDefinition[]` with `handler(ctx)` + raw `ctx.args` (types.d.ts:425,1424).

## Step 1 — Prompt templates (durable IP)

Created `prompts/{01-research,02-assumptions,03-classify,04-research-item,05-plan,06-implement,
critique}.md`, carried over from the prototype's reasoning content (the durable IP) with the
plan's three strips applied:

1. `write_artifact`/`read_artifact` custom-tool references + run-context preamble → "write/read this
   file under the run directory (`{{runDir}}`) with your normal file tools".
2. The machine-readable `stages:`/`gate:` YAML block the orchestrator parsed → "define your stages
   **in prose** with explicit gate commands and **run the gates yourself**" (05-plan.md, 06-implement
   reads prose stages).
3. Fresh-isolated-session / external-validator wording → the `PHASE_RESULT: pass|fail|needs_input`
   self-report. Blocking assumptions now surface as `needs_input` instead of an orchestrator-parsed
   `blocking: true` pause.

Kept `{{task}}` and added `{{runDir}}`/`{{priorErrors}}` placeholders (verified each template uses
only those three — supplied by `dispatch`). Each template now self-checks its output; 06-implement
additionally runs the gate commands.

**Deviation D1 — research-item is one agent over all sub-items, not N dispatches.** The prototype
dispatched phase 4 once per sub-item with `{{item}}`/`{{itemId}}` placeholders. To stay agent-first
and keep `dispatch` uniform (only `{{task}}/{{runDir}}/{{priorErrors}}`), `04-research-item.md`
instructs the single `aw-research-item` agent to read `subitems.json` itself and produce
`research/<id>.md` for every item in dependency order. Lower-maintenance, no per-item orchestration
code. (Aligns with @JennyPng's "lean on the agent" preference.)

**Deviation D2 — implement is one agent over all stages, not one dispatch per stage.** The prototype
ran a fresh session per stage with `{{stage}}`/`{{handoff}}`/`{{cumulativeDiff}}` injected. Here the
single `aw-implement` agent reads the whole prose plan and works stage-by-stage, running each stage's
gates before advancing. This removes the stage state machine and the YAML gate parser entirely — the
explicit goal of the plan ("no state machine, run gates yourself").

**Deviation D3 — `/aw-judge` revises via the author phase, no separate `revise.md`.** The prototype
had a `revise.md` adjudication prompt. The impl plan's `/aw-judge` spec is "run critique, then re-run
the author phase with the critique as `priorErrors`", so the author phase's own template handles the
revision. `revise.md` was intentionally dropped.

## Steps 2–5 — `extension.mjs`

Single file: a phase registry (`PHASES` + `CRITIQUE`) carrying per-phase model, tool access,
template, and the sentinel artifact for completion detection; `dispatch`; `autoRun`; thin command
handlers; and the `joinSession` config.

- **Step 2 (agents + tool access).** Built `customAgents` (one per phase incl. critique) with
  `tools:null` and a stable role `prompt`, plus `infiniteSessions.enabled`. Every phase gets all
  tools so it can create/write its own artifacts under the run directory.

  **Deviation D4 — models repinned at runtime via `setModel`, not pinned in `customAgents[].model`.**
  The plan's table pins a model per agent and `/aw-model` is supposed to repin live. But
  `customAgents` is fixed at join time, and a pinned `customAgents[].model` would *win* over
  `session.setModel`, making `/aw-model` a no-op without an `/extensions reload`. To make `/aw-model`
  genuinely work (the design's "repin models" goal) I omit `customAgents[].model`, store each phase's
  model in the registry, and apply it via `session.setModel(phase.model)` immediately before
  `agent.select` in `dispatch`. Agents with no pinned model fall back to the session model
  (CustomAgentConfig.model docs), so this is clean. `/aw-model` mutates `phase.model` for the next
  dispatch. Net: same per-phase pinned-model behavior, but live-repinnable.

  **Deviation D5 — agent `prompt` is a short role line; full filled template delivered per dispatch.**
  The plan put "the phase prompt" in `customAgents[].prompt`. But the templates contain
  `{{task}}/{{runDir}}` unknown at join time, so a statically-pinned template would leak literal
  `{{…}}` into agent context. Instead each agent's pinned `prompt` is a concise role/identity line
  and the fully-substituted phase instructions are sent per-dispatch via `sendAndWait`. The prompt
  files remain the durable IP; only their delivery moment changed. Avoids double-injection and
  placeholder leakage.

- **Step 3 (dispatch + commands).** Run dir = `<cwd>/.aw/<task-slug>/` (repo-agnostic, derived from
  `process.cwd()`); active run tracked in memory; next phase derived by `fs.existsSync` checks on
  each phase's sentinel artifact (no `state.json`). `dispatch(phase,{priorErrors})` selects the
  agent, repins model, substitutes the three placeholders, `sendAndWait`s, and parses the
  `PHASE_RESULT` sentinel with one regex (`parsePhaseResult`). Thin handlers for `/aw-start`,
  `/aw-research`…`/aw-implement`, `/aw-continue [n]`, `/aw-judge`, `/aw-redo`, `/aw-model`,
  `/aw-status` (artifacts + `git diff --stat`, degrades on non-git).

  **Minor D6 — templates embed `{{task}}`, so `dispatch` does not also prepend the raw task.** The
  plan sketch wrote `prompt: task + "\n\n" + template`. Since every template already has a `## Task
  {{task}}` section, prepending would duplicate it; `dispatch` just substitutes.

- **Step 4 (auto-runner + elicitation).** `autoRun({from,to,unattended,pauseAt})` walks the phase
  order; per phase branches on `PHASE_RESULT`: `pass`→advance; `needs_input`→`askHuman` (UI
  `input` when `capabilities.ui?.elicitation` && not unattended, else a safe-default answer);
  `fail`→retry up to `MAX_RETRIES=2` with the agent's reason as `priorErrors`, then `resolveFailure`
  (UI `select` retry/skip/abort, or abort when unattended/no-UI). `/aw-pause` sets a cooperative flag
  checked each boundary; `pauseAt` honored as a breakpoint. `from` defaults to the next incomplete
  phase, `to` defaults to `implement`.

- **Step 5 (context hygiene).** `/aw-compact` enqueues `/compact` via
  `session.rpc.commands.enqueue`; `dispatch` auto-enqueues `/compact` before the `implement` phase;
  `infiniteSessions:{enabled:true}` set in the join config. All state on disk → resume by re-deriving
  the next phase from existing files.

- **Removed the old shim.** The prior `extension.mjs` (a thin front-door delegating to the built
  prototype `dist/`) was deleted and fully replaced.

## Step 6 — Replace prototype, document

- Deleted `tools/agentic-workflow/` (prototype). Verified no remaining active references in
  `.github/`, `eng/`, or `README.md`.
- `README.md` index row for `agentic-workflow` repointed to
  `.github/extensions/agentic-workflow/README.md` with an updated description.
- `.github/CODEOWNERS`: `/tools/agentic-workflow/ @JennyPng` →
  `/.github/extensions/agentic-workflow/ @JennyPng`.
- Wrote `.github/extensions/agentic-workflow/README.md` (purpose, layout, phase/agent table,
  install, command list, manual/auto modes, `PHASE_RESULT` convention, develop/test).
- Added `.gitignore` (`node_modules/`, `.aw/`) and a `test` npm script.
- **Light test (optional in plan):** `test/smoke.test.mjs` (node:test) covers the only non-trivial
  pure logic — `parsePhaseResult`, `parseKv`, `isTruthy`, `slugify` — plus the `joinConfig` shape
  (seven agents, all-tools phase access, command surface). Guarded the top-level
  `joinSession` behind `AW_SKIP_JOIN` so the module imports cleanly under test without a live host.

  Left the older `agentic-workflow-design.md` (the *prototype's* historical design doc) untouched —
  out of scope for this implementation; only `cli-extension-design.md`/`cli-extension-impl.md` drive
  this work.

## Validation performed

- `node --check extension.mjs` → clean.
- `npm test` → 8/8 pass (pure logic + join-config shape).
- `npm ci` → lockfile consistent, 0 vulnerabilities.
- SDK import smoke (`@github/copilot-sdk/extension`) → ok.

**Not run here:** the live `/extensions info agentic-workflow` and a real end-to-end `/aw-start →
… → /aw-implement` run require the interactive Copilot CLI host, which isn't available in this
environment. The join config is verified against the v1.0.4 type definitions and the
`joinConfig`-shape test; these are the Step 1–6 "Verify" items achievable offline. The remaining
live verifications are the first manual dogfood per the plan's Rollout step 1 (spike on a throwaway
branch).

## What is code vs delegated to the agent (as built)

Irreducible code: `customAgents` registry (tool access + role) + runtime `setModel` repin;
thin `commands` + `parseKv`/`isTruthy` arg parsing;
`dispatch` + `parsePhaseResult` (one regex); `autoRun` loop + stop branching; `session.ui`
elicitation calls. Delegated to the agent: artifact correctness/self-check, running gate commands,
stage breakdown/sequencing, reading/writing artifacts, classification + research fan-out, judging
critiques.
