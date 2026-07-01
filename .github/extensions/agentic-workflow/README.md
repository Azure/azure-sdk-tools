# Agentic Workflow — Copilot CLI extension

A self-contained [Copilot CLI](https://github.com/github/copilot-cli) extension that drives a
disciplined **research → plan → implement** workflow inside your interactive session. Each phase
runs as its own sub-agent with **all tools** and a **phase prompt**, handing
off to the next phase through **artifacts on disk**. Every phase runs on your configured session
model — the workflow never switches models. You stay in the loop — advance phases manually,
inspect artifacts between steps, answer decisions via dialogs — or let the auto-runner
chain phases unattended.

## Getting started
> Currently, requires toggling experimental mode in Copilot CLI. Either run `/experimental on` in the session or start it with `copilot --experimental.`

The Copilot CLI **auto-discovers** extensions — it scans `.github/extensions/` (project) and
`~/.copilot/extensions/` (user) for subdirectories containing an `extension.mjs`. This extension ships in the repo under
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


## Quickstart

```
/aw-auto Add CSV export to the report endpoint      # start + auto-run the whole flow to implement
```

Prefer to drive it one phase at a time?

```
/aw-start Add CSV export to the report endpoint    # init the run + run the first phase
/aw-status                                          # see phase checklist + artifacts + git diff
/aw-continue                                        # run the next phase, then stop
/aw-auto                                             # or: auto-run the rest to implement
```

Prefer a shorter loop? Use `/aw-auto-simple` (or `/aw-start-simple`) to skip the classify /
per-item research phases (the `simple` suffix on `/aw-start` still works as an alias):

```
/aw-auto-simple Fix null deref in TokenCache
```

Everything a run produces lives under `<cwd>/.aw/<task-slug>-<hash>/` — specs, assumptions, the
plan, execution log, and a `state.json` that tracks which phases have passed. Inspect or edit those
files between phases at any time. If your session reloads, pick the run back up with `/aw-resume`.

## Commands

| Command | Behavior |
| --- | --- |
| `/aw-auto <task>` | Start a run (if given a task) and auto-run to completion. On an existing run with no task, auto-runs the rest. Accepts `[from:<p>] [to:<p>] [unattended:true] [pause-at:<p>]`. |
| `/aw-auto-simple <task>` | Same as `/aw-auto` but the short flow (research → assumptions → plan → implement). |
| `/aw-start <task>` | Init the run dir and run the first phase (full flow). |
| `/aw-start-simple <task>` | Same, but the short flow (research → assumptions → plan → implement). Equivalent to `/aw-start <task> simple`. |
| `/aw-resume [name-or-text]` | Rehydrate a run after a reload from `.aw/` (task, flow, auto-judge toggle). With no arg, resumes the only/most-recent run; otherwise matches by dir name or task text. |
| `/aw-continue [n]` | Run the next phase (or next `n`), then stop. |
| `/aw-pause` | Stop the auto-runner at the next phase boundary. |
| `/aw-research` … `/aw-implement` | Run one specific phase by name (`/aw-research`, `/aw-assumptions`, `/aw-classify`, `/aw-research-item`, `/aw-plan`, `/aw-implement`). |
| `/aw-redo <phase> <feedback>` | Re-run a phase with steering notes appended to its prompt. |
| `/aw-judge [artifact\|diff]` | Rubber-duck an artifact via the native `/rubber-duck` agent and append its critique to that file (e.g. `/aw-judge plan.md`). Use `/aw-judge diff` to critique the current git diff into `critiques/git-diff-*.md`. With no arg, critiques the current work inline. |
| `/aw-autojudge [on\|off]` | Toggle auto-judge for the active run. When on, the auto-runner rubber-ducks every new markdown artifact a phase generates right after it passes, appending each critique to the artifact; after `implement`, it also critiques the git diff. No arg flips the current state. Persisted to `state.json`. |
| `/aw-status` | Show the phase checklist, run-dir artifacts, and `git diff --stat`. |
| `/aw-compact` | Reclaim context by queuing `/compact`. |

### Execution modes

- **Manual** — `/aw-<phase>` or `/aw-continue`; stops after every phase so you can inspect artifacts.
- **Ranged auto** — `/aw-auto from:assumptions to:plan`; stops at the boundary or a stop condition.
- **Full auto** — `/aw-auto <task>` (or add `unattended:true`); starts the run if needed and runs to
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

- **On demand:** `/aw-judge <artifact>` reviews that artifact and appends a `## Rubber-duck critique`
  section to the file (e.g. `/aw-judge plan.md`). `/aw-judge diff` reviews the current `git diff`
  after implementation and writes the critique to `critiques/git-diff-*.md`. With no argument,
  `/aw-judge` critiques the current work inline and the agent decides whether to apply the findings.
- **Automatic:** `/aw-autojudge [on|off]` toggles auto-judge for the active run (persisted to
  `state.json`). When on, the auto-runner rubber-ducks every **new markdown artifact** a phase
  generates right after the phase passes, appending each critique to the artifact. After the
  `implement` phase passes, it also rubber-ducks the repository git diff and writes the critique to
  `critiques/git-diff-*.md`. Non-markdown artifacts (e.g. JSON) are skipped so their structure stays
  valid. Critiques are append-only — auto-judge does not rewrite or auto-revise the artifact.

### Run directory, state, and resume

State lives entirely in a per-run directory: `<cwd>/.aw/<task-slug>-<hash>/`. The short content hash
keeps tasks that share a slug prefix from colliding onto the same dir.

- Each phase's `PHASE_RESULT` sentinel is recorded in `state.json`. A phase counts as **complete**
  only when it reported `pass` **and** its artifact exists — a phase that failed after writing a
  partial file is **not** skipped. `research-item` additionally requires one `research/<id>.md` per
  sub-item in `subitems.json`.
- Run metadata (task, flow, auto-judge toggle) is persisted to `state.json`, so after a reload you
  only need enough information to identify the run:
  - `/aw-resume` resumes the only run under `.aw/`; if there are multiple runs, it lists them.
  - `/aw-resume <dir-name>` resumes by the run directory name, for example
    `/aw-resume add-csv-export-1a2b3c4d`.
  - `/aw-resume <task-substring>` resumes by matching text from the original task, for example
    `/aw-resume CSV export`.
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
  prompts/          # one .md per phase — the durable IP
  test/             # smoke + state-machine tests (arg/sentinel parsing, continuity, resume)
  README.md
```

### Phases

Every phase runs on your configured session model (recommended: `claude-opus-4.8`).

| Phase | Agent | Tools | Writes |
| --- | --- | --- | --- |
| research | `aw-research` | **all** | `specs/*.md`, `manifest.json` |
| assumptions | `aw-assumptions` | **all** | `assumptions.md` |
| classify | `aw-classify` | **all** | `subitems.json`, `classification.md` |
| research-item | `aw-research-item` | **all** | `research/<id>.md` |
| plan | `aw-plan` | **all** | `plan.md` |
| implement | `aw-implement` | **all** | code edits + `execution-log.md`, `handoff.md` |

All phase agents can write their own artifacts under the run directory.

## Develop / test

```bash
npm test        # node --test: arg/sentinel parsing + phase-continuity + resume state-machine tests
```

The durable IP is the per-phase prompt in `prompts/` plus the on-disk artifact contract; tuning
prompts is usually the only thing that needs iteration. Keep prompts repo-agnostic — let the agent
discover repo specifics at runtime; repo-specific guidance belongs in that repo's own `.github/`
instructions, not here.
