# CLI-Extension Design: Phased Agentic Workflow

A design for running a disciplined **research → plan → implement** workflow as a single **Copilot CLI
extension** that lives inside the user's interactive session — giving the user fine-grained control
over each phase *and* an opt-in auto-runner that chains phases unattended.

## 1. Goal

Drive a multi-phase coding workflow where each phase runs with its own **pinned model**, **scoped
tools**, and **prompt**, handing off to the next phase through **artifacts on disk**. Unlike a
headless runner, the user stays in the loop: they advance phases manually, inspect artifacts between
steps, repin models, and answer decisions via dialogs — or let it auto-run when they want hands-off
progress.

The workflow phases:

| Phase | Reads | Writes | Mutates code? |
| --- | --- | --- | --- |
| `research` | the codebase | `specs/*.md` | no |
| `assumptions` | specs + task | `assumptions.md` | no |
| `classify` | specs | `subitems.json` | no |
| `research-item` ×N | one sub-item | `research/<id>.md` | no |
| `plan` | everything above | `plan.md` (+ stage/gate block) | no |
| `implement` (staged) | `plan.md`, `handoff.md` | code edits + `execution-log` | **yes** |

The durable IP is the **prompt template per phase** plus the **on-disk artifact contract**. The
extension is just the driver.

## 2. Core idea: one extension, sub-agents per phase

The extension calls `joinSession()` once and registers one **custom agent per phase**. Dispatching a
sub-agent runs it in **its own context window** — the parent session only gets the summarized result
back — so each phase reasons in isolation without a separate process or session. This replaces a
bespoke orchestrator with native SDK primitives:

- **Pinned model per phase** — `customAgents[].model`.
- **Tool scoping** — `customAgents[].tools`; non-implementation phases simply don't list
  `edit`/`create`/`write`.
- **Read-only enforcement** — `defaultAgent.excludedTools` hides mutating tools from the default
  agent so only the `implement` agent can change source.
- **Phase prompt** — `customAgents[].prompt`.

State and handoff live entirely in a per-run directory on disk, so the workflow is inspectable,
resumable, and survives an extension reload.

## 3. SDK surface used

Verified against the installed `@github/copilot-sdk` **v1.0.4** type definitions.

| Need | API | Citation |
| --- | --- | --- |
| Join the live session | `joinSession(config)` | `extension.d.ts` |
| Per-phase agent + pinned model | `customAgents: CustomAgentConfig[]` (`.model`, `.tools`, `.prompt`) | `types.d.ts:1157,1197,1626` |
| Delegation-only (mutating) tools | `defaultAgent.excludedTools` | `types.d.ts:1208` |
| Select/switch active agent | `session.rpc.agent.select({ name })` | `rpc.d.ts:2537` |
| Slash commands | `commands: CommandDefinition[]` + `handler(ctx)` | `types.d.ts:425,1424` |
| Structured dialogs | `session.ui.{confirm,select,input,elicitation}` (gate on `session.capabilities.ui?.elicitation`) | `types.d.ts:614` |
| Switch model mid-session | `session.setModel(model, opts)` | `session.d.ts:268` |
| Reclaim context | enqueue `/compact`; or `infiniteSessions` config | `rpc.d.ts:14553`, `types.d.ts:1671` |
| Artifact tools | custom `tools` (`read_artifact` marked `skipPermission`) | `types.d.ts` |

**Version note:** newer SDK releases rename slash commands to `slashCommands` with
`action(session, params)` (typed params) and adjust the elicitation result shape. On v1.0.4 use
`commands`/`handler(ctx)` and parse `ctx.args` yourself. If you upgrade, confirm the
`slashCommands`/elicitation types in `node_modules/@github/copilot-sdk/dist/*.d.ts` first.

## 4. Command surface

Each phase is a **user-triggered** command. Between commands the user can inspect artifacts, chat,
edit files, or repin models.

| Command | Params | Behavior |
| --- | --- | --- |
| `/aw-start` | `task`, `simple?` | init run-dir; dispatch the first phase |
| `/aw-run` | `from?`, `to?`, `unattended?`, `pause-at?` | **auto-run** a range of phases (§5) |
| `/aw-continue` | `n?` | single-step: run the next phase (or next `n`), then pause |
| `/aw-pause` | — | stop the auto-runner at the next phase boundary |
| `/aw-plan` / `/aw-implement` / … | — | run one specific phase |
| `/aw-judge` | `artifact` | on-demand critique→revise on an alternate-model agent |
| `/aw-redo` | `phase`, `feedback` | re-run a phase with steering notes |
| `/aw-model` | `phase`, `model` | repin a phase's model |
| `/aw-status` | — | print phase state + artifacts + `git diff --stat` |

On v1.0.4, params are parsed from `ctx.args`. Post-upgrade they become typed `slashCommands` params.

## 5. Execution modes: manual ↔ auto

Control is a spectrum; the same per-phase agents back all modes.

| Mode | How | Stops |
| --- | --- | --- |
| **Manual** | `/aw-<phase>` or `/aw-continue` | after every phase |
| **Ranged auto** | `/aw-run from:assumptions to:plan` | at the boundary or a stop condition |
| **Full auto** | `/aw-run` (or `unattended:true`) | at stop conditions (only on hard failure when `unattended`) |

The **auto-runner** is a small async function in the extension (no separate process). It walks the
phase order from `from` (default: next incomplete) toward `to` (default: `implement`); for each phase
it dispatches the sub-agent, validates the artifact, auto-retries up to N on validation failure, then
checks **stop conditions** before advancing:

1. breakpoint / `to` boundary reached,
2. `assumptions.md` flags a blocking clarification,
3. a stage gate reported failure,
4. retries exhausted,
5. `/aw-pause` was requested.

At a stop, the runner yields to the human: interactively it shows a `session.ui` dialog (retry / skip
/ abort a gate; resolve a blocking assumption; pick which critique points to apply) and resumes per
the answer. With `unattended:true` it auto-resolves with safe defaults and only halts on hard
failure.

The loop is cooperative — it checks the pause flag at each boundary, so a long auto-run stops cleanly
without killing the session; `session.abort()` cancels an in-flight phase. Because all state is in the
run-dir, the user can switch between auto and manual at any boundary
(`/aw-run` → inspect → `/aw-continue` → `/aw-run to:implement`).

`/aw-run` with no params = "advance to the end, pausing only at gates and blocking assumptions" —
autonomous where safe, interactive where judgment is needed.

## 6. Trade-offs and gotchas

- **Not zero-shared-lineage.** Sub-agents isolate working context, but the parent accumulates each
  phase's handoff summary. Fine for almost all workflows; use `/compact` or `infiniteSessions` for
  long runs.
- **Single foreground session.** `joinSession` binds to one session — don't use `/new` to reset
  phases; `/clear` reloads the extension and wipes in-memory state (so keep all state on disk).
- **One hooks owner.** If multiple extensions register hooks, only the last-loaded fires —
  consolidate hooks into this one extension.
- **Protocol hygiene.** `.mjs` only; log to **stderr** (stdout corrupts JSON-RPC); never
  `session.send()` synchronously from a prompt hook.

## 7. Layout

```
.github/extensions/agentic-workflow/
  extension.mjs        # joinSession: customAgents + commands + artifact tools + auto-runner
  prompts/             # one prompt template per phase (the durable IP)
```

A single file plus the prompt templates. No orchestrator, no state machine — the SDK's sub-agents,
slash commands, and elicitation dialogs do the work.
