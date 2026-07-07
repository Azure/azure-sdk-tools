# RPI — Copilot CLI extension

A self-contained [Copilot CLI](https://github.com/github/copilot-cli) extension that drives a
disciplined **research → plan → implement** workflow inside your interactive session. Each phase
runs with **all tools** and a **phase prompt**, handing off to the next phase through **artifacts on
disk**. By default phases run **inline** in your session (with a `/compact` before the heavy
implement phase); flip `/rpi-subagents on` to run each phase in its own **fresh context window**
instead — this takes effect only during an **unattended auto-run** (`/rpi-auto … unattended:true`),
with each sub-agent's activity relayed live into the session. Every phase runs on your configured
session model — the workflow never switches models. You stay in the loop — advance phases manually,
inspect artifacts between steps, answer decisions via dialogs — or let the auto-runner
chain phases unattended.

## Getting started
> Currently, requires toggling experimental mode in Copilot CLI. Either run `/experimental on` in the session or start it with `copilot --experimental.`

The Copilot CLI **auto-discovers** extensions — it scans `.github/extensions/` (project) and
`~/.copilot/extensions/` (user) for subdirectories containing an `extension.mjs`. This extension ships in the repo under
`.github/extensions/rpi/`. Install its dependencies once:

```bash
cd .github/extensions/rpi
npm install
```

Then make the CLI (re)discover it and confirm it loaded:

```
/clear     # extensions are (re)loaded on /clear; or restart the CLI entirely
/env       # lists loaded extensions, agents, and commands — confirm rpi is there
```

### Use it globally (any repo)

The install above is **project-scoped** — the extension only loads when you launch the CLI from
this repo. To make `/rpi-*` available in **every** repository, put the extension in the **user**
extensions directory (`~/.copilot/extensions/`), which the CLI scans regardless of your working
directory.

Symlink it (recommended — stays in sync as you `git pull` this repo):

```bash
mkdir -p ~/.copilot/extensions
# `git rev-parse --show-toplevel` resolves the repo root, so this works from any dir inside the repo:
ln -s "$(git rev-parse --show-toplevel)/.github/extensions/rpi" ~/.copilot/extensions/rpi
cd ~/.copilot/extensions/rpi && npm install   # install deps at the link target once
```

Or copy it (a detached snapshot you update manually):

```bash
mkdir -p ~/.copilot/extensions
cp -r "$(git rev-parse --show-toplevel)/.github/extensions/rpi" ~/.copilot/extensions/rpi
cd ~/.copilot/extensions/rpi && npm install
```

Then `/clear` (or restart) and run `/env` to confirm `rpi` is loaded. Now `/rpi-auto`, `/rpi-start`,
etc. work from any repo. The workflow still writes its run artifacts to `.rpi/` in **whatever repo you
launch the CLI from**, so each project keeps its own runs.

> Avoid running two copies at once: if you keep the project install (`.github/extensions/rpi/`) for
> developing local changes, you generally don't also want the global copy loaded in this repo, or the
> `/rpi-*` commands may be registered twice. Remove the global install with `rm ~/.copilot/extensions/rpi`
> (deletes only the symlink or the copied folder).


## Quickstart

```
/rpi-auto Add CSV export to the report endpoint      # start + auto-run the whole flow to implement
```

Prefer to drive it one phase at a time?

```
/rpi-start Add CSV export to the report endpoint    # init the run + run the first phase
/rpi-status                                          # see phase checklist + artifacts + git diff
/rpi-continue                                        # run the next phase, then stop
/rpi-auto                                             # or: auto-run the rest to implement
```

Prefer a shorter loop? Use `/rpi-auto-simple` (or `/rpi-start-simple`) to skip the research,
classify, and per-item research phases (the `simple` suffix on `/rpi-start` still works as an alias):

```
/rpi-auto-simple Fix null deref in TokenCache
```

Everything a run produces lives under `<cwd>/.rpi/<task-slug>-<hash>/` — specs, assumptions, the
plan, execution log, and a `state.json` that tracks which phases have passed. Inspect or edit those
files between phases at any time. If your session reloads, pick the run back up with `/rpi-resume`.

## Commands

| Command | Behavior |
| --- | --- |
| `/rpi-auto <task>` | Start a run (if given a task) and auto-run to completion. On an existing run with no task, auto-runs the rest. Accepts `[from:<p>] [to:<p>] [unattended:true] [pause-at:<p>]`. |
| `/rpi-auto-simple <task>` | Same as `/rpi-auto` but the short flow (assumptions → plan → implement). |
| `/rpi-start <task>` | Init the run dir and run the first phase (full flow). |
| `/rpi-start-simple <task>` | Same, but the short flow (assumptions → plan → implement). Equivalent to `/rpi-start <task> simple`. |
| `/rpi-resume [name-or-text]` | Rehydrate a run after a reload from `.rpi/` (task, flow, execution mode + auto-judge toggles). With no arg, resumes the only/most-recent run; otherwise matches by dir name or task text. |
| `/rpi-continue [n]` | Run the next phase (or next `n`), then stop. |
| `/rpi-pause` | Stop the auto-runner at the next phase boundary. |
| `/rpi-research` … `/rpi-implement` | Run one specific phase by name (`/rpi-research`, `/rpi-assumptions`, `/rpi-classify`, `/rpi-research-item`, `/rpi-plan`, `/rpi-implement`). |
| `/rpi-redo <phase> <feedback>` | Re-run a phase with steering notes appended to its prompt. |
| `/rpi-judge [artifact\|diff]` | Rubber-duck an artifact via the native `/rubber-duck` agent and append its critique to that file (e.g. `/rpi-judge plan.md`). Use `/rpi-judge diff` to critique the current git diff into `critiques/git-diff-*.md`. With no arg, critiques the current work inline. |
| `/rpi-autojudge [on\|off]` | Toggle auto-judge for the active run. When on, the auto-runner rubber-ducks every new markdown artifact a phase generates right after it passes, appending each critique to the artifact; after `implement`, it also critiques the git diff. No arg flips the current state. Persisted to `state.json`. |
| `/rpi-subagents [on\|off]` | Toggle how phases run: **COMPACT** (default) runs each phase inline in this session (streams natively; `/compact` before implement) vs **SUB-AGENTS** — each phase runs in its own fresh context window with its activity relayed live. Sub-agents engage **only during an unattended auto-run** (`/rpi-auto … unattended:true`); interactive runs always stay COMPACT. No arg flips the current state. Persisted to `state.json`. |
| `/rpi-status` | Show the current execution mode + auto-judge state, the phase checklist, run-dir artifacts, and `git diff --stat`. |
| `/rpi-help` | Show a brief explanation of the workflow and its main commands. |
| `/rpi-compact` | Reclaim context by queuing `/compact`. |

### Compact vs sub-agents

By default the workflow runs each phase **inline** in your session (COMPACT mode): you see its work
stream natively, and a `/compact` runs before the context-heavy implement phase. Toggle
`/rpi-subagents on` to instead run every phase as its **own fresh-context sub-agent** — each stage
starts from a clean window seeded only by its prompt and the prior phases' on-disk artifacts, so the
workflow never has to compact between stages. Because a sub-agent's work does not land in the host
timeline on its own, its activity (intents, tool calls, messages) is **relayed live** into the
session so you are never left staring at a silent prompt. The mode is per-run and persisted to
`state.json`, so `/rpi-resume` restores it.

**Sub-agents only run during an unattended auto-run.** Even with `/rpi-subagents on`, a phase runs
inline/COMPACT unless it is executing inside `/rpi-auto … unattended:true`. Every interactive path —
manual phase commands (`/rpi-research`, `/rpi-continue`, `/rpi-redo`) and *attended* auto-runs — stays
COMPACT. This is intentional: when a human is watching, the runtime's own "background agent …
completed" notice would collide with the interactive agent (it can get woken by that notice and start
redoing the phase itself). Restricting sub-agents to unattended runs keeps the interactive experience
clean while still giving long unattended runs a fresh context per stage.

Sub-agents spawned this way are session-level **background agents**, so the Copilot CLI also emits
its own native "background agent … completed" notice for each phase (on top of the relayed activity)
— that's expected. The extension deliberately does **not** cancel/remove these tasks when a phase
settles: doing so would delete the task out from under the CLI's own completion read and surface a
confusing "agent no longer exists" error. The runtime cleans up settled tasks on its own. COMPACT
stays the default precisely because it keeps everything in one clean, natively-streamed timeline.
its own native "background agent … completed" notice for each phase (on top of the relayed activity)
— that's expected. The extension deliberately does **not** cancel/remove these tasks when a phase
settles: doing so would delete the task out from under the CLI's own completion read and surface a
confusing "agent no longer exists" error. The runtime cleans up settled tasks on its own. COMPACT
stays the default precisely because it keeps everything in one clean, natively-streamed timeline.

### Execution modes

- **Manual** — `/rpi-<phase>` or `/rpi-continue`; stops after every phase so you can inspect artifacts.
- **Ranged auto** — `/rpi-auto from:assumptions to:plan`; stops at the boundary or a stop condition.
- **Full auto** — `/rpi-auto <task>` (or add `unattended:true`); starts the run if needed and runs to
  `implement`, halting only at gates, `needs_input`, or hard failure.

At a `needs_input` stop (interactive, not `unattended`) the runner shows a `session.ui` dialog to
collect the answer and feeds it back into the next attempt. On `fail` it retries up to twice with the
agent's reason as feedback, then asks retry/skip/abort. With `unattended:true` (or no UI capability)
it auto-resolves with safe defaults and only halts on hard failure.

## Configuration

### Model

The workflow never switches models: every phase runs on whatever model you have configured for your
Copilot CLI session. The recommended session model is `claude-opus-4.8`. To use a different model,
set it for your session (e.g. via the CLI's model selection) before starting a run.

### Critique

For an independent critique, use the native `/rubber-duck` agent through the workflow:

- **On demand:** `/rpi-judge <artifact>` reviews that artifact and appends a `## Rubber-duck critique`
  section to the file (e.g. `/rpi-judge plan.md`). `/rpi-judge diff` reviews the current `git diff`
  after implementation and writes the critique to `critiques/git-diff-*.md`. With no argument,
  `/rpi-judge` critiques the current work inline and the agent decides whether to apply the findings.
- **Automatic:** `/rpi-autojudge [on|off]` toggles auto-judge for the active run (persisted to
  `state.json`). When on, the auto-runner rubber-ducks every **new markdown artifact** a phase
  generates right after the phase passes, appending each critique to the artifact. After the
  `implement` phase passes, it also rubber-ducks the repository git diff and writes the critique to
  `critiques/git-diff-*.md`. Non-markdown artifacts (e.g. JSON) are skipped so their structure stays
  valid. Critiques are append-only — auto-judge does not rewrite or auto-revise the artifact.

### Run directory, state, and resume

State lives entirely in a per-run directory: `<cwd>/.rpi/<task-slug>-<hash>/`. The short content hash
keeps tasks that share a slug prefix from colliding onto the same dir.

- Each phase's `PHASE_RESULT` sentinel is recorded in `state.json`. A phase counts as **complete**
  only when it reported `pass` **and** its artifact exists — a phase that failed after writing a
  partial file is **not** skipped. `research-item` additionally requires one `research/<id>.md` per
  sub-item in `subitems.json`.
- Run metadata (task, flow, auto-judge toggle) is persisted to `state.json`, so after a reload you
  only need enough information to identify the run:
  - `/rpi-resume` resumes the only run under `.rpi/`; if there are multiple runs, it lists them.
  - `/rpi-resume <dir-name>` resumes by the run directory name, for example
    `/rpi-resume add-csv-export-1a2b3c4d`.
  - `/rpi-resume <task-substring>` resumes by matching text from the original task, for example
    `/rpi-resume CSV export`.
- On `/rpi-start`, `.rpi/` is auto-appended to the target repo's `.gitignore` so run artifacts never
  leak into commits.

## Design

Agent-first: the extension is *config + thin command handlers + a tiny dispatch loop*. There is no
`lib/` of validators, gate parsers, or a state machine. Each phase is registered as a custom agent
and dispatched in one of two user-selectable modes (`/rpi-subagents`, persisted per run):

- **COMPACT (default):** run the phase inline via `agent.select()` + `sendAndWait()`, so its work
  streams natively in the host timeline; `/compact` runs before the context-heavy implement phase.
  The session is `agent.deselect()`ed back to the default agent after each phase so it is never left
  pinned to a phase role (a lingering selection makes the interactive agent keep acting as that
  phase on later wakes).
- **SUB-AGENTS:** spawn the phase's agent as a fresh-context background task via
  `session.rpc.tasks.startAgent`. The loop polls `tasks.list` until it settles (background sub-agents
  rest at `idle`), then reads its final response. A sub-agent's work does not land in the host
  timeline on its own, so a `session.on(...)` subscription forwards its events — which arrive tagged
  with a non-empty `agentId` (root-agent events have none) — to the user as concise activity lines.
  This mode is gated on `unattendedActive`: it engages **only** while an unattended `autoRun` is
  executing its phase loop. Interactive dispatch (manual phase commands, attended auto) always takes
  the COMPACT path even when `/rpi-subagents` is on, so the interactive agent is never racing the
  runtime's own background-completion notice.

Either way, each phase agent self-reports with a single sentinel line the loop parses:

```
PHASE_RESULT: pass | fail | needs_input
```

(optionally followed by a short reason). That one convention replaces deterministic validators and
gate parsers — the agent owns correctness, runs its own gate commands, and reads/writes artifacts
with normal file tools. The runner's only durable record is `state.json`, which pairs each phase's
result with its artifact to decide what is complete and what to run next — and also carries the
per-run `subagents` and `autoJudge` toggles so `/rpi-resume` restores them.

### Layout

```
.github/extensions/rpi/
  extension.mjs     # joinSession: per-phase sub-agents + commands + auto-runner loop
  package.json      # @github/copilot-sdk dependency
  prompts/          # one .md per phase — the durable IP
  test/             # smoke + state-machine tests, plus *.live.mjs sub-agent spawn checks
  README.md
```

### Phases

Every phase runs on your configured session model (recommended: `claude-opus-4.8`).

| Phase | Agent | Tools | Writes |
| --- | --- | --- | --- |
| research | `rpi-research` | **all** | `specs/*.md`, `manifest.json` |
| assumptions | `rpi-assumptions` | **all** | `assumptions.md` |
| classify | `rpi-classify` | **all** | `subitems.json`, `classification.md` |
| research-item | `rpi-research-item` | **all** | `research/<id>.md` |
| plan | `rpi-plan` | **all** | `plan.md` |
| implement | `rpi-implement` | **all** | code edits + `execution-log.md`, `handoff.md` |

All phase agents can write their own artifacts under the run directory.

## Develop / test

```bash
npm test        # node --test: arg/sentinel parsing + phase-continuity + resume state-machine tests
```

Two **live** checks (require an authenticated Copilot CLI; they make real model calls) verify the
fresh-context sub-agent path end to end:

```bash
node test/spawn-subagent.live.mjs     # minimal: tasks.startAgent spawns a sub-agent that replies PONG
node test/dispatch-subagent.live.mjs  # a real phase agent spawns fresh, writes its artifact, self-reports
```

The durable IP is the per-phase prompt in `prompts/` plus the on-disk artifact contract; tuning
prompts is usually the only thing that needs iteration. Keep prompts repo-agnostic — let the agent
discover repo specifics at runtime; repo-specific guidance belongs in that repo's own `.github/`
instructions, not here.
