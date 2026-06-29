# agentic-workflow

A single command that drives a disciplined **research → plan → implement** workflow for AI coding
agents, running **one fresh, isolated [Copilot SDK](https://github.com/github/copilot-sdk) session
per phase** and handing off between phases through **durable artifacts on disk**.

The hard guarantee — *one clean context per phase* — is what a single skill or one long session
can't give you. Each phase reasons without bias-bleed from the previous one, and every run is
inspectable and resumable because every phase reads/writes plain files.

> This is a **thin-spine** implementation: the durable value (prompt templates + the artifact
> contract) is plain markdown/data with zero framework coupling, and **all** Copilot SDK calls are
> isolated in a single adapter (`src/harness.ts`). When the agent harness grows a native
> multi-phase primitive, the spine collapses into config instead of being rewritten.

## How it works

| # | Phase | Fresh session | Reads | Writes |
| --- | --- | --- | --- | --- |
| 1 | research *(skipped by `--simple`)* | ✅ | the codebase | `specs/*.md`, `manifest.json` |
| 2 | assumptions | ✅ | specs (or task+code) | `assumptions.md` |
| 3 | classify *(skipped by `--simple`)* | ✅ | specs + assumptions | `classification.md`, `subitems.json` |
| 4 | research-item ×N *(skipped by `--simple`)* | ✅ each | one sub-item + specs | `research/<id>.md` |
| 5 | plan | ✅ | everything above | `plan.md` (+ machine-readable gate block) |
| 6 | implement *(staged)* | ✅ per stage | `plan.md`, `handoff.md` | code edits + `execution-log.md` + `handoff.md` |

After **any** phase's artifact validates, an optional **reflexive judge loop** runs (on by default):
a *critique* session on an **alternate model** emits high-signal feedback only, then an
*adjudicate/revise* session on the author's model decides which points to apply and rewrites the
artifact, which is then re-validated. Disable with `--no-judge`.

### Guarantees (and where they come from)

- **Fresh context per phase / retry** — every phase, fan-out item, judge session, and retry is a
  brand-new `createSession()`. This is the one piece of bespoke rigor.
- **Read-only non-impl phases** — enforced by an `onPreToolUse` hook that *denies* edit/shell tools
  in phases 1–5 (verified live during the capability spike). A post-phase git-diff check is a backstop.
- **Sanctioned writes** — phases persist artifacts only through a `write_artifact` custom tool
  (path-traversal guarded).
- **Gates** — the plan embeds a machine-readable `stages:`/`gate:` block. The implement agent runs
  each stage's `gate.commands` in-session and reports `STAGE_RESULT: pass|fail`; the orchestrator
  halts at the first reported failure. (Low-maintenance by design — see the design doc.)
- **Structured log** — `execution-log.jsonl` is orchestrator-owned with trivial secret redaction.

## Prerequisites

- Node.js `^20.19.0` or `>=22.12.0`.
- Authenticated GitHub Copilot (the SDK uses your logged-in user by default).

## Install & build

```bash
cd tools/agentic-workflow
npm install
npm run build
npm link          # exposes the `agentic-workflow` command on your PATH
```

`npm link` registers the package's `bin` so you can call `agentic-workflow` from anywhere. (Use
`npm unlink -g @azure-tools/agentic-workflow` to remove it.)

## Usage

```bash
# Headless (primary entry point) — once linked:
agentic-workflow run "<task description>" [--simple] [--no-judge] [--judge-model <model>] [--run-id <id>] [--out <dir>]

# Resume an interrupted run from its last completed phase
agentic-workflow resume [<run-id>]
```

Without linking you can still invoke it directly with `node dist/cli.js run "<task>"`, via
`npx agentic-workflow run "<task>"` from this directory, or from source during development with
`npm start -- run "<task>" --simple`.

| Flag | Effect |
| --- | --- |
| `--simple` | Skip research, classify, and per-item research → `assumptions → plan → implement`. |
| `--no-judge` | Disable the reflexive critique/revise judge loop. |
| `--judge-model <m>` | Alternate model used for the critique session. |
| `--run-id <id>` | Explicit run id (default: `YYYYMMDD-HHMM-<task-slug>`). |
| `--out <dir>` | Working-dir root (default: `./.agentic-workflow`). |

**Exit codes:** `0` done · `10` paused (resume / blocking clarification) · `1` failure · `2` usage.

### Interactive front-door (optional)

`.github/extensions/agentic-workflow/extension.mjs` registers a `/agentic-workflow` slash command
and a `run_agentic_workflow` tool so you can trigger the same headless run from inside an
interactive Copilot CLI session. It is a thin shim that imports the built orchestrator — no logic.

## Artifacts & git

Each run writes to `.agentic-workflow/<run-id>/`. The scratch tree is ignored **without modifying
the tracked root `.gitignore`** — via `.git/info/exclude` plus an inner `.agentic-workflow/.gitignore`.
Code edits from phase 6 are *real* source changes meant to be reviewed/committed (run on a branch).

## Layout

```
prompts/        the durable IP: 6 phase templates + critique.md + revise.md
src/
  harness.ts          *** the only file that imports @github/copilot-sdk ***
  session-options.ts  write_artifact tool + onPreToolUse policy + JSONL logging
  orchestrator.ts     phase sequencing, fresh-session retry, fan-out, judge loop
  gates.ts            parse the plan's machine-readable stages block
  validate.ts         plain validators (subitems.json + plan)
  artifacts.ts        run-dir, atomic write, local git-ignore
  state.ts            state.json (resume)
  cli.ts              headless entry point
```

## Develop

```bash
npm test            # vitest (mocked harness — no SDK calls, CI-safe)
npm run format:check
npm run build
```

The end-to-end test against the real SDK is opt-in (it spends model requests); see the design doc.

## Design

See [`../../agentic-workflow-design.md`](../../agentic-workflow-design.md) (full design) and the
thin-spine plan it was cut down to.
