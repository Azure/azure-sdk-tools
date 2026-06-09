# Spec: 8 Operations — Agent Evaluation Strategy

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Agent Prompts](#agent-prompts)
- [Success Criteria](#success-criteria)
- [Open Questions](#open-questions)
- [Implementation Plan](#implementation-plan)

---

## Definitions

- **Agent**: a live LLM conversation driving Azure SDK MCP tools through skills.
- **Skill**: a markdown contract under `.github/skills/<name>/` telling the
  agent *when* to engage and *which* tools/workflow to use.
- **MCP tool**: a discrete capability exposed by the Azure SDK MCP server.
- **Workflow scenario**: a user prompt that crosses multiple tools / skills
  end-to-end (e.g. *create release plan → generate SDK → link the SDK PR*).
- **Stimulus**: one prompt + its expected behavior — the unit of an eval.
- **Three graders per stimulus**: `skill-invocation` (right skill picked),
  `tool-calls` (right tools / order / args), and `prompt` (right final answer).
- **Mock MCP**: an in-memory fake of the Azure SDK MCP server — no network,
  no side effects. **Live MCP**: the real server hitting real DevOps / GitHub.


---

## Background / Problem Statement

We're shipping agent-driven replacements for manual SDK workflows — starting
with the release planner. When someone
asks *"does the agent actually do what we said it does?"*, today the only
honest answer is "I tried a few prompts on my laptop." That is not good
enough to hand to partner teams or to keep regressions out as more workflows
land.

We need a small, shared set of prompts we promise to support, run regularly,
with a clear pass/fail per prompt — so we can point at the report instead
of re-demoing.

---

## Goals and Exceptions/Limitations

### Goals

- [ ] **One file per workflow, three graders per prompt** — skill picked,
      tools called, final answer.
- [ ] **Mock MCP by default, live MCP only on opt-in** — no accidental writes
      to DevOps / GitHub; release / publish tools stay mock-only.
- [ ] **Mock covers every tool the scenarios call**, with realistic responses.
- [ ] **Anyone can clone and run** — env vars, no hard-coded paths; live
      scenarios declare what repos they need.
- [ ] **The run produces a status table** of pass/fail per prompt plus a
      trajectory per prompt — readable by non-engineers.
- [ ] **Reports come out in the formats people actually use** — markdown
      for humans, JUnit for CI, CSV for spreadsheets and dashboards.
- [ ] **Adding a partner-reported prompt is one new stimulus**, no runner
      or CI changes.
- [ ] **Multi-step chains work** (e.g. *validate TypeSpec → create release
      plan → generate SDK → link the SDK PR*).

### Exceptions and Limitations

- **Some prompts can only be checked against live MCP** — the mock can't
  prove a release plan was really created. Those run opt-in only.
- **The agent is not deterministic.** Same prompt, different wording or
  turn count each run. We grade shape, not exact strings, and accept some
  flake.

---


## Design Proposal

### The three eval kinds

We organize evals around what's actually being tested. No tier numbers —
use the names. The first three columns are the same axis (what does this
prove); the last two say where each lives and what backend it needs.

| Kind | What it proves | Agent | MCP | Lives in |
|---|---|---|---|---|
| **Skills** | A user prompt routes to the right skill. | live | none | `.github/skills/<skill>/evals/` |
| **Workflows — Mock** | Agent picks the right skills, calls the right tools in the right order with the right args, returns the right answer. | live | **mock** | `evals/workflow-scenarios/mock/` |
| **Workflows — Live** | Same as above, but against the real backend — catches drift the mock can't see (TypeSpec ordering, real codegen output, real DevOps state). | live | **live** | `evals/workflow-scenarios/live/` |

Plus a hermetic tool-shape layer that isn't agent-driven:

| Kind | What it proves | Lives in |
|---|---|---|
| **Tools** | Each MCP tool is wired up, returns the expected response shape, and is reliably picked from a range of paraphrased user prompts (one tool per stimulus, no multi-step planning). | `evals/tools/` |

#### Required graders by kind

Mock and live workflow scenarios share the same scenario format but
differ in which graders are *required* vs *optional*:

| Kind | `tool-calls` | `skill-invocation` | response grader (`prompt` / LLM-judge) |
|---|---|---|---|
| **Workflows — Mock** | required | optional | not applicable — mock responses are stubbed, so a response grader has nothing meaningful to assert |
| **Workflows — Live** | required | required | required — only live runs produce a real assistant answer worth grading |

Rationale: the mock backend deterministically replays canned data, so
"the agent said the right thing" reduces to "the agent called the right
tools." Live runs are the only place a free-form response can drift, so
that's where the response grader earns its cost.


### Folder layout

```
evals/
├── tools/                  one prompt → one tool (paraphrase routing + shape, hermetic)
├── workflow-scenarios/
│   ├── mock/               workflow scenarios run against the mock MCP
│   └── live/               workflow scenarios run against the live MCP
└── setup/                  shared fixture scripts (repo clone, etc.)
```

A scenario lives under `mock/` or `live/` based on which backend the
graders are written against, not based on the prompt. A prompt can
have a `mock/` and a `live/` variant (release-planner does).

**Scenarios are environment-agnostic.** A scenario file declares the
prompt, expected skills, expected tool sequence, and graders — nothing
about whether MCP is mock or live. Same file, same graders; the MCP
backend is picked at run time.

| Run mode | MCP | Repos? | When | Coverage |
|---|---|---|---|---|
| Workflows — Mock | mock (stub, no LLM) | azure-sdk-tools only | nightly + on demand | every scenario |
| Workflows — Live | live (real backends) | azure-sdk-tools + shallow/sparse clones of the spec & language SDK repos each scenario declares | weekly | scenarios tagged `live-safe` (curated subset) |

When live and mock results disagree, the mock is wrong — the divergence
points straight at the missing or stale handler. Every scenario that
runs on mock therefore drives the mock to grow handlers for the tools
it exercises.

### Where each eval lives

| What it tests | Lives in |
|---|---|
| **One skill** (does this skill route, call its tools, return a sensible answer) | `.github/skills/<skill>/evals/` |
| **Cross-skill / cross-tool** (multi-step chains, e2e flows, mock-server integration, anything that doesn't belong to one skill) | `tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/` |

Skill evals stay next to `SKILL.md` — that's the convention skill
authors expect, and it keeps everything about a skill in one folder.
Existing skill eval files do not move.

#### Skill eval suite — current state and direction

The per-skill suite predates this project. Today roughly a dozen skills
have eval files; some are missing thresholds and pass without asserting
anything, and most capability stimuli are graded only by a single
substring check — they pass whether the agent called the right tool,
the wrong tool, or just echoed the prompt.

*Direction.* Raise the bar on what counts as a per-skill eval: adopt
the four-layer pattern — skill-invocation + tool-calls + structural
output match + optional LLM-judge — as the required shape for every
capability stimulus. A `skill-eval-authoring` skill packages the
pattern, grader catalog, and anti-patterns so other Azure SDK teams
adopt without re-learning the gotchas.

### Decision tree — where does my new eval go?

```
Do you only care that the agent picks the right skill
(you don't care which tools it then calls)?
└── yes → .github/skills/<skill-name>/evals/   (not this project)

Do you want to check that one MCP tool returns the right shape
for a given input — no agent in the loop?
└── yes → evals/tools/

Is it a multi-step / multi-tool agent flow?
└── yes → Workflow scenario
        ├── Default → evals/workflow-scenarios/mock/
        │   Runs against the mock MCP. Use this unless the mock can't
        │   faithfully cover the behavior.
        └── Also need live coverage → add an evals/workflow-scenarios/live/
            variant. Reserve for cases where the real backend's behavior
            matters (TypeSpec ordering, real codegen output, real DevOps
            state).
```

### CI

The suite runs on a schedule, not on every pull request. Agent runs
talk to an LLM — they cost money and they flake in ways that have
nothing to do with the code under review.

| When | What runs | Backend |
|---|---|---|
| Nightly | All workflow scenarios + the hermetic tool layer | mock |
| Weekly | Workflow scenarios marked safe to run live | live (with safe-mode flag on writes) |
| On demand | Any suite, any backend | author's choice |

#### PR gate for essential workflows (open)

A case for *narrow* PR gating: a small curated set of mock scenarios
covering the workflows we have already promised to partner teams
(release-planner today; more as they ship) could run on PRs that touch
the agent, skills, or MCP tools — so we catch a regression in the
workflows users actually rely on before merge, instead of the morning
after.

Unresolved trade-offs: which scenarios count as "essential"; how to
keep the gate from flaking on LLM non-determinism (retries? loose
thresholds? quorum across N runs?); whether the cost of the gated
subset is acceptable for every PR; and which paths actually trigger it
(agent-only? skills? MCP server? all of the above?).

See [Open Questions](#open-questions).

#### Pre-run setup for live scenarios

**The problem.** A real workflow crosses repos. The release planner
reads a TypeSpec project from `azure-rest-api-specs`, generates code
into a language SDK repo, and links a PR back. The tools the agent
calls expect those files on disk. If a repo is missing, the agent
fails for the wrong reason and we learn nothing.

**The setup step.** Each live scenario declares the repos (and
optionally the commit) it needs. One setup step reads all live
scenarios, takes the union, and makes sure each repo is present at the
requested commit before any eval runs.

**Locally.** A single script. Run it once; it clones into a cache
folder under your home directory and reuses the clone on subsequent
runs. Same script CI uses.

**In CI.** The weekly live job runs the same script. The cache folder
is a build-cache artifact keyed on the set of repos the scenarios
declare; it's invalidated only when that set changes.

**Pinning.** A scenario can pin a commit when reproducibility matters.
Otherwise the setup step takes the default branch and records the
commit it used in the run output.

The nightly mock job runs no setup — mock evals touch no external repos.


### Mock MCP server status

#### How it works

`Azure.Sdk.Tools.Mock` reflects over the real CLI's tool list at boot and
registers a mock proxy for **every** tool the real `Azure.Sdk.Tools.Cli`
advertises, preserving each tool's name, description, and input schema.
At call time the proxy looks up a handler by tool name:

- **Custom handler exists** → scripted, type-correct response.
- **No custom handler** → fallback `{ Message = "Success" }`.



### Results

The goal: anyone — partner team, manager, the engineer who broke
something — should be able to open a run and understand what passed,
what failed, and why, without help.

Each run writes three files into the output directory:

| File | What it is | Who reads it |
|---|---|---|
| `eval-results.md` | Human status table: one row per prompt, pass/fail per grader. | Reviewers, partner teams, anyone scanning a run. |
| `results.jsonl` | The full agent trajectory — every tool call, args, return values, timings. One JSON object per line. | Engineers debugging a failure with tooling. |
| `junit.xml` | Standard test-results format the CI test-results widget already understands. | CI dashboards. |

The JSONL is rich but hard to read raw. We add two post-processors
on top of it:

- **Trajectory HTML** — one self-contained web page per prompt, opens
  straight from `file://`. Shows the same trajectory as `results.jsonl`
  but readable by someone who has never seen JSONL.
- **CSV history** — one row per prompt, appended across runs. Lets us
  ask *"how often did release-planner pass in the last 30 nightlies?"*
  and feed a dashboard later.

In CI: trajectories + JSONL are uploaded as build artifacts you can
download from the run page; the CSV gets appended to a long-lived
history branch.

### Performance and cost controls

Why this section exists: agent evals are *slow* and *expensive*. Every
run talks to a real LLM — every tool call is a round trip, every turn
is tokens billed against our subscription. Without limits, a single
badly-written scenario can sit in a loop for an hour and burn through
the budget while still reporting *"passed"*.

Concrete example: one real release-planner end-to-end run took **17
minutes wall time, 1.78M tokens, 41 turns**.

The framework therefore enforces three things:

**1. Per-scenario budgets.** Every scenario file declares an upper
bound on:

- **Turns** — how many times the agent loops.
- **Wall time** — how long the whole run can take.
- **Billable tokens** — input + output tokens we actually pay for.
- **Tool calls** — catches an agent stuck calling the same tool forever.

The runner warns at 50% of any limit, fails the scenario at 100%, and
kills the whole run at 200% so a runaway can't bleed indefinitely.

**2. Tiered defaults.** Mock runs nightly against an in-memory fake —
cheap and fast, so the limits are tight. Live runs weekly against real
backends — slower by nature, so the limits are looser.

| Tier | Turns | Wall (s) | Billable tokens |
|---|---|---|---|
| Nightly mock | 30 | 300 | 200k |
| Weekly live | 60 | 600 | 500k |

A scenario that needs more must opt in with a justification comment in
the scenario file. If reviewers reject the opt-in, the scenario has to
be rewritten to fit, or moved to mock — budgets don't widen.

**3. Background guardrails** — things the scenario author never has
to think about, baked into the framework:

- Polling tools (`*_get_*_status`) return a terminal state on the first poll under safe mode — no agent stuck waiting for *"in progress"* to flip.
- LLM-judge graders default to a cheaper model than the agent itself.
- CI cancels superseded runs when a branch gets a new push.


---

## Agent Prompts

The list of prompts the agent is promised to support. Each lives as a
stimulus in `evals/workflow-scenarios/mock/<workflow>.eval.yaml` (plus a
`live/` counterpart where applicable). Adding a new prompt is one new
entry in the matching file.

### Release-planner workflow

Derived from the release-planner replacement test plan
([#15835](https://github.com/Azure/azure-sdk-tools/issues/15835)). All
five route to the `azsdk-common-prepare-release-plan` skill.

| Prompt | What the agent must do | Required tool calls |
|---|---|---|
| Create a public-preview release plan for a TypeSpec spec, target month June 2026 | Pick the prepare-release-plan skill; check for an existing plan; create one. | `azsdk_get_release_plan`, `azsdk_create_release_plan` |
| Create a release plan **and** generate SDK for a TypeSpec spec, release type beta | End-to-end chain: create, then generate, then back-fill SDK details. | `azsdk_get_release_plan`, `azsdk_create_release_plan`, `azsdk_run_generate_sdk`, `azsdk_update_sdk_details_in_release_plan` |
| Generate SDK for all languages for an existing release plan id | Look up the plan, run generation against the languages it lists. | `azsdk_get_release_plan`, `azsdk_run_generate_sdk` |
| Link a different spec PR (`https://github.com/Azure/azure-rest-api-specs/pull/...`) to an existing release plan | Look up the plan, swap the spec-PR field. | `azsdk_get_release_plan`, `azsdk_update_api_spec_pull_request_in_release_plan` |
| Update SDK details (package names) on an existing release plan from `tspconfig.yaml` | Look up the plan, update the SDK details from emitter config. | `azsdk_get_release_plan`, `azsdk_update_sdk_details_in_release_plan` |

All five forbid `azsdk_verify_setup` (the setup gate runs once at the
top of the workflow, not per prompt) and forbid the irrelevant
`azsdk_create_release_plan` in the four "existing plan" prompts so we
catch the agent creating a duplicate.

### Other workflows in the first round

| Workflow | File | Coverage |
|---|---|---|
| Check spec is in public repo then validate TypeSpec | `check-public-repo-then-validate.eval.yaml` | TypeSpec authoring routing + validation tool call. |
| TypeSpec generation — step 2 of the authoring flow | `typespec-generation-step02.eval.yaml` | TypeSpec authoring skill + generate tool. |
| Rename a client property in a generated SDK | `rename-client-property.eval.yaml` | Customization skill + customize-code tool. |

The live counterpart of release-planner lives at
`evals/workflow-scenarios/live/release-planner.eval.yaml` and adds a
prompt-grader that checks the real DevOps response.

---

## Success Criteria

- A single command runs the full mock suite locally and produces
  `eval-results.md`, `results.jsonl`, JUnit XML, the per-prompt
  trajectory HTML, and a `history.csv` row.
- Every release-planner prompt above is green in the mock suite.
- Every MCP tool a green scenario calls has a custom mock handler
  returning a chainable, type-correct response.
- A new contributor can clone the repo, set the documented env vars,
  and reproduce the same `eval-results.md` verdict table on their
  machine.
- A partner team reporting *"I tried this prompt and the agent didn't
  do anything"* can be answered by pasting their prompt as a new
  stimulus and re-running the workflow file — no runner or CI changes.
- The status table is what we hand to reviewers (Renhe, Laurent,
  partner teams) to answer *"what does the agent currently support?"*

---

## Open Questions

### CI cadence and PR gating

**Cadence.** Current proposal: nightly mock + weekly live + on-demand.
Open: is nightly the right frequency for mock, or do we want it on
every push to `main`? Is weekly enough for live, given live is the
only thing that catches real-backend drift?

**PR gate for essential workflows.** Should a curated subset of mock
scenarios block merge on PRs that touch the agent, skills, or MCP
tools? Specifically to answer:

- *Which workflows are "essential"* — just release-planner today, or
  a broader set? Who decides when a new workflow joins or leaves the
  gated set?
- *Which paths trigger the gate* — agent code, skill markdown, MCP
  tool code, mock handlers, all of the above? Anything else?
- *How do we tame flake* — retries on failure, quorum across N runs,
  loose thresholds, or just accept some red and require a human
  override? Hard requirement: a green PR must mean *the gated
  scenarios passed*, not *we got lucky this run*.
- *What's the cost ceiling* — the gated subset runs on every PR push
  to a touched path; what's the per-PR token / wall-time budget we're
  willing to spend before we move it back off the PR?

We need owners' input on all four before turning the gate on.

-
