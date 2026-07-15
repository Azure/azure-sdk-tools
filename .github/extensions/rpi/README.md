# RPI — Copilot CLI extension

A [Copilot CLI](https://github.com/github/copilot-cli) extension that drives a disciplined
**research → plan → implement** workflow. Each phase runs with all tools and its own prompt, handing
off to the next through **artifacts on disk** under `.rpi/`. Drive it one phase at a time and inspect
artifacts between steps, or let the auto-runner chain phases to completion. Every phase runs on your
configured session model — the workflow never switches models.

## Install

> Requires experimental mode: run `/experimental on`, or start with `copilot --experimental`.

The CLI auto-discovers extensions in `.github/extensions/` (project) and `~/.copilot/extensions/`
(user). This extension ships under `.github/extensions/rpi/`. Install its deps once:

```bash
cd .github/extensions/rpi && npm install
```

Then `/clear` (extensions reload on `/clear`, or restart the CLI) and run `/env` to confirm `rpi` loaded.

### Use it globally (any repo)

The project install only loads inside this repo. To get `/rpi-*` in **every** repo, put the
extension in the user directory (`~/.copilot/extensions/`), which the CLI scans from any working
directory. Two ways:

**Symlink** — stays in sync as you `git pull`, and reuses the repo's already-installed deps:

```bash
mkdir -p ~/.copilot/extensions
ln -s "$(git rev-parse --show-toplevel)/.github/extensions/rpi" ~/.copilot/extensions/rpi
```

> Caveat: a symlink tracks a working-tree path, so it breaks if you switch the repo to a branch that
> doesn't contain `.github/extensions/rpi/` (the folder disappears until you switch back). Use a
> detached copy if you want it independent of the repo's current branch.

**Detached copy** — a snapshot independent of the repo's branch; update it manually by re-copying:

```bash
mkdir -p ~/.copilot/extensions
cp -r "$(git rev-parse --show-toplevel)/.github/extensions/rpi" ~/.copilot/extensions/rpi
cd ~/.copilot/extensions/rpi && npm install
```

Either way: `/clear` (or restart) then `/env` to confirm `rpi` loaded. Uninstall with
`rm ~/.copilot/extensions/rpi`. Run artifacts still land in `.rpi/` of whatever repo you launch the
CLI from, so each project keeps its own runs.

## Quickstart

```
/rpi-auto Add CSV export to the report endpoint      # start + auto-run the whole flow to implement
```

Or drive it one phase at a time:

```
/rpi-start Add CSV export to the report endpoint     # init the run + run the first phase
/rpi-status                                          # phase checklist + artifacts + git diff
/rpi-continue                                        # run the next phase, then stop
/rpi-auto                                            # or auto-run the rest to implement
```

Want a shorter loop? `/rpi-auto-simple` (or `/rpi-start-simple`) skips the research, classify, and
per-item research phases:

```
/rpi-auto-simple Fix null deref in TokenCache
```

Everything a run produces lives under `<cwd>/.rpi/<task-slug>-<hash>/` — specs, assumptions, the
plan, and a `state.json` tracking which phases passed. Inspect or edit those files between phases. If
your session reloads, pick the run back up with `/rpi-resume`.

## Commands

| Command | Behavior |
| --- | --- |
| `/rpi-auto <task>` | Start a run (if given a task) and auto-run to completion. On an existing run with no task, auto-runs the rest. Accepts `[from:<p>] [to:<p>] [unattended:true] [pause-at:<p>]`. |
| `/rpi-auto-simple <task>` | Same as `/rpi-auto` but the short flow (assumptions → plan → implement). |
| `/rpi-start <task>` | Init the run dir and run the first phase (full flow). |
| `/rpi-start-simple <task>` | Same, but the short flow. Equivalent to `/rpi-start <task> simple`. |
| `/rpi-resume [name-or-text]` | Resume a run after a reload. No arg resumes the only/most-recent run; otherwise matches by dir name or task text. |
| `/rpi-continue [n]` | Run the next phase (or next `n`), then stop. |
| `/rpi-pause` | Stop the auto-runner at the next phase boundary. |
| `/rpi-research` … `/rpi-implement` | Run one phase by name (`research`, `assumptions`, `classify`, `research-item`, `plan`, `implement`). |
| `/rpi-redo <phase> <feedback>` | Re-run a phase with steering notes appended to its prompt. |
| `/rpi-judge [artifact\|diff]` | Rubber-duck an artifact (appends a critique to the file), or `diff` to critique the current git diff. No arg critiques current work inline. |
| `/rpi-autojudge [on\|off]` | Toggle auto-judge: rubber-duck each new artifact right after its phase passes, and the git diff after `implement`. No arg flips it. |
| `/rpi-subagents [on\|off]` | Toggle execution mode (see below). No arg flips it. |
| `/rpi-status` | Show execution mode, auto-judge state, phase checklist, artifacts, and `git diff --stat`. |
| `/rpi-help` | Brief in-session explanation of the workflow and main commands. |
| `/rpi-compact` | Reclaim context by queuing `/compact`. |

## Execution modes

- **Manual** — `/rpi-<phase>` or `/rpi-continue`; stops after every phase so you can inspect artifacts.
- **Ranged auto** — `/rpi-auto from:assumptions to:plan`; stops at the boundary.
- **Full auto** — `/rpi-auto <task>` (add `unattended:true` to skip prompts); runs to `implement`,
  halting only at `needs_input` or hard failure. Interactively, a `needs_input` stop opens a dialog;
  a `fail` retries up to twice, then asks retry/skip/abort. Unattended runs auto-resolve with safe
  defaults and halt only on hard failure.

**Compact vs sub-agents** (`/rpi-subagents`, persisted per run): by default (**COMPACT**) phases run
inline in your session and stream natively, with a `/compact` before the heavy implement phase.
Toggle **SUB-AGENTS** on to run each phase in its own fresh context window (activity relayed live) —
this engages **only during an unattended auto-run** (`/rpi-auto … unattended:true`); every
interactive path stays COMPACT.

## Configuration

- **Model** — every phase uses your configured session model (recommended: `claude-opus-4.8`); set it
  before starting a run.
- **Critique** — `/rpi-judge` for on-demand review, `/rpi-autojudge` for automatic per-phase review.
  Critiques are append-only and never rewrite your artifacts.

## Run directory & resume

State lives in `<cwd>/.rpi/<task-slug>-<hash>/`. A phase counts as complete only when it reported
`pass` **and** its artifact exists. Run metadata (task, flow, toggles) is persisted to `state.json`,
so after a reload:

- `/rpi-resume` — resumes the only run under `.rpi/` (lists them if there are several).
- `/rpi-resume <dir-name>` — e.g. `/rpi-resume add-csv-export-1a2b3c4d`.
- `/rpi-resume <task-substring>` — e.g. `/rpi-resume CSV export`.

`.rpi/` is auto-added to the repo's `.gitignore` on `/rpi-start`, so run artifacts never leak into commits.

## Phases

| Phase | Writes |
| --- | --- |
| research | `specs/*.md`, `manifest.json` |
| assumptions | `assumptions.md` |
| classify | `subitems.json`, `classification.md` |
| research-item | `research/<id>.md` |
| plan | `plan.md` |
| implement | code edits + `execution-log.md`, `handoff.md` |

## Develop / test

```bash
npm test   # node --test: arg/sentinel parsing, phase-continuity, and resume state-machine tests
```

Two `*.live.mjs` checks (require an authenticated Copilot CLI; they make real model calls) exercise
the sub-agent path end to end. The durable IP is the per-phase prompts in `prompts/` plus the on-disk
artifact contract — keep prompts repo-agnostic and let the agent discover repo specifics at runtime.
