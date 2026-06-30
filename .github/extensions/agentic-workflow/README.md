# Agentic Workflow — Copilot CLI extension

A self-contained [Copilot CLI](https://github.com/github/copilot-cli) extension that drives a
disciplined **research → plan → implement** workflow inside your interactive session. Each phase
runs as its own sub-agent with a **pinned model**, **all tools**, and a **phase prompt**, handing
off to the next phase through **artifacts on disk**. You stay in the loop — advance phases manually,
inspect artifacts between steps, repin models, answer decisions via dialogs — or let the auto-runner
chain phases unattended.

## Design

Agent-first: the extension is *config + thin command handlers + a tiny dispatch loop*. There is no
`lib/` of validators, gate parsers, or a state machine. Each phase agent self-reports with a single
sentinel line the loop reads:

```
PHASE_RESULT: pass | fail | needs_input
```

(optionally followed by a short reason). That one convention replaces deterministic validators and
gate parsers — the agent owns correctness, runs its own gate commands, and reads/writes artifacts
with normal file tools.

### Layout

```
.github/extensions/agentic-workflow/
  extension.mjs     # joinSession: per-phase sub-agents + commands + auto-runner loop
  package.json      # @github/copilot-sdk dependency
  prompts/          # one .md per phase (+ critique) — the durable IP
  test/             # smoke test for arg parsing + PHASE_RESULT parsing
  README.md
```

### Phases

| Phase | Agent | Model (default) | Tools | Writes |
| --- | --- | --- | --- | --- |
| research | `aw-research` | `claude-sonnet-4.5` | **all** | `specs/*.md`, `manifest.json` |
| assumptions | `aw-assumptions` | session default | **all** | `assumptions.md` |
| classify | `aw-classify` | `claude-haiku-4.5` | **all** | `subitems.json`, `classification.md` |
| research-item | `aw-research-item` | `claude-sonnet-4.5` | **all** | `research/<id>.md` |
| plan | `aw-plan` | `claude-sonnet-4.5` | **all** | `plan.md` |
| implement | `aw-implement` | `claude-sonnet-4.5` | **all** | code edits + `execution-log.md`, `handoff.md` |
| critique | `aw-critique` | `claude-haiku-4.5` | **all** | `critiques/<name>.md` |

All phase agents can write their own artifacts under the run directory.

State lives entirely in a per-run directory: `<cwd>/.aw/<task-slug>/`. The workflow is inspectable,
resumable, and survives an extension reload — the next phase is derived from which artifact files
already exist, so re-running `/aw-start <same task>` resumes where you left off.

## Install

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

> If the extension was added while the CLI was already running, a full **restart** of the CLI is the
> most reliable way to pick it up (the in-session `/clear` reload also works once it's been
> discovered).

To use it across every repo you open, copy this folder (with its installed `node_modules`) into the
user extensions directory `~/.copilot/extensions/agentic-workflow/`, then `/clear` or restart.

## Commands

| Command | Behavior |
| --- | --- |
| `/aw-start <task> [simple]` | Init the run dir and run the first phase. `simple` = short flow (research → assumptions → plan → implement). |
| `/aw-run [from:<p>] [to:<p>] [unattended:true] [pause-at:<p>]` | Auto-run a range of phases. |
| `/aw-continue [n]` | Run the next phase (or next `n`), then stop. |
| `/aw-pause` | Stop the auto-runner at the next phase boundary. |
| `/aw-research` … `/aw-implement` | Run one specific phase. |
| `/aw-judge <artifact>` | Critique an artifact, then re-run its author phase to revise it. |
| `/aw-redo <phase> <feedback>` | Re-run a phase with steering notes. |
| `/aw-model <phase> <model>` | Repin a phase's model for its next run. |
| `/aw-status` | Show phase checklist, run-dir artifacts, and `git diff --stat`. |
| `/aw-compact` | Reclaim context by queuing `/compact`. |

## Execution modes

- **Manual** — `/aw-<phase>` or `/aw-continue`; stops after every phase.
- **Ranged auto** — `/aw-run from:assumptions to:plan`; stops at the boundary or a stop condition.
- **Full auto** — `/aw-run` (or `unattended:true`); runs to `implement`, halting only at gates,
  `needs_input`, or hard failure.

At a `needs_input` stop (interactive, not `unattended`) the runner shows a `session.ui` dialog to
collect the answer and feeds it back into the next attempt. On `fail` it retries up to twice with the
agent's reason as feedback, then asks retry/skip/abort. With `unattended:true` (or no UI capability)
it auto-resolves with safe defaults and only halts on hard failure.

## Develop / test

```bash
npm test        # node --test: smoke test for arg parsing + PHASE_RESULT parsing
```

The durable IP is the per-phase prompt in `prompts/` plus the on-disk artifact contract; tuning
prompts is usually the only thing that needs iteration. Keep prompts repo-agnostic — let the agent
discover repo specifics at runtime; repo-specific guidance belongs in that repo's own `.github/`
instructions, not here.
