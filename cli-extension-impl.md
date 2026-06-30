# Implementation Plan: CLI-Extension Phased Workflow

Step-by-step plan to build the design in `cli-extension-design.md` as a **self-contained, agent-first**
Copilot CLI extension that **replaces** the `tools/agentic-workflow` prototype.

Design principle: **lean on the agent, minimize deterministic code.** Validation, gate-running, stage
breakdown, and artifact I/O are delegated to the phase agents. The extension is essentially *config +
thin command handlers + a tiny dispatch loop* — no `lib/` of validators/parsers, no build step
(extensions are `.mjs`).

Target layout:

```
.github/extensions/agentic-workflow/
  extension.mjs     # joinSession: agents + commands + auto-runner loop
  package.json      # @github/copilot-sdk dependency
  prompts/          # one .md per phase (+ critique) — the durable IP
  README.md
```

The agent reports status by ending each phase with a sentinel line the loop reads:

```
PHASE_RESULT: pass | fail | needs_input
```

(optionally followed by a short reason). That single convention replaces deterministic validators and
gate parsers.

## Step 0 — Prerequisites

- Create `.github/extensions/agentic-workflow/package.json` (`{ "type": "module" }` +
  `@github/copilot-sdk` dependency); `npm install` there.
- **SDK version:** plan targets **v1.0.4** (`commands` + `handler(ctx)`, `session.ui.*`). If you pin a
  newer release, re-check `slashCommands`/elicitation types in `node_modules/.../dist/*.d.ts` and
  adjust Steps 3–4.
- **Verify:** `node -e "import('@github/copilot-sdk/extension').then(()=>console.error('ok'))"`.

## Step 1 — Prompt templates (the durable IP)

- **Files:** `prompts/{01-research,02-assumptions,03-classify,04-research-item,05-plan,06-implement,
  critique}.md`.
- **Source:** **start from the prototype's `tools/agentic-workflow/prompts/*.md`** — that reasoning
  content (what each phase investigates, produces, and hands off) is the durable IP and should be
  reused. **Strip only the deterministic-handoff scaffolding** that no longer applies:
  - references to the `write_artifact`/`read_artifact` custom tools and the run-context preamble →
    replace with "write/read this file in the run dir with your normal file tools";
  - the requirement to emit a **machine-readable `stages:`/`gate:` block** for the orchestrator to
    parse → replace with "define your stages in prose and **run the gate commands yourself**";
  - any wording assuming a fresh isolated session or external validator → replace with the
    `PHASE_RESULT` self-report below.
- **Work:** carry over each phase's instructions, apply the strip above, keep `{{task}}`/
  `{{priorErrors}}` placeholders, and have each template tell the agent to:
  - write its output to the run dir (a path the extension passes in, e.g. `.aw/<run>/plan.md`) using
    normal file tools, and read prior artifacts the same way;
  - **self-check** its own output and the `06-implement` template additionally **run the gate commands
    it defined in the plan**;
  - end with `PHASE_RESULT: pass|fail|needs_input` (+ reason). Use `needs_input` for blocking
    questions.
- **No deterministic validator** backs these — the agent owns correctness.
- **Verify:** each template's placeholders are supplied by its caller (Step 4); each instructs the
  sentinel line.

## Step 2 — Register the session (agents = the real value)

- **Files:** `extension.mjs`.
- **Work:** `await joinSession({...})` with:
  - `customAgents`: one per phase — **pinned model + scoped tools + the phase prompt** (the whole
    reason to be an extension):

    | agent | model | tools | template |
    | --- | --- | --- | --- |
    | `aw-research` | `claude-sonnet-4.5` | read/shell | `01-research` |
    | `aw-assumptions` | default | read | `02-assumptions` |
    | `aw-classify` | `claude-haiku-4.5` | read | `03-classify` |
    | `aw-research-item` | `claude-sonnet-4.5` | read | `04-research-item` |
    | `aw-plan` | `claude-sonnet-4.5` | read | `05-plan` |
    | `aw-implement` | `claude-sonnet-4.5` | **all** (incl. edit/create/shell) | `06-implement` |
    | `aw-critique` | `claude-haiku-4.5` | read | `critique` |
  - `defaultAgent: { excludedTools: ["edit","create","delete","write"] }` — the one cheap guardrail
    so non-implement phases can't mutate source (config, not code). Optional; drop if you want zero
    enforcement.
  - keep a module-scoped `session` ref; **log to stderr only**.
- **Verify:** `/extensions info agentic-workflow` lists the seven agents.

## Step 3 — Dispatch loop + thin command handlers

- **Files:** `extension.mjs`.
- **Work:**
  - **Run dir / current phase** — on `/aw-start`, derive a run dir from the task; track the active run
    dir in memory. Determine the next phase by which artifact files already exist in the run dir (a
    few `fs.existsSync` checks) — no state machine, no `state.json`.
  - **`runPhase(phaseId, { priorErrors })`** — `session.rpc.agent.select({ name })`, then
    `await session.sendAndWait({ prompt: task + "\n\n" + readFile(prompts/<template>).replaceAll(...) })`.
    Read the final assistant text, match `PHASE_RESULT:\s*(pass|fail|needs_input)`. Return that.
    (~15 lines; the inline `replaceAll` is the entire "templating".)
  - **`commands`** — thin `handler(ctx)` each parsing `ctx.args` (tiny `key:value`/free-text split):
    - `/aw-start task [simple]` → set run dir → `runPhase(first)`.
    - `/aw-research … /aw-implement` → `runPhase(phase)`.
    - `/aw-continue [n]` → run next `n` phases (default 1).
    - `/aw-judge artifact` → run `aw-critique`, then re-run the author phase with the critique as
      `priorErrors`.
    - `/aw-redo phase feedback` → `runPhase(phase, { priorErrors: feedback })`.
    - `/aw-model phase model` → update the registry entry / `session.setModel` for next dispatch.
    - `/aw-status` → list run-dir artifacts + `git diff --stat`.
- **Verify:** `/aw-start … → /aw-assumptions → … → /aw-implement` completes, producing artifacts and
  real code edits.

## Step 4 — Auto-runner + elicitation

- **Files:** `extension.mjs` (`autoRun({from,to,unattended,pauseAt})`, `/aw-run`, `/aw-pause`).
- **Work:** loop over the phase order from `from` (default: next incomplete) to `to` (default
  `implement`). For each: `runPhase`, then branch on the returned `PHASE_RESULT`:
  - `pass` → advance (unless breakpoint/`to`/`/aw-pause`);
  - `fail` → retry up to N with the agent's reason as `priorErrors`; if still failing, stop;
  - `needs_input` → **stop and ask the human**.
  When stopping for input/gate/judge and `session.capabilities.ui?.elicitation` is available and not
  `unattended`, use `session.ui.select/confirm/elicitation` (e.g. retry/skip/abort, or collect a
  blocking answer and feed it back via the next `runPhase`). With `unattended:true` or no UI, auto-
  resolve with safe defaults. `/aw-pause` sets a flag checked each iteration; `session.abort()`
  cancels an in-flight phase.
- **Verify:** `/aw-run to:plan` runs research→plan and halts; a `needs_input` phase pops a dialog;
  `unattended:true` runs straight through.

## Step 5 — Context hygiene

- **Files:** `extension.mjs`.
- **Work:** `/aw-compact` (and an automatic check before `implement`) →
  `session.rpc.commands.enqueue({ command: "/compact" })`; optionally
  `infiniteSessions: { enabled: true }` in the join config. Artifacts on disk are the source of truth,
  so resuming after `/clear` works by re-deriving the next phase from existing files.
- **Verify:** `/aw-compact` reclaims context; interrupt + re-`/aw-start` same task resumes correctly.

## Step 6 — Replace the prototype, document

- **Work:**
  - Delete `tools/agentic-workflow/`; remove it from the root `README.md` index and
    `.github/CODEOWNERS`.
  - Write `.github/extensions/agentic-workflow/README.md`: purpose, install, command list, manual/auto
    modes, and the `PHASE_RESULT` convention.
  - Optional light test: a mocked-`session` smoke test for arg parsing + the `PHASE_RESULT` branch
    logic (the only non-trivial pure code).
- **Verify:** one real end-to-end run on a throwaway task (on a branch) produces reviewable edits.

## What is code vs delegated to the agent

| Delegated to the agent (no script) | Irreducible code (config + glue) |
| --- | --- |
| Plan/subitems correctness (self-check) | `customAgents` registry (pinned models + tool scoping) |
| Running gate commands (implement agent) | `defaultAgent.excludedTools` guardrail (config) |
| Stage breakdown / sequencing within a phase | thin `commands` handlers + arg parsing |
| Reading/writing artifacts (normal file tools) | `runPhase` dispatch + `PHASE_RESULT` parse (one regex) |
| Classification, research fan-out | `autoRun` loop + stop branching |
| Judging critiques | `session.ui` elicitation calls |

No `lib/` of validators, gate parsers, artifact tools, or a state machine — just `extension.mjs` +
`prompts/`.

## Milestone ordering

1. Steps 0–2 — extension loads with the seven pinned-model agents.
2. Step 3 — manual single-step workflow end to end.
3. Step 4 — auto-run + elicitation.
4. Steps 5–6 — context hygiene, prototype removal, docs.

Steps 1–3 are the critical path; 4–6 layer on top.

## Using the extension across multiple repos

The extension is plain files + a `package.json`, so distribution is a *placement* decision, not a code
change. Three options, lowest to highest reach:

| Scope | Where it lives | Reach | Trade-off |
| --- | --- | --- | --- |
| **Repo-local** | `.github/extensions/agentic-workflow/` (committed) | just this repo; everyone who clones it | versioned with the repo, but copy-per-repo |
| **User-scoped** | a user-global extensions dir (scaffold with `location: "user"`) | every repo *you* open, no per-repo setup | personal only; not shared with teammates |
| **Shared/published** | a git repo or npm package teammates install into their user scope | every repo for everyone who installs | needs a publish/update path |

Recommended approach for "same extension, many repos":

1. **Develop repo-local** here (fast `/extensions reload` iteration).
2. **Promote to user scope** once stable — scaffold/copy it to the user extensions location
   (`extensions_manage({ operation: "scaffold", name: "agentic-workflow", location: "user" })`, or copy
   the folder there). It then loads in **any** repo you open, with no `.github/extensions/` entry.
   *(Confirm the exact user-extensions path for your CLI version via `/extensions info`.)*
3. **For a team**, put the extension's folder in its own git repo (or npm package) and have each member
   install it into their user scope; pin a version and update via `git pull` / `npm update` +
   `/extensions reload`.

Make it repo-agnostic so it works anywhere:

- **No hard-coded repo paths.** Derive the run dir from `process.cwd()` (the repo the user is in),
  e.g. `<cwd>/.aw/<run>/`; bundle `prompts/` via `import.meta.url` (already the plan).
- **Resolve the SDK robustly.** A user-scoped extension needs `@github/copilot-sdk` resolvable from its
  own `node_modules` (keep its `package.json` + installed deps alongside `extension.mjs`).
- **No repo-specific assumptions in prompts.** Keep phase prompts about *general* research/plan/
  implement reasoning; let the agent discover repo specifics at runtime. Repo-specific guidance belongs
  in that repo's own `.github/` instructions, not this extension.
- **Tolerate non-git / empty repos** — `/aw-status`'s `git diff` and resume-by-file-existence should
  degrade gracefully.

## Rollout plan

Ship incrementally; keep the prototype until the extension reaches parity.

1. **Spike (repo-local, behind nothing).** Land Steps 0–3 in `.github/extensions/agentic-workflow/`.
   Validate the manual flow (`/aw-start` → … → `/aw-implement`) on a throwaway branch. Prototype stays.
2. **Feature-complete.** Add Steps 4–5 (auto-run, elicitation, compaction). Dogfood on real tasks in
   this repo; compare output quality against the prototype's headless runs.
3. **Parity sign-off.** When the extension reliably produces reviewable edits with the control + auto
   modes, do Step 6: **delete `tools/agentic-workflow/`**, update the root `README.md` index and
   `.github/CODEOWNERS`, and add the extension `README.md`. This is the cutover.
4. **Promote to user scope (optional).** Copy the validated extension to the user extensions location
   so it follows you across repos; verify it loads in a second, unrelated repo.
5. **Team rollout (optional).** Move the folder to a shared repo/package, document install +
   `/extensions reload`, pin a version, and announce. Gather feedback and iterate on prompts (the only
   thing that usually needs tuning).

Rollback at any stage is trivial: `/extensions disable agentic-workflow` (or, pre-cutover, fall back
to the still-present prototype).
