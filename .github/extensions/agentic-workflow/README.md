# Agentic Workflow — Copilot CLI extension

A self-contained [Copilot CLI](https://github.com/github/copilot-cli) extension that drives a
disciplined **research → plan → implement** workflow inside your interactive session. Each phase
runs as its own sub-agent with a **pinned model**, **all tools**, and a **phase prompt**, handing
off to the next phase through **artifacts on disk**. You stay in the loop — advance phases manually,
inspect artifacts between steps, repin models, answer decisions via dialogs — or let the auto-runner
chain phases unattended.

## Getting started

The Copilot CLI **auto-discovers** extensions — it scans `.github/extensions/` (project) and
`~/.copilot/extensions/` (user) for subdirectories containing an `extension.mjs`. There is no
`/extensions` command; discovery is automatic. This extension ships in the repo under
`.github/extensions/agentic-workflow/`. Install its dependencies once:

```bash
cd .github/extensions/agentic-workflow
npm install
```

Then make the CLI (re)discover it and confirm it loaded:

```
/clear     # extensions are (re)loaded on /clear; or restart the CLI entirely
/env       # lists loaded extensions, agents, and commands — confirm agentic-workflow is there
```

> Currently, requires toggling experimental mode in Copilot CLI. Either run `/experimental on` in the session or start it with `copilot --experimental.`

## Quickstart

```
/aw-start Add CSV export to the report endpoint    # init the run + run the first phase
/aw-status                                          # see phase checklist + artifacts + git diff
/aw-continue                                        # run the next phase, then stop
/aw-run                                             # or: auto-run the rest to implement
```

Prefer a shorter loop? Use `/aw-start-simple` to skip the classify / per-item research phases (the
`simple` suffix on `/aw-start` still works as an alias):

```
/aw-start-simple Fix null deref in TokenCache
```

Everything a run produces lives under `<cwd>/.aw/<task-slug>-<hash>/` — specs, assumptions, the
plan, execution log, and a `state.json` that tracks which phases have passed. Inspect or edit those
files between phases at any time. If your session reloads, pick the run back up with `/aw-resume`.

## Commands

| Command | Behavior |
| --- | --- |
| `/aw-start <task>` | Init the run dir and run the first phase (full flow). |
| `/aw-start-simple <task>` | Same, but the short flow (research → assumptions → plan → implement). Equivalent to `/aw-start <task> simple`. |
| `/aw-resume [name-or-text]` | Rehydrate a run after a reload from `.aw/` (task, flow, model overrides). With no arg, resumes the only/most-recent run; otherwise matches by dir name or task text. |
| `/aw-continue [n]` | Run the next phase (or next `n`), then stop. |
| `/aw-run [from:<p>] [to:<p>] [unattended:true] [pause-at:<p>]` | Auto-run a range of phases (defaults: next incomplete → `implement`). |
| `/aw-pause` | Stop the auto-runner at the next phase boundary. |
| `/aw-research` … `/aw-implement` | Run one specific phase by name (`/aw-research`, `/aw-assumptions`, `/aw-classify`, `/aw-research-item`, `/aw-plan`, `/aw-implement`). |
| `/aw-redo <phase> <feedback>` | Re-run a phase with steering notes appended to its prompt. |
| `/aw-judge <artifact>` | Critique an artifact with the critique model, then re-run its author phase to revise it (e.g. `/aw-judge plan.md`). |
| `/aw-autojudge [on\|off]` | Toggle auto-judge for the active run. When on, the auto-runner critiques + revises each phase's judgeable artifacts right after the phase passes. No arg flips the current state. Persisted to `state.json`. |
| `/aw-model <phase> <model>` | Repin a phase's model for its next run (persisted for the active run). |
| `/aw-status` | Show the phase checklist, run-dir artifacts, and `git diff --stat`. |
| `/aw-compact` | Reclaim context by queuing `/compact`. |

### Execution modes

- **Manual** — `/aw-<phase>` or `/aw-continue`; stops after every phase so you can inspect artifacts.
- **Ranged auto** — `/aw-run from:assumptions to:plan`; stops at the boundary or a stop condition.
- **Full auto** — `/aw-run` (or add `unattended:true`); runs to `implement`, halting only at gates,
  `needs_input`, or hard failure.

At a `needs_input` stop (interactive, not `unattended`) the runner shows a `session.ui` dialog to
collect the answer and feeds it back into the next attempt. On `fail` it retries up to twice with the
agent's reason as feedback, then asks retry/skip/abort. With `unattended:true` (or no UI capability)
it auto-resolves with safe defaults and only halts on hard failure.

## Configuration

### Pinned models

Each phase has a default model (see the [Phases](#phases) table). The **critique** phase runs a
different model family (`gpt-5.5`) on purpose, so an artifact is judged by a model other than the one
that wrote it.

Repin any phase at runtime with `/aw-model <phase> <model>`:

```
/aw-model plan claude-opus-4.8
/aw-model critique gpt-5.5
```

The override applies on that phase's **next** run and is persisted to the active run's `state.json`,
so it survives a reload and is re-applied on `/aw-resume`. To change a default permanently, edit the
`PHASES` (or `CRITIQUE`) entry in `extension.mjs`.

### Run directory, state, and resume

State lives entirely in a per-run directory: `<cwd>/.aw/<task-slug>-<hash>/`. The short content hash
keeps tasks that share a slug prefix from colliding onto the same dir.

- Each phase's `PHASE_RESULT` sentinel is recorded in `state.json`. A phase counts as **complete**
  only when it reported `pass` **and** its artifact exists — a phase that failed after writing a
  partial file is **not** skipped. `research-item` additionally requires one `research/<id>.md` per
  sub-item in `subitems.json`.
- Run metadata (task, flow, model overrides) is persisted to `state.json`, so after a reload you can
  `/aw-resume` instead of re-typing the exact task string. `/aw-resume` with no argument restores the
  most-recent run; pass a dir name or task substring to pick a specific one.
- On `/aw-start`, `.aw/` is auto-appended to the target repo's `.gitignore` so run artifacts never
  leak into commits.

## Design

Agent-first: the extension is *config + thin command handlers + a tiny dispatch loop*. There is no
`lib/` of validators, gate parsers, or a state machine. Each phase agent self-reports with a single
sentinel line the loop reads:

```
PHASE_RESULT: pass | fail | needs_input
```

(optionally followed by a short reason). That one convention replaces deterministic validators and
gate parsers — the agent owns correctness, runs its own gate commands, and reads/writes artifacts
with normal file tools. The runner's only durable record is `state.json`, which pairs each phase's
result with its artifact to decide what is complete and what to run next.

### Layout

```
.github/extensions/agentic-workflow/
  extension.mjs     # joinSession: per-phase sub-agents + commands + auto-runner loop
  package.json      # @github/copilot-sdk dependency
  prompts/          # one .md per phase (+ critique) — the durable IP
  test/             # smoke + state-machine tests (arg/sentinel parsing, continuity, resume)
  README.md
```

### Phases

| Phase | Agent | Model (default) | Tools | Writes |
| --- | --- | --- | --- | --- |
| research | `aw-research` | `claude-opus-4.8` | **all** | `specs/*.md`, `manifest.json` |
| assumptions | `aw-assumptions` | `claude-opus-4.8` | **all** | `assumptions.md` |
| classify | `aw-classify` | `claude-opus-4.8` | **all** | `subitems.json`, `classification.md` |
| research-item | `aw-research-item` | `claude-opus-4.8` | **all** | `research/<id>.md` |
| plan | `aw-plan` | `claude-opus-4.8` | **all** | `plan.md` |
| implement | `aw-implement` | `claude-opus-4.8` | **all** | code edits + `execution-log.md`, `handoff.md` |
| critique | `aw-critique` | `gpt-5.5` | **all** | `critiques/<name>.md` |

All phase agents can write their own artifacts under the run directory.

## Develop / test

```bash
npm test        # node --test: arg/sentinel parsing + phase-continuity + resume state-machine tests
```

The durable IP is the per-phase prompt in `prompts/` plus the on-disk artifact contract; tuning
prompts is usually the only thing that needs iteration. Keep prompts repo-agnostic — let the agent
discover repo specifics at runtime; repo-specific guidance belongs in that repo's own `.github/`
instructions, not here.
