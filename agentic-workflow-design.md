# Agentic `research → plan → implement` workflow — design & implementation plan

## 1. Problem

When implementing features with AI agents, the highest-quality output comes from a
disciplined, multi-phase workflow where each phase runs in a **fresh agent session** (clean
context, no bias bleed from earlier reasoning) and hands off to the next phase through
**durable artifacts on disk**. Doing this by hand — opening new sessions, copying context,
re-prompting — is tedious and error-prone.

**Goal:** chain the whole workflow behind a *single entrypoint* so one command takes a task
description and drives research → assumptions → classification → per-item research → plan →
implementation, producing reviewable artifacts at every step.

## 2. Why the GitHub Copilot SDK (not a skill, not raw CLI)

The workflow's hard requirement is **one fresh, isolated context per phase**. Evaluated
options:

| Approach | Fresh context per phase? | Notes |
| --- | --- | --- |
| Single skill (`SKILL.md`) | ❌ | A skill runs inside one conversation/context. Can't isolate phases. |
| Sub-agents (`Task`/`/fleet`) | ✅ | Works, but orchestration logic lives in a long-lived parent context; gating/retries/parallelism are awkward to express. |
| Headless CLI (`copilot -p`) | ✅ | Works, but you hand-roll process lifecycle, parsing, retries. |
| **Copilot SDK** | ✅ | `client.createSession()` = a fresh isolated session, first-class. Same agent runtime as the CLI, exposed programmatically. |

The [Copilot SDK](https://github.com/github/copilot-sdk) wins: it exposes the same agent
engine behind the CLI over JSON-RPC, manages the CLI process lifecycle automatically, and is
available for TS/Python/Go/.NET/Java/Rust. **We will build in TypeScript** (`@github/copilot-sdk`),
which bundles the CLI automatically and has the richest surface.

What the SDK gives us that a script/skill can't easily:

- **Fresh context per phase** — one `createSession()` per phase, on purpose.
- **Per-phase model selection** — cheap model for classification, strong model for plan/implement.
- **Custom tools** (`defineTool`) — give the agent a `write_artifact` / `validate_subitems`
  tool so it *cannot* skip emitting an artifact or produce a malformed one.
- **Programmatic gating & retries** — our code inspects each artifact (exists? parses? schema-valid?)
  before advancing, and re-runs a phase on bad output.
- **Parallel fan-out** — phase 4 spins up N concurrent sessions, one per sub-item.
- **Structured event capture** — subscribe to session events to auto-write the execution log,
  token usage, and tool-call traces.
- **Checkpoint / resume** — persist "phase 3 complete" so a failed run resumes instead of
  restarting from scratch.

### SDK API surface we rely on (verified from SDK docs)

```ts
import { CopilotClient, defineTool } from "@github/copilot-sdk";

const client  = new CopilotClient();                 // manages CLI process lifecycle
const session = await client.createSession({         // FRESH isolated context
  model: "gpt-4.1",
  streaming: true,
  tools: [/* defineTool(...) */],
  // onPermissionRequest: approveAll  // for autonomous runs (verify exact option name)
});
session.on("assistant.message_delta", e => process.stdout.write(e.data.deltaContent));
session.on("session.idle", () => {});
const res = await session.sendAndWait({ prompt });
await client.stop();
```

> Open item: confirm the exact TS option for auto-approving tool permissions in autonomous runs
> (Python exposes `PermissionHandler.approve_all`), **and** whether built-in file/shell tools can
> be scoped/disabled per session (the §6.1 enforcement model depends on it). Both are validated in
> the M0 capability spike (§11) before further build-out.

## 3. The artifact contract (the heart of the system)

Each run gets a working directory: `.agentic-workflow/<run-id>/` (see §3.1). Phases communicate
**only** through files in it. This makes every run inspectable, resumable, and lets us improve
any phase's prompt independently.

| # | Phase | Fresh session | Reads | Writes |
| --- | --- | --- | --- | --- |
| 1 | research *(skippable, §3.2)* | ✅ | the codebase | `specs/architecture.md`, `specs/functional.md`, `specs/apispec.md` (only if an API definition is relevant to the task) |
| 2 | assumptions | ✅ | phase 1 specs *(or task + codebase if research skipped)* | `assumptions.md` |
| 3 | classify + split *(skippable, §3.2)* | ✅ | specs + assumptions *(or task + codebase if research skipped)* | `classification.md`, `subitems.json` |
| 4 | research-item (loop ×N, parallel) *(skippable, §3.2)* | ✅ each | one sub-item + specs | `research/<item-id>.md` |
| 5 | plan | ✅ | specs + assumptions + all item research *(whatever exists)* | `plan.md` (structured, with checkpoint gates — see §5) |
| 6 | implement *(staged, §5)* | ✅ per stage | `plan.md`, `execution-log.md`, `handoff.md` | code edits + `execution-log.md` + `handoff.md` (cross-stage memory, §6.3) |

`specs/apispec.md` is **conditional**: phase 1 produces it only when the task touches an API
surface (REST/RPC/SDK contract, schema, public interface). It captures the relevant existing
API definition (endpoints/operations, request/response shapes, contracts) so later phases reason
about the real interface. To remove the "not relevant vs. forgotten/failed" ambiguity, phase 1
**always records the decision explicitly** in a per-run `manifest.json`, e.g.:

```jsonc
{
  "apispec": { "required": false, "reason": "Task does not touch any public API surface" }
}
```

Downstream phases read the manifest (not the mere presence/absence of the file) to decide
whether API concerns apply.

### 3.1 Artifact storage & gitignore (not checked in by default)

Run artifacts are **scratch by default** — they must not be committed unless the user opts in.

- **Default location:** a single temp root `.agentic-workflow/` at the repo root, with one
  subdirectory per run: `.agentic-workflow/<run-id>/`. (Leading dot keeps it out of the way;
  a dedicated root — rather than reusing `.workflow/` — makes the ignore rule unambiguous.)
- **Auto-ignore without mutating tracked files:** the tool ignores the scratch tree **without
  modifying the repo's tracked root `.gitignore`** (which would itself be a source-tree change
  before phase 6 ever runs). Two mechanisms, in priority order:
  1. **Local ignore (default):** add `.agentic-workflow/` to `.git/info/exclude`, which is a
     local, untracked ignore file — no committed change to the repo.
  2. **Self-contained inner `.gitignore`:** also write an inner `.agentic-workflow/.gitignore`
     containing `*` and `!.gitignore`, so the whole tree is ignored and travels with the
     directory (and the ignore file itself can be tracked if a user opts to commit artifacts).

  Mutating the repo-root `.gitignore` is **opt-in only** (`--write-root-gitignore`), never
  automatic. Both steps no-op if already present or if the repo isn't a git repo.
- **Code edits are the exception:** phase 6 edits real source files in the repo — those are
  *meant* to be committed (ideally on a feature branch via `--branch`). Only the
  artifacts/logs under `.agentic-workflow/` are ignored, never the implementation changes.
- **Opt-in to keep artifacts:** `--out <dir>` redirects the working-dir root to a committed
  location (e.g. `docs/specs/<feature>/`) when the artifacts themselves are a deliverable. When
  `--out` points outside `.agentic-workflow/`, the tool does **not** auto-ignore it (the user is
  explicitly choosing to track those files).
- **Cleanup:** `agentic-workflow clean [<run-id>]` removes a run's scratch dir; `--keep-days <n>`
  config prunes old runs. Since it's all under the ignored root, deletion never affects git state.

Resolution order for the working-dir root: `--out` flag → `AGENTIC_WORKFLOW_OUT` env var →
config file → default `.agentic-workflow/`.

### 3.2 Skipping phases for simple tasks (`--skip-research`, `--skip-classify`)

For small, well-understood tasks, some phases are pure overhead. Two independent skip flags let
the user trade rigor for tokens. Both record the decision in `manifest.json` (so `status` and
downstream phases can tell "skipped" apart from "missing/failed"), and both keep the plan phase's
full structured `plan.md` (incl. machine-readable gates) — skipping is an *input* shortcut, never
a reduction of plan rigor.

**`--skip-research[=all|specs]`** (alias `--no-research`) — omit **phase 1** spec generation.

- **What's skipped:** phase 1 writes no `specs/*`; `manifest.json` records
  `{ "research": { "skipped": true, "reason": "<from --reason or 'user opted out'>" } }`.
- **Downstream adaptation:** phases that normally read `specs/*` (assumptions, classify, plan)
  fall back to reading the **task description + the codebase directly**. Their prompt templates
  branch on `manifest.research.skipped`.
- **Granularity:** `all` (default when no value) skips phase 1 **and** phase-4 per-item research
  (the same "deep research" cost); `specs` skips only phase 1 but keeps phase-4. Bare
  `--skip-research` ≡ `--skip-research=all`.
- **API safety valve:** skipping research loses `apispec.md`. The classify/plan prompts must still
  set `manifest.apispec` from a quick inspection; if they detect likely API impact, they emit a
  `blocking: true` assumption (§5) so the run pauses before proceeding without an API spec.

**`--skip-classify`** (alias `--no-classify`) — omit **phase 3** classification & splitting for
tasks that are obviously a single, atomic unit of work.

- **What's skipped:** no `classification.md`; the orchestrator **synthesizes a trivial
  `subitems.json`** with exactly one item (the whole task) so the schema/contract downstream
  phases rely on is preserved. `manifest.json` records
  `{ "classify": { "skipped": true, "reason": "..." } }`.
- **Single-item synthesis:** the lone item gets `id: "main"`, `type` defaulted to the run's
  `--type` flag if given (else `"feature"`), `dependsOn: []`, `overlapRisk: "low"`, and
  `expectedFilesOrAreas`/`acceptanceCriteria` left to the plan phase to infer.
- **Phase-4 collapse:** with one item, the fan-out is a single (optional) research session, not a
  parallel set — and is itself skipped when `--skip-research[=all]` is also set.
- **Plan reconciliation note:** the "research reconciliation" section of `plan.md` (§5) is a no-op
  when there's a single item; the plan template notes this rather than inventing overlaps.

**Combining for the smallest tasks:** `--skip-research --skip-classify` reduces the pipeline to
**assumptions → plan → implement** (a typo fix, a one-line guard, etc.). The orchestrator
**warns** (does not error) if either skip is combined with `--autopilot` on a task that turns out
to be larger than expected — e.g. classify is skipped but the plan phase proposes edits across
many unrelated areas — since skipping raises the risk of an under-informed plan.

**`--simple` (convenience preset).** Because skipping both research and classify is the common
"this is a small task" case, `--simple` is a single flag that **defaults both
`--skip-research=all` and `--skip-classify` on**, yielding the assumptions → plan → implement
pipeline. It's purely a preset:

- Equivalent to `--skip-research --skip-classify`; `manifest.json` records the same `skipped`
  entries (with reason `"--simple"` unless `--reason` overrides).
- **Individual flags override the preset.** e.g. `--simple --no-skip-research` keeps research but
  still skips classify; `--simple --skip-research=specs` downgrades the research skip to specs-only.
  (Each skip exposes a `--no-skip-*` negation so the preset can be partially re-enabled.)
- Does **not** imply `--autopilot` — mode is still chosen separately; `--simple` only changes which
  phases run, not whether the run pauses for review.

### `subitems.json` schema (sketch)

```jsonc
{
  "task": "string — original task description",
  "classification": "feature | bug | refactor | mixed",
  "items": [
    {
      "id": "kebab-case-id",
      "type": "feature | bug | refactor",
      "title": "short title",
      "description": "what this sub-item covers",
      "rationale": "why it's a separate item",
      "dependsOn": ["other-item-id"],          // ordering / dependency awareness
      "expectedFilesOrAreas": ["src/api/**"],  // where this item is expected to touch
      "acceptanceCriteria": ["..."],           // how we know this item is done
      "nonGoals": ["..."],                     // explicitly out of scope for this item
      "overlapRisk": "low | medium | high"     // risk of conflicting with sibling items
    }
  ]
}
```

The classify prompt must aim for **independent, non-overlapping** sub-items and populate
`dependsOn`/`overlapRisk` honestly. These fields let phase 4 order/parallelize safely and let
phase 5 reconcile sibling research (see §5, "research reconciliation").

## 4. Proposed code layout (to be built in a later pass)

Following this repo's tool convention (`/tools/<name>/` self-contained with README, tests, CI):

```
tools/agentic-workflow/
├── README.md                     usage + design summary
├── package.json / tsconfig.json / .gitignore
├── ci.yml                        (from eng/pipelines/templates — follow-up)
├── prompts/                      versioned per-phase prompt templates (the IP)
│   ├── 01-research.md
│   ├── 02-assumptions.md
│   ├── 03-classify.md
│   ├── 04-research-item.md
│   ├── 05-plan.md
│   └── 06-implement.md
├── schema/
│   └── subitems.schema.json
└── src/
    ├── index.ts        CLI entrypoint: parse task → run workflow
    ├── orchestrator.ts phase sequencing, checkpoint/resume, gate enforcement
    ├── phase.ts        runPhase: createSession → render prompt → sendAndWait → validate
    ├── policy.ts       per-phase tool/filesystem policy + post-phase git-diff guard (§6.1)
    ├── gates.ts        parse plan stages/gates; run gate commands; check expected_files scope (§6.2)
    ├── prompts.ts      load + render templates (var substitution)
    ├── artifacts.ts    working-dir mgmt, atomic read/write, hashing, run lock, state file
    ├── tools.ts        custom SDK tools: write_artifact, validate_subitems
    ├── config.ts       per-phase model + options
    └── types.ts        shared types (Phase, SubItem, RunState, Stage, Gate…)
```

### Orchestrator flow (pseudocode)

```ts
const client = new CopilotClient();
const ctx = workingDir(slug(task));            // .agentic-workflow/<run-id>/
await acquireRunLock(ctx);                     // prevent concurrent resume/approve (§9.4)
const state = loadCheckpoint(ctx);             // resume support

await phase(client, "research",     { reads: [],                          writes: ["specs/architecture.md","specs/functional.md","manifest.json"], policy: READ_ONLY });
await phase(client, "assumptions",  { reads: ["specs/*"],                 writes: ["assumptions.md"], policy: READ_ONLY });
const items = await phase(client, "classify", { reads: ["specs/*","assumptions.md"], writes: ["subitems.json"], validate: subitemsSchema, policy: READ_ONLY });

// parallel, FRESH session each, bounded concurrency, each item write-scoped to its own note
await mapWithConcurrency(items, config.concurrency, it =>
  phase(client, "research-item", { item: it, writes: [`research/${it.id}.md`], policy: researchItemPolicy(it) }));

await phase(client, "plan", { reads: ["specs/*","assumptions.md","research/*"], writes: ["plan.md"], validate: planSchema, policy: READ_ONLY });

// Phase 6 is STAGED: each plan stage runs in its own fresh sub-session, gated by the orchestrator.
// Fresh ≠ blank: the orchestrator curates a context pack so no inter-stage knowledge is lost (§6.3).
const stages = parsePlanStages("plan.md");     // structured stages + machine-readable gates (§6.2)
for (const stage of stages) {
  const pack = buildContextPack(ctx, stage);   // plan.md + prior handoffs + cumulative diff + dep files (§6.3)
  await phase(client, "implement-stage", {
    stage, contextPack: pack,
    reads: ["plan.md", "execution-log.md", "handoff.md"],   // durable cross-stage memory
    appends: ["execution-log.md", "handoff.md"],            // each stage writes a handoff for the next
    updates: ["plan.md"], policy: implementPolicy(stage) });
  const dev = checkDeviation(stage);            // edits outside expected_files must be documented (§5)
  if (dev.undocumented) { markPaused(state, stage, dev); break; }  // missing docs → pause for review
  const result = runGate(stage.gate);           // ORCHESTRATOR runs gate cmds, checks exit codes
  appendGateResult("execution-log.md", result);
  if (!result.passed) { markFailed(state, stage); break; }   // halt at the failing gate
  checkpoint(state, stage);
}

await client.stop();
```

`phase()` responsibilities:
1. `client.createSession({ model: config[phase].model, tools, onPermissionRequest, policy })`
2. Render the phase's prompt template with run vars (task, ctx paths, item).
3. `session.sendAndWait({ prompt })`, streaming events into the execution log.
4. **Validate** the expected output artifact(s): exists → parses → schema-valid.
5. **Enforce policy (§6.1):** for non-implementation phases, assert `git diff` is empty (no
   source mutation) and that only the declared artifact paths were written; fail the phase if not.
6. On failure: retry up to N times by spawning a **fresh session each time** (preserving the
   clean-context guarantee), seeding it with the original inputs **plus** the validator/policy
   errors — never by continuing the failed session's transcript. Else abort the run.
7. Persist checkpoint (`state.json`) atomically: phase/item status + artifact hashes.

## 5. Per-phase prompt design

Each template is a small, versioned markdown file with explicit I/O contract. Pattern:

- **Role/goal** for the phase.
- **Inputs**: exact file paths to read (no guessing).
- **Output**: exact file path(s) to write, required sections, format.
- **Constraints**: stay in scope; don't implement during research; cite code locations; etc.

Examples of the intent per phase:

- **01-research** *(skippable via `--skip-research`, §3.2)* — produce architecture + functional
  specs of the *current* code relevant to the task. Read-only exploration; no design proposals
  yet. **Conditionally** produce `specs/apispec.md` when the task touches an API surface
  (REST/RPC/SDK contract, schema, public interface), documenting the relevant existing API
  definition.
- **02-assumptions** — from the specs, enumerate baseline assumptions, unknowns, and risks the
  later work will rely on. One assumption per line with rationale/confidence. **Clarification
  stop:** if any assumption is *low-confidence and affects correctness, security, or API
  behavior*, the prompt must flag it as `blocking: true`; the orchestrator then **pauses and asks
  the user** before proceeding to classify/plan rather than inventing an answer (see §9.1, applies
  in every mode including autopilot).
- **03-classify** *(skippable via `--skip-classify`, §3.2)* — classify the task as
  bug/feature/refactor (or mixed) and split into **independent, non-overlapping** sub-items; emit
  `subitems.json` (schema-validated, incl. `dependsOn`/`overlapRisk`/`expectedFilesOrAreas`) + a
  human-readable `classification.md`. When skipped, the orchestrator synthesizes a single-item
  `subitems.json` instead of running this phase.
- **04-research-item** — deep, isolated research on a single sub-item, with specs as context;
  output a focused research note. Runs once per item, in parallel (ordered by `dependsOn`).
- **05-plan** — consume all research and produce a highly detailed, structured implementation
  plan. `plan.md` must contain these sections, in order:
  0. **Research reconciliation** — reconcile the N independent phase-4 notes: call out overlaps,
     contradictions, and duplicated work, and state the single coherent strategy chosen. (Comes
     first because the rest of the plan depends on it.)
  1. **Decisions and rationale** — the choices made and *why*.
  2. **End-to-end approach** — the overall strategy, and explicitly how the success criterion
     will be *proved*.
  3. **Step-by-step implementation plan** — ordered, concrete steps (file-by-file changes).
     Steps are grouped into stages, each ending in a **checkpoint gate** (see below).
  4. **Stop/go gates** — explicit points where work pauses for validation before continuing.
  5. **Validation plan** — tests to run, tests to add, and observability checks.
  6. **Rollout strategy** — how the change is shipped/enabled.
  7. **Rollback plan** — how to safely revert.
  8. **Risks and mitigations**.
  9. **Definition of done** — concrete, checkable completion criteria.
  10. **Open questions**.
  11. **Out-of-scope observations** — things noticed but deliberately not addressed.
  12. **Plan changes** — initially empty; phase 6 appends entries here whenever implementation
      deviates from the plan (what changed, why, impact), so `plan.md` stays the source of truth.

  **Machine-readable gates (required).** Beyond the prose, the plan must embed a structured,
  machine-parseable block (validated by `planSchema`) that the orchestrator — not the agent —
  enforces at runtime:

  ```yaml
  stages:
    - id: stage-1
      expected_files: ["src/foo.ts", "test/foo.test.ts"]  # the plan's best guess at scope (advisory)
      context_needed: ["src/bar.ts", "src/types.ts"]      # existing files/symbols this stage depends on
      steps:
        - { id: "1.1", description: "..." }
      gate:
        id: gate-1
        commands: ["npm test -- foo"]      # orchestrator runs these
        expected: exit_code_0              # pass criterion
  ```

  Each stage names its `expected_files` (the plan's anticipated scope — **advisory, not a hard
  wall**, see phase 6), the `context_needed` files/symbols it depends on (seeded into the stage's
  context pack, §6.3), and a `gate` with concrete validation `commands` + expected result.
  Free-form prose gates alone are insufficient because they're unenforceable.
  **Stage sizing for context safety:** the plan phase must split work into stages that are
  **cohesive and loosely coupled** — each independently completable and verifiable by its gate —
  to minimize what must cross a session boundary. Tightly-coupled changes that only make sense
  together belong in **one** stage, not split across two.
- **06-implement (staged).** Implementation is **not** one big session. Each plan *stage* runs in
  its **own fresh sub-session** (`implement-stage-1 → gate → implement-stage-2 → …`), preserving
  the clean-context guarantee inside the riskiest phase and bounding context growth. Per stage:
  - `expected_files` is the **anticipated** scope, not a lock. Implementation legitimately
    surfaces plan gaps — the agent **may edit beyond `expected_files`** when needed to do the work
    correctly, provided the deviation is **documented** (see deviation policy) and the scope is
    **justified** in the execution log. The orchestrator does not hard-block out-of-scope edits.
  - The agent appends to `execution-log.md`. For **every action** the log must (a) **justify the
    scope** — why it was necessary — and (b) **map it to a concrete step** in `plan.md` (by
    stage/step id).
  - **Stage handoff (cross-stage memory).** Before its gate, each stage appends a concise entry to
    `handoff.md` for the *next* stage: what it built, the **new/changed public symbols and files**,
    decisions or conventions established, anything intentionally deferred, and known follow-ups. The
    **next** stage's prompt is seeded with this (plus the cumulative diff and `context_needed`
    files, §6.3) so reasoning/intent — not just code — carries forward across the session boundary.
  - **Gate enforcement is the orchestrator's job, not the agent's:** after the sub-session ends,
    the orchestrator runs the stage's `gate.commands`, checks `expected`, records pass/fail +
    output in the log, and **halts at the first failing gate** rather than continuing.
  - **Deviation policy (documented, not blocked):** deviating from the plan is **allowed** — it's
    often the *correct* response to a gap discovered mid-implementation. The requirement is
    transparency, not prevention:
    - The agent appends a **"Plan changes"** entry to `plan.md` describing what changed and why
      (e.g. additional files touched, a step reordered, an approach adjusted), keeping `plan.md`
      the source of truth.
    - The `execution-log.md` entry for the deviating action **justifies its scope** and maps it to
      the originating step / plan-change entry.
    - The orchestrator **detects** edits outside `expected_files` and **verifies the deviation was
      documented** (a Plan-changes entry + a scope justification exist); if an out-of-scope edit
      has no documentation, *that* is the failure — it pauses for review, not the edit itself.
    - **Larger deviations** — a change to **architecture, public API, or overall test strategy** —
      still **stop** and either request approval (review mode) or, if the divergence invalidates
      the plan, return to the plan phase. The line is "did this break the plan's foundation?",
      not "did this touch an unlisted file?". Silent, *undocumented* scope expansion remains
      disallowed.

## 6. Custom SDK tools (reliability)

- `write_artifact(path, content)` — the only sanctioned way for a phase to emit its artifact;
  lets us guarantee the file lands in the working dir with the right name (with path-traversal /
  normalization checks so a phase can't escape its scope).
- `validate_subitems(json)` — validates phase-3 output against the schema and returns errors so
  the agent can self-correct via its own tool call **within its turn** before the orchestrator
  gates. (This is in-turn validation feedback, not a phase-level retry — phase retries still
  spawn a fresh session per §4.)

### 6.1 Per-phase tool & filesystem policy (enforced, not requested)

The "phases communicate only through files" guarantee must be **enforced by the orchestrator**,
not merely asked of the agent. Each phase runs under an explicit policy:

| Phase(s) | Repo source | Artifact writes | Network/shell |
| --- | --- | --- | --- |
| 1 research, 2 assumptions, 3 classify, 5 plan | **read-only** | only its declared artifacts via `write_artifact` | shell/network disabled (research may read code, not run it) |
| 4 research-item | **read-only** | only `research/<item-id>.md` | as above |
| 6 implement-stage | **edit** (anywhere needed; edits beyond `expected_files` must be documented, §5) | append `execution-log.md`, update `plan.md` Plan-changes | shell allowed for gate/validation commands |

Enforcement mechanisms (all orchestrator-side):
- **Scope built-in file/shell tools.** During the M0 spike, determine whether the SDK lets us
  disable or path-scope the agent's built-in edit/shell tools per session. If it does, use it; if
  not, fall back to running phases against a **read-only checkout / overlay** for non-impl phases.
- **`write_artifact` is the only write path** for phases 1–5, with path normalization + traversal
  rejection so writes can't escape the run's working dir.
- **Post-phase git-diff guard.** After every non-implementation phase, assert `git diff` (and
  untracked files) is empty; a non-empty diff fails the phase (the agent mutated source it
  shouldn't have).
- **`expected_files` is advisory in phase 6.** After each stage, the orchestrator diffs the
  changes against the stage's `expected_files`: edits *within* scope pass silently; edits *outside*
  scope are allowed but **require a matching Plan-changes entry + execution-log justification**
  (§5). An undocumented out-of-scope edit pauses the run for review — the missing documentation is
  the failure, not the edit. Source edits in phase 6 are otherwise unrestricted.

### 6.2 Orchestrator-enforced checkpoint gates

Gates are **machine-checked by the orchestrator**, never self-reported by the agent:
- `gates.ts` parses the structured `stages:`/`gate:` block from `plan.md` (§5).
- After a stage's implement sub-session ends, the orchestrator **runs the gate's `commands`**
  itself, compares against `expected` (e.g. `exit_code_0`), and records exit codes + output in
  `execution-log.md`.
- On failure the run **halts at that gate** (autopilot stops too); the user can inspect, fix, and
  `resume`. This removes reliance on the agent to honestly stop, validate, and report.

### 6.3 Cross-stage context continuity

Splitting phase 6 into one fresh sub-session per stage bounds context growth and drift, but risks
losing knowledge built up in earlier stages. We treat a fresh session as *clean*, **not blank** —
`buildContextPack(ctx, stage)` curates an explicit context pack for each stage so nothing material
is lost across the boundary:

- **Code on disk** — the real source written by prior stages *is* the shared state; the new session
  reads actual symbols/signatures rather than reconstructing them from memory.
- **`plan.md`** — the durable source of truth, including the running **Plan-changes** log.
- **`handoff.md`** — the prior stages' concise handoff entries (what was built, new/changed public
  symbols, decisions/conventions, deferred items) — see §5.
- **Cumulative diff** — a summary of files changed so far in the run, so the stage sees what already
  moved.
- **`context_needed`** — the existing files/symbols the plan declared this stage depends on (§5),
  pre-opened so the agent doesn't have to rediscover them.

This is a deliberate trade: a **curated, bounded handoff** over an ever-growing single transcript.
It usually beats one long session, which accretes noise, drifts, and risks context-window limits in
exactly the riskiest phase. Two design rules reinforce it: the plan phase **sizes stages to be
cohesive and loosely coupled** (§5) so little must cross a boundary, and tightly-coupled work stays
in a single stage. If a context pack would have to be enormous for a stage to succeed, that's a
signal the plan split the work at the wrong seam.

## 7. Configuration

`config.ts` maps each phase to a model + options, e.g.:

| Phase | Model (suggested) | Why |
| --- | --- | --- |
| research / research-item | strong reasoning | comprehension quality matters most |
| assumptions | mid | structured enumeration |
| classify | cheap/fast | short structured output |
| plan | strongest | this is the highest-leverage artifact |
| implement | strong + tools | code edits + verification |

Models, retry counts, parallelism cap, and working-dir root are all config-driven.

## 8. Single entrypoint & optional skill front-door

- Primary entrypoint: `npm run workflow -- "<task description>"` (or a thin `bin`).
- Optional **thin skill** (`SKILL.md`) whose only job is to invoke this tool, giving
  discoverability inside interactive Copilot ("run the feature workflow on X"). The SDK program
  remains the engine; the skill is just a friendly door. This is the recommended end state:
  **SDK program = engine, skill = front door.**

## 9. CLI design

The single entrypoint is a CLI (`agentic-workflow`, also runnable via `npm run workflow --`).
It supports starting a new run, **per-phase gating** (where to pause vs. autopilot, §9.1), and
resuming an interrupted run.

### Commands

| Command | Purpose |
| --- | --- |
| `run "<task description>"` | Start a new workflow run for the given task. Default command. |
| `resume [<run-id>]` | Continue an existing run from its last completed phase (see §9.4). |
| `status [<run-id>]` | Show a run's progress: phases completed, current phase, artifacts written. |
| `list` | List known runs in the working-dir root with their state. |
| `approve [<run-id>]` | In review mode, mark the current paused stage approved and advance one phase. |
| `abort [<run-id>]` | Mark a run aborted (keeps artifacts for inspection). |
| `clean [<run-id>]` | Delete a run's scratch dir (or all old runs with `--keep-days`). Safe — the dir is gitignored. |

`run` and `resume` are the core of the request; the others are thin helpers over the same
checkpoint state.

### 9.1 Execution control: per-phase gating

Rather than a single global mode, a run is controlled by a **gate set** — the set of phase
boundaries where the orchestrator **pauses for human review**. Every boundary *not* in the gate
set runs on autopilot. This lets the user place stops exactly where their judgment adds value
(e.g. review the plan, but auto-run the research) and let everything else flow.

The familiar "modes" are just **presets** over this gate set:

| Preset | Flag | Resulting gate set |
| --- | --- | --- |
| **Autopilot** | `--autopilot` | **∅** — no stops; runs end to end (auto-approve within guardrails). |
| **Review** (default) | `--review` | a gate **after every phase** — pause, inspect/edit, `resume`. |
| **Dry run** | `--dry-run` | orthogonal — renders prompts/flow with **no SDK calls or edits** at all. |

**Fine-grained overrides** (applied on top of the preset, left to right):

| Flag | Effect |
| --- | --- |
| `--pause-after <phase[,…]>` | Add a gate **after** each listed phase. |
| `--pause-before <phase[,…]>` | Add a gate **before** each listed phase. |
| `--auto <phase[,…]>` | **Remove** the gate(s) around each listed phase (run it without stopping). |

Phase names: `research`, `assumptions`, `classify`, `research-item`, `plan`, `implement`.
Because phase 6 is staged (§5), `implement:*` addresses *every* stage boundary and
`implement:<stage-id>` a specific one — so you can autopilot the implementation but pause between
stages, or vice-versa.

**Resolution & persistence.** The orchestrator starts from the preset's gate set, then applies
the override flags in order, producing a concrete gate set that is **persisted in `state.json`**.
`resume` honors it; passing the same flags to `resume` adjusts it mid-run. `status` prints the
resolved gates so the user can see exactly where the run will stop next.

**Examples:**

```bash
# Auto-run research→plan, then review the plan and gate the implementation
agentic-workflow run "<task>" --autopilot --pause-after plan --pause-before implement

# Review everything EXCEPT the cheap early phases
agentic-workflow run "<task>" --review --auto research,assumptions

# Fully autopilot, but stop between each implementation stage to sanity-check
agentic-workflow run "<task>" --autopilot --pause-after 'implement:*'

# Only one stop in the whole run: right before code is touched
agentic-workflow run "<task>" --autopilot --pause-before implement
```

Notes:
- `--review` is the safe default precisely because phase 6 edits real code; autopilot is opt-in.
- `--pause-before <phase>` remains the common "auto-run up to here, then gate" shorthand; it's just
  the single-phase case of the override flags above.
- **Blocking-clarification stop (independent of the gate set).** Even with an empty gate set
  (autopilot), if phase 2 marks an assumption `blocking: true` (low-confidence and affecting
  correctness/security/API behavior), the run pauses and asks the user rather than inventing an
  answer — and likewise the orchestrator pauses on an **undocumented out-of-scope edit** or a
  **failing gate** (§5/§6.2). These safety stops are not part of the user-defined gate set and
  can't be removed by `--auto`; `--yes` suppresses only the *clarification* prompt for fully
  unattended runs.

### 9.2 Flags (full surface)

```
agentic-workflow run "<task>" [options]

Execution control (gate set; presets + per-phase overrides, §9.1):
  --autopilot                 Preset: no stops (gate set = ∅). Auto-approve within guardrails.
  --review                    Preset (default): pause after every phase.
  --dry-run                   Render prompts/flow only; no SDK calls, no edits.
  --pause-after <phase[,…]>   Add a gate after each listed phase.
  --pause-before <phase[,…]>  Add a gate before each listed phase.
  --auto <phase[,…]>          Remove the gate(s) around each listed phase (run without stopping).
                              Phases: research, assumptions, classify, research-item, plan,
                              implement (and implement:* / implement:<stage-id> for stage gates).

Run control:
  --simple                    Preset for small tasks: defaults --skip-research=all and
                              --skip-classify on (assumptions → plan → implement); §3.2.
                              Override with --no-skip-research / --no-skip-classify.
  --run-id <id>               Use/assign an explicit run id (default: slug of task + short hash).
  --out <dir>                 Working-dir root (default: ./.agentic-workflow). Run lives in <out>/<run-id>/.
  --from <phase>              Start at a specific phase (advanced; assumes prior artifacts exist).
  --only <phase[,phase...]>   Run just the listed phase(s), then stop.
  --skip-research[=all|specs] Skip phase-1 spec generation (and phase-4 per-item research unless
                              =specs) for simple tasks to save tokens; §3.2. Alias: --no-research.
  --skip-classify             Skip phase-3 classification; synthesize a single-item subitems.json
                              for atomic tasks; §3.2. Alias: --no-classify.
  --type <bug|feature|refactor> Declared task type; used as the synthesized item type when
                              classify is skipped (default: feature).
  --reason <text>            Reason recorded in manifest.json for a skipped phase.

Execution tuning:
  --model <phase=model,...>   Override per-phase model (e.g. plan=gpt-5,classify=gpt-4.1-mini).
  --concurrency <n>           Max parallel sessions for phase-4 fan-out (default: e.g. 3).
  --max-retries <n>           Per-phase validation retry budget (default: e.g. 2).
  --branch <name>             Git branch to run phase-6 edits on (recommended for safety).

Permissions / guardrails (autopilot):
  --allow-dir <path>          Additional directory the agent may read/write (repeatable).
  --no-network                Disallow network tools during the run.
  --write-root-gitignore      Opt in to adding `.agentic-workflow/` to the repo-root .gitignore
                              (default: ignore locally via .git/info/exclude only; §3.1).
  --yes                       Non-interactive: assume "yes" to any orchestrator prompts
                              (also suppresses the blocking-clarification stop; §9.1).

Output:
  --json                      Machine-readable progress/events on stdout.
  --quiet | --verbose         Control log verbosity.
```

`resume`, `status`, `approve`, `abort` accept `[<run-id>]` and `--out <dir>`; if `run-id` is
omitted they target the most recent run under the working-dir root.

### 9.3 Gating behavior at a glance

```
GATE SET = the phase boundaries where the run PAUSES for review. Everything else autopilots.

--autopilot (gates = ∅):
  research → assumptions → classify → research-item×N → plan → implement → done   (no stops)

--review (gates = after every phase):
  research ─┐ resume  assumptions ─┐ resume  …  plan ─┐ resume  implement → done
  (PAUSE+exit after each phase; user inspects/edits artifact, then resume)

--autopilot --pause-after plan --pause-before implement  (gates = {after plan} only*):
  research → assumptions → classify → research-item×N → plan ─┐ resume  implement → done
                                                    (one PAUSE, right before code is touched)

DRY RUN: print rendered prompt + planned session/artifact for every phase; make no calls

* "after plan" and "before implement" are the same boundary; safety stops (blocking
  clarification, undocumented out-of-scope edit, failing gate) fire regardless of the gate set.
```

### 9.4 Resume semantics

State lives in `<out>/<run-id>/state.json`, written **atomically** and updated after every phase
(and every phase-4 sub-item). It records: the resolved **gate set** (§9.1), task, run-id,
**per-phase status** (`pending` / `in_progress` / `failed` / `completed` — not just "last
completed"), per-item phase-4 status, retry counts, template versions, and a **content hash for
each produced artifact**. A **run lock** (e.g. `run.lock`) is held while a run mutates state to
prevent concurrent `resume`/`approve` from corrupting it or duplicating phase-4 work.

`resume` rules:
- Reads `state.json`, finds the last `completed` (and validated) phase, and continues — running
  straight through autopilot boundaries and stopping at the next gate in the persisted gate set.
  Re-passing `--pause-*`/`--auto`/`--autopilot`/`--review` adjusts the gate set for the remainder.
  A phase left `in_progress`/`failed` (e.g. after a crash) is re-run from scratch in a fresh
  session — never half-trusted.
- **Revalidate before consuming.** On every resume, re-hash and re-validate the input artifacts.
  If a hash differs from `state.json`, the artifact was edited since it was produced — in review
  mode that's expected (record the new approved hash); the orchestrator re-checks it parses /
  is schema-valid before downstream phases read it, so a corrupted/partial edit can't flow
  through silently.
- Phase-4 fan-out resumes per item: items with a `completed` status **and** a matching hash are
  skipped; missing/failed/edited items are re-run.
- In **review** mode, `resume` advances exactly one phase and pauses again. `approve` records the
  current artifact's hash as approved and advances one phase. Repeated `resume`/`approve` walks
  the user through the pipeline.
- In **autopilot** mode, `resume` (after a crash/failure) continues straight through to completion.
- The user may **edit an artifact** during a review pause; the next phase reads the edited file
  from disk (and its new hash is recorded), so human corrections flow downstream naturally.
- `--from <phase>` forces a starting phase (rewinding state and invalidating later artifacts'
  hashes); guarded behind a confirmation since it discards downstream work.

### 9.5 Exit codes (for scripting / CI)

| Code | Meaning |
| --- | --- |
| `0` | Run **fully complete** (all phases done). |
| `10` | **Paused** — the run hit a gate in its gate set, a `--pause-*` boundary, a blocking clarification, an undocumented out-of-scope edit, or a failing gate, and awaits `resume`/`approve`. Distinct from completion. |
| `1` | Unrecoverable failure (validation exhausted retries, gate hard-failed with no path forward, SDK/auth error). |
| `2` | Bad usage / invalid flags. |

### 9.6 Example sessions

```bash
# True autopilot, isolated branch for the implementation phase
agentic-workflow run "Add rate limiting to the public API" \
  --autopilot --branch feat/rate-limiting

# Stage-gated review: run one phase at a time, inspecting artifacts between
agentic-workflow run "Refactor auth module" --review
#   → writes specs/, pauses. User reads specs/architecture.md, edits if needed.
agentic-workflow resume          # → assumptions.md, pauses
agentic-workflow resume          # → subitems.json + classification.md, pauses
agentic-workflow status          # → shows phase 3 done, phase 4 next (N items)
agentic-workflow resume          # → research/<item>.md ×N (parallel), pauses
agentic-workflow approve         # → plan.md, pauses (review the plan carefully)
agentic-workflow resume          # → implements per plan, writes execution-log.md, done

# Auto-run research→plan, but gate before touching code
agentic-workflow run "Migrate to new logging lib" --pause-before implement

# Inspect prompts/flow without spending requests
agentic-workflow run "Add CSV export" --dry-run

# Simple task: skip research/specs entirely to save tokens (straight to plan → implement)
agentic-workflow run "Fix typo in error message in src/auth.ts" --skip-research --autopilot

# Tiny atomic task: --simple presets skip research + classify → assumptions → plan → implement
agentic-workflow run "Add a null guard in src/parse.ts:42" --simple --type bug --autopilot
```

## 10. Repo integration (per this repo's conventions)

- Place under `tools/agentic-workflow/` with its own README and tests.
- Add `/tools/agentic-workflow/ @JennyPng` to `.github/CODEOWNERS`.
- Add a row to the root `README.md` tool index.
- Provide `ci.yml` from `eng/pipelines/templates` for build/test.
- Pipeline names: public `tools - agentic-workflow - ci`, internal `tools - agentic-workflow`.

## 11. Phased build plan (milestones)

1. **M0 — Capability spike (1 day):** stand up a minimal SDK program and **validate the
   assumptions the whole design rests on** before building further. Confirm:
   - `createSession()` truly isolates history (no bleed across sessions);
   - multiple concurrent sessions per client work (needed for phase-4 fan-out);
   - exact event names + streaming behavior;
   - the TS permission hook / auto-approve option (Python has `PermissionHandler.approve_all`);
   - **whether built-in file/shell tools can be scoped or disabled per session** (critical for
     the §6.1 read-only policy) — if not, design the read-only-checkout fallback;
   - working-directory behavior; custom-tool schema/validation support; model override;
   - token/cost usage events; and that **code editing is actually available** via the SDK runtime.
2. **M1 — Artifact contract + working dir (½ day):** `artifacts.ts`, `types.ts`, `config.ts`,
   `.agentic-workflow/<run-id>/` layout + local ignore (§3.1), atomic writes, hashing, run lock,
   checkpoint `state.json` with per-phase status.
3. **M2 — `phase()` core + policy (1.5 days):** createSession → render prompt → sendAndWait →
   validate → **policy/git-diff guard (§6.1)** → fresh-session retry → checkpoint. Stream events
   to the execution log.
4. **M3 — Prompt templates (1 day):** author + iterate all 6 templates against a real task.
5. **M4 — Classification + parallel fan-out (1 day):** `subitems.json` schema (with deps/overlap)
   + validator tool; parallel phase-4 sessions ordered by `dependsOn`, with a concurrency cap.
6. **M5 — Plan + staged implement + gates (1.5 days):** wire phase 5 (incl. machine-readable
   `stages:`/`gate:` block + `planSchema`) and the **staged** phase 6 with orchestrator-enforced
   gates, `expected_files` scope checks, and the cross-stage context pack (§6.3); execution-log +
   handoff + deviation handling; end-to-end run on a sample.
7. **M6 — Hardening (1 day):** retries/backoff, resume revalidation + locking, dry-run, redaction.
8. **M7 — CLI surface (½ day):** wire commands (`run`/`resume`/`status`/`list`/`approve`/`abort`/
   `clean`), the gate-set resolution (presets + `--pause-after`/`--pause-before`/`--auto`),
   blocking-clarification stop, exit codes.
9. **M8 — Packaging:** README, tests, `ci.yml`, CODEOWNERS, root README index. Optional skill
   front-door.

> **MVP cut (de-risk first):** a thinner v1 is worth landing before the full surface —
> research → (assumptions+classify) → optional per-item research → plan → **mandatory human
> approval** → staged implement with orchestrator-enforced gates. Defer `--only`, `--from`,
> `approve`-as-separate-from-`resume`, root-`.gitignore` mutation, and fully unattended phase 6
> until the gate/policy enforcement is proven. The richer CLI above remains the target design.

## 12. Open questions / risks

- **Permissions in autonomous mode:** exact TS option to auto-approve tool/file permissions so
  phases run unattended; what guardrails (allowed dirs, no network) to enforce.
- **Tool scoping (design-critical):** whether the SDK can disable/path-scope built-in file/shell
  tools per session. The §6.1 enforcement model depends on this; the read-only-checkout fallback
  exists if it can't. Resolve in M0.
- **Gate command safety:** orchestrator-run gate `commands` execute arbitrary shell from a
  generated `plan.md`. Constrain to an allowlist / require approval of the gate block in autopilot.
- **Cost/quota:** each phase = ≥1 premium request; phase-4 fan-out and staged phase-6 multiply
  this. Need a concurrency cap and per-run budget.
- **Context size for phase 5:** plan phase reads all specs + all item research — may need
  summarization if large.
- **Implementation safety:** phase 6 edits real code. Run on a dedicated branch; gates +
  `expected_files` + deviation policy bound scope; human review of `plan.md` recommended.
- **Artifact location:** scratch by default — runs live under a locally-ignored `.agentic-workflow/`
  root (`.git/info/exclude` + inner `.gitignore`; root-`.gitignore` only via opt-in flag); see §3.1.
- **Determinism:** prompt templates are versioned; the template version is stamped into each
  artifact / `state.json` for traceability.

## 13. Decisions locked so far

- Language: **TypeScript** (`@github/copilot-sdk`).
- Mechanism: **SDK `createSession` per phase**, artifacts on disk as hand-off; **fresh session on
  every retry** (no transcript reuse).
- **Guarantees are orchestrator-enforced, not prompt-only:** per-phase tool/FS policy + git-diff
  guard (§6.1), machine-readable checkpoint gates the orchestrator runs (§6.2), and resume
  revalidation + hashing + run lock (§9.4).
- **Phase 6 is staged:** one fresh sub-session per plan stage, each gated. `expected_files` is
  advisory (documented deviations allowed, §5); cross-stage knowledge is preserved via a curated
  context pack — `plan.md` + `handoff.md` + cumulative diff + `context_needed` (§6.3).
- Location: new tool at `tools/agentic-workflow/` (this design doc lives at repo root for now).
- End state: SDK engine + optional thin skill as the single entrypoint.
- CLI: `run` + `resume` core commands. Execution is controlled by a **per-phase gate set** —
  the boundaries where the run pauses for review — with `--autopilot` (∅) and `--review` (every
  phase) as presets, refined by `--pause-after`/`--pause-before`/`--auto` (incl. per-stage
  `implement:*`). Safety stops (blocking clarification, undocumented out-of-scope edit, failing
  gate) fire regardless. The resolved gate set persists in `state.json`; `--dry-run` is orthogonal.
- **Research is optional:** `--skip-research[=all|specs]` omits spec generation (and per-item
  research unless `=specs`) for simple tasks; downstream prompts branch on `manifest.research.skipped`
  and the API safety valve still guards against silently dropping an API spec (§3.2).
- **Classification is optional:** `--skip-classify` omits phase 3 for atomic tasks; the
  orchestrator synthesizes a single-item `subitems.json` so the downstream contract is preserved.
  Combined with `--skip-research`, the pipeline collapses to assumptions → plan → implement (§3.2).
- **`--simple` preset:** one flag defaults both skips on for small tasks; individual `--no-skip-*`
  flags override it. It changes which phases run, not the execution mode (§3.2).
