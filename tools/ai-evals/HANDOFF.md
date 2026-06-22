# Eval Work Handoff — Azure SDK Tools / MCP / Skills


## 1. The 30-second version

We ship an **AI agent** that helps Azure SDK engineers do release work (create release plans, validate TypeSpec, edit APIView, etc.). The agent is **Copilot + the azsdk-cli MCP server + a set of SKILL.md instruction files**.

Because LLM behavior drifts every time the model, the skills, or the MCP tools change, we need a **test suite for the agent** — something that says "yes, this skill still works" with the same confidence a unit test gives for normal code.

That test suite is what "eval" means here. It has gone through **two generations**, and we are mid-migration to a third: an external Microsoft framework called **Vally**.

---

## 2. The three moving parts you need to keep straight

| Piece | What it is | Where it lives |
|---|---|---|
| **azsdk-cli MCP server** | The set of tools the agent can call (create release plan, update APIView, validate TypeSpec, …) | `tools/azsdk-cli/Azure.Sdk.Tools.Cli/` |
| **Skills** | `SKILL.md` files that teach Copilot *how* to use those tools for a given task | `.github/skills/` |
| **Eval system** | The harness that tests whether the skills + MCP tools actually work | Spread across several folders — see table below |

**Important:** skills *use* the MCP server, so when we evaluate skills we are transitively evaluating the MCP server. They are not two separate test efforts.

### Where the eval system actually lives today

There is no single "evals folder" — the work is split across the framework migration. Concrete paths:

| Path | What it is | Generation |
|---|---|---|
| `tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations/` | The in-house **C# NUnit** harness — scenarios, tool-mock dispatch, HTML reporter, model config. The Gen-1 system. | Gen 1 |
| `tools/azsdk-cli/Azure.Sdk.Tools.Cli.Benchmarks/` | The longer end-to-end benchmark scenarios (sparse-checkout real repos, walk a full workflow). | Gen 1.5 — **to be deleted** |
| `tools/azsdk-cli/Azure.Sdk.Tools.Mock/` | Standalone **mock MCP server**. Returns pre-registered responses keyed on inputs; falls through to `Success` when no match. Used by both old and new evals to avoid real API calls. | Cross-gen |
| `.github/skills/<skill-name>/evaluate/` | Per-skill **Vally** setup (`.vally.yaml`, `evals/`, `fixtures/`, `README.md`). Currently only `azure-typespec-author` has one, set up by the Shanghai team. | Gen 2 |
| `tools/ai-evals/azure-mcp/` | **Separate, Python-based** evals for the *azure-mcp* project (not our azsdk-cli). Tool-call accuracy. Independent effort — listed for completeness so you don't confuse it with ours. | Unrelated |
| `eng/common/pipelines/templates/steps/ai-evals-*.yml` (synced to all `azure-sdk-for-*` repos) | The CI plumbing that runs Gen-1 evals on copilot-instruction changes. | Gen 1 |
| Azure DevOps pipeline **definition 8165** | The (currently disabled) Vally CI pipeline from PR #15376. | Gen 2 |

---

## 2.5 Background primer — MCP and Skills, as they exist in *this* repo

This is a domain-specific primer. We assume you know what an LLM and a CLI are; we *don't* assume you've seen MCP or skills before. For the general protocol/spec, see [modelcontextprotocol.io](https://modelcontextprotocol.io).

### 2.5.1 The azsdk-cli MCP server

**What it is, in our repo:** `Azure.Sdk.Tools.Cli` is a C# command-line tool that *also* speaks the Model Context Protocol. When launched by an MCP client (Copilot CLI, VS Code, Claude Desktop, …), it exposes a catalogue of **tools** the model can call to perform Azure-SDK release-engineering tasks. Same binary, two front-ends: human CLI commands, and MCP tools.

**The catalogue (representative — not exhaustive):**

| Area | Example MCP tools | What they do |
|---|---|---|
| Release plan | `azsdk_release_plan_create`, `azsdk_release_plan_get`, `azsdk_release_plan_update`, `azsdk_link_sdk_pull_request_to_release_plan` | Talk to Azure DevOps to create / read / update the release-plan work item for a given SDK package. |
| APIView | `azsdk_apiview_get_comments`, `azsdk_apiview_request_copilot_review`, `azsdk_apiview_get_copilot_review` | Read review feedback and request copilot reviews against APIView. |
| TypeSpec | `azsdk_run_typespec_validation`, `azsdk_get_modified_typespec_projects`, `azsdk_typespec_init_project` | Validate TypeSpec specs and bootstrap projects. |
| SDK generation | `azsdk_package_generate_code`, `azsdk_package_build_code`, `azsdk_package_run_tests`, `azsdk_release_sdk` | Run the per-language generate / build / test / release pipeline locally. |
| Engsys / CODEOWNERS | `azsdk_engsys_codeowner_*` | Manage codeowners and labels. |

The **auto-generated** catalogue lives at `tools/azsdk-cli/Azure.Sdk.Tools.Cli/docs/mcp-tools.md` (regenerated on each release by the pipeline added in #13108).

**How it runs:**

```
Copilot CLI / VS Code (MCP client)
        │  spawns child process, JSON-RPC over stdio
        ▼
Azure.Sdk.Tools.Cli  (--mcp-server mode)
        │  each tool = one C# class under Tools/* implementing MCPNoCommandTool / InstrumentedTool
        ▼
Azure DevOps · APIView · GitHub · TypeSpec compiler · npm · dotnet · …
```

**The mock variant — `Azure.Sdk.Tools.Mock`:** a *separate* MCP server in the same repo (`tools/azsdk-cli/Azure.Sdk.Tools.Mock/`). It exposes the same tool names but returns pre-registered canned responses keyed on input arguments, and falls through to a generic `Success` when nothing matches. Evals point Copilot at this mock instead of the real `azsdk-cli` so a test run doesn't actually create release plans, post APIView comments, or hit Azure DevOps.

**Why this matters for evals:** the eval system's whole job is to verify that, given some user prompt, the model picks the *correct* tool from this catalogue, with the *correct* arguments, in the *correct* order. Bad tool descriptions, overlapping descriptions, or skill drift all show up here.

---

### 2.5.2 Skills, as we use them

**What a skill is, in this repo:** a `SKILL.md` file under `.github/skills/<skill-name>/` that tells Copilot "when the user wants to do *X*, follow these exact steps using these exact azsdk-cli MCP tools." It is markdown with YAML frontmatter. No compilation, no separate runtime.

**The skills we ship today** (live at [`.github/skills/`](https://github.com/Azure/azure-sdk-tools/tree/main/.github/skills)):

| Skill | What workflow it owns |
|---|---|
| `azsdk-common-prepare-release-plan` | Create / update an Azure SDK release plan work item end-to-end. |
| `azsdk-common-generate-sdk-locally` | Generate, build, and test an SDK from a TypeSpec spec on the developer's machine. |
| `azsdk-common-sdk-release` | Check readiness and trigger the release pipeline. |
| `azsdk-common-apiview-feedback-resolution` | Read APIView comments and propose code fixes. |
| `azsdk-common-pipeline-troubleshooting` | Diagnose a failing Azure SDK CI pipeline. |
| `azure-typespec-author` | Author / modify Azure TypeSpec specs (the Shanghai team's skill — has its own Vally eval setup). |
| `skill-authoring`, `sensei`, `markdown-token-optimizer` | Meta-skills for writing and maintaining the skills themselves. |

**What's inside a SKILL.md (real shape, abbreviated):**

```markdown
---
name: azsdk-common-prepare-release-plan
description: |
  **UTILITY SKILL**. USE FOR: "create release plan", "update release plan", "link SDK PR to plan", ...
  DO NOT USE FOR: SDK code generation, pipeline troubleshooting, API review feedback.
  INVOKES: azure-sdk-mcp:azsdk_create_release_plan, azure-sdk-mcp:azsdk_get_release_plan,
           azure-sdk-mcp:azsdk_link_sdk_pull_request_to_release_plan.
---

# Prepare Release Plan

## When to use
…
## Steps
1. Check whether a plan exists with `azsdk_get_release_plan`.
2. If none, call `azsdk_create_release_plan` with: packageName, language (case-sensitive: java|python|js|net|go), …
3. Confirm with the user, then `azsdk_update_release_plan`.
```

Notice the frontmatter explicitly lists **`INVOKES:`** with the MCP tool names — that's the contract between skill and MCP server, and it's what evals assert against.

**How Copilot picks a skill:** at runtime the agent host scans `.github/skills/`, reads the `description` block (the `USE FOR` / `DO NOT USE FOR` / `INVOKES` lines), and decides which skill (if any) applies to the user's request. Skills are discoverable, composable (one skill can reference another), and versioned in the same repo as the MCP server they call — so a tool rename and a skill update ship in the same PR.

**Skill ↔ MCP tool relationship:**

```
   User: "Start a release for @azure/foo"
         │
         ▼
   .github/skills/azsdk-common-prepare-release-plan/SKILL.md     ← recipe
         │  step 1 → azsdk_get_release_plan
         │  step 2 → azsdk_create_release_plan
         ▼
   azsdk-cli MCP server  (real)   ─or─   Azure.Sdk.Tools.Mock  (in evals)
```

**Analogy:** the MCP tools are the API; the skill is the instruction manual. You can have perfect tools and still get bad outcomes if the manual is wrong — which is exactly why evals exist and why most of them are *skill-level*, not tool-level.

**In eval terms:** "testing a skill" means feeding Copilot a prompt that should trigger that skill, letting it run against the mock MCP server, and grading whether the resulting trajectory matches the recipe (right tools, right order, right arguments, sensible final answer).

---

## 3. Why evals are hard (the conceptual jump)

A normal unit test: `add(2, 2) == 4`. Deterministic, instant, pass/fail.

An agent task: "ask Copilot to create a release plan for package X." The model may answer differently each time, call the right tools in a different order, or sometimes fail. Exact-match assertions break.

So eval reframes "correct" as:

- **Did the right MCP tools get called, with the right arguments?**
- **Did the agent produce an artifact that meets a rubric?**
- **Does it succeed consistently across N retries?** (`pass@k`, `pass^k`)
- **Does it improve when the skill is present vs. absent?** (A/B baseline)

This is what every generation of our eval system is trying to measure — they just disagree on *how*.

---

## 4. Generation history

### Generation 1 — In-house C# harness (Oct → Dec 2025)

**Goal:** Quickly stand up *something* that runs in CI and catches obvious MCP-tool regressions.

**Tech:** NUnit project at `tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations/`. Written from scratch in C#.

**What it tested:** Mostly **MCP-tool-level quality**:
- Are the tool descriptions distinct enough that the model picks the right one?
- Do single-prompt scenarios produce the expected tool calls?

**Key design choices:**
- **Mocked MCP responses** — auto-function-invocation was turned off so the harness could feed canned answers (avoids real API calls, makes runs deterministic).
- **HTML report** generated per run.
- **NUnit parallelism (level 5)** to keep wall time down while staying gentle on token quotas.

**CI integration:** Pipeline templates under `eng/common/pipelines/templates/steps/` triggered when `.github/copilot-instructions*.md` changed, fanned out to every `azure-sdk-for-*` repo via the eng/common sync.

**Status:** **Working, in production, but slated for replacement.**

---

### Generation 1.5 — Benchmarks (Feb → Apr 2026)

**Why it existed:** Gen-1 evals were small single-prompt checks. We also needed **longer end-to-end scenarios** — checkout a real repo, walk through a full workflow, score the agent's behavior.

**Tech:** Same C# project, separate "Benchmarks" subdir. Added:
- Sparse git checkout to pull only needed repo paths
- Token-budgeted reporting (long traces overflowed)
- Pipeline artifacts for reports
- Parallel execution

**Status:** **To be deleted.** The capability moved into Vally. Any pipelines still referencing it should be removed during the migration.

---

### Generation 2 — Migration to Vally (current, in progress)

**Why we switched:** Two homegrown frameworks (Gen-1 + Benchmarks) is two more frameworks than we want to maintain. Microsoft built **Vally** (`microsoft/evaluate`, package `@microsoft/vally-cli`) as a shared, pluggable eval platform for agents. We adopt it and delete our custom code.

**What Vally gives us:**
- A standard pipeline: **Stimulus → Trajectory → Graders → Score**
- Built-in skill linting (`vally lint`) — fast static check, perfect for PR CI
- Full agent eval (`vally eval`) — runs scenarios through an executor
- Pluggable **executors** (real Copilot SDK, or mock) and **graders** (static, LLM-as-judge, A/B)
- Tag-based filtering so one pipeline can target a single skill
- Reusable reporters (console, JUnit, Markdown, JSONL)

**Where Vally runs the agent:**

```
Vally  →  Copilot SDK executor  →  Copilot CLI  →  MCP server(s)
                                                    ├── real azsdk-cli MCP
                                                    └── Azure.Sdk.Tools.Mock (canned responses)
```

`Azure.Sdk.Tools.Mock` is a **separate MCP server** we wrote: it returns pre-registered responses keyed on inputs, and falls through to `Success` when no match is registered. It lets evals exercise a skill without actually creating release plans, posting APIView comments, etc.

Vally has since shipped its **own mock executor**, which could eventually replace `Azure.Sdk.Tools.Mock`. Worth evaluating during the migration.

**Migration PRs (the state of the world today):**

| PR | Purpose | Status |
|---|---|---|
| **#15376 — Migration waza skills to vally** | The flagship: replace the deprecated `azd waza` extension + `.waza.yaml` with `@microsoft/vally-cli`. Per-skill pipeline (Azure DevOps def **8165**). | **Not merged.** Vally was consuming huge amounts of memory (suspected: the Copilot CLI child process wasn't properly closing its MCP server). Pipeline disabled. Author intended to file an upstream issue. |
| **#15183 — Migrate Tool Evaluations to vally** | Port the Gen-1 "are MCP tool descriptions too similar?" eval into Vally. | **Open, low priority.** Prompts still need to be wired to their parameters. |
| **#15202 — Testing CI vally** | Trial CI integration of Vally. | Closed (was always a "do not merge" experiment, served its purpose). |

**Parallel effort (separate team):** The Shanghai folks already use Vally for `azure-typespec-author` evals at [`.github/skills/azure-typespec-author/evaluate`](https://github.com/Azure/azure-sdk-tools/tree/main/.github/skills/azure-typespec-author/evaluate). They run **live** (no mock), so it's slow but realistic. Setup is more complex because they spin up a QA-bot knowledge base. **Not merged into the main Vally config** yet — leave as-is for now, unify later if it pays off.

---

## 4.5 The PRs that built each generation

A chronological tour of `jeo02`'s eval-relevant PRs. Each block is the **load-bearing change** for that step in the story — i.e. if you want to understand *why* something in the repo looks the way it does, read these.

### Gen 1 — building the in-house C# eval harness (Oct–Nov 2025)

| PR | One-line summary | Why it matters |
|---|---|---|
| [#12534 — Provide Mocked MCP tool answers](https://github.com/Azure/azure-sdk-tools/pull/12534) | Turned **off** Copilot SDK's auto function invocation and started feeding tool responses manually from a dictionary. Also turned eval output into HTML reports. | First load-bearing move: without manual tool injection, evals would call real APIs every run. This is the seed of `Azure.Sdk.Tools.Mock`. |
| [#12623 — Single prompt evals](https://github.com/Azure/azure-sdk-tools/pull/12623) | Established the "one prompt = one scenario" pattern. | Defined the basic scenario shape every later eval inherits. |
| [#12627 — copilot instructions](https://github.com/Azure/azure-sdk-tools/pull/12627) | Added the copilot-instructions file the agent under test uses during evals. | Pins what the agent "knows" so eval results are reproducible. |
| [#12628 — Logger](https://github.com/Azure/azure-sdk-tools/pull/12628) | Shared logger for the eval harness. | Foundation for the HTML/JUnit reports. |
| [#12408 — Generate HTML view for evals](https://github.com/Azure/azure-sdk-tools/pull/12408) | Per-run HTML report with scenario-by-scenario detail. | First human-readable output — turned evals from "did CI pass?" into "*why* did it fail?" |
| [#12697 — Azsdk cli Evaluations](https://github.com/Azure/azure-sdk-tools/pull/12697) (**61 commits**) | The "drop the whole project in" PR. Created `Azure.Sdk.Tools.Cli.Evaluations`, wired it into the publish pipeline, sourced secrets from Key Vault, picked a default model. | **Single biggest eval PR ever.** Everything else in Gen-1 hangs off this. If you only read one PR to understand the C# harness, read this one. |

### Gen 1 — hardening & pipeline plumbing (Nov–Dec 2025)

| PR | One-line summary |
|---|---|
| [#12862 — Eval mock fix](https://github.com/Azure/azure-sdk-tools/pull/12862) | Bug fix in the mocked-tool dispatch logic. |
| [#12890 — Simplify Eval scenarios](https://github.com/Azure/azure-sdk-tools/pull/12890) | Refactor: less boilerplate per scenario. |
| [#12893 — verify prompts eval fix](https://github.com/Azure/azure-sdk-tools/pull/12893) | Fixed the "verify" scenario; demonstrates the typical "scenario flakes → patch grader/prompt" debugging loop. |
| [#12951 — Pipeline status check context + fix warning evals](https://github.com/Azure/azure-sdk-tools/pull/12951) | Wired eval pass/fail into the PR status check surface. |
| [#12995 — Azsdk cli eval tests pipeline](https://github.com/Azure/azure-sdk-tools/pull/12995) | Added the AI-evals pipeline templates under `eng/common/pipelines/templates/steps/`. **Triggered on changes to `copilot-instructions*.md` files, fanned out to every `azure-sdk-for-*` repo via the eng/common sync.** This is the "Gen-1 pipeline" everyone refers to. |
| [#13019 — Conditional testing](https://github.com/Azure/azure-sdk-tools/pull/13019) | Skip evals not affected by the changed files — keeps CI fast. |
| [#13025 — Change last pipeline failure status as warning](https://github.com/Azure/azure-sdk-tools/pull/13025) | Soft-fail behavior for known-flaky steps. |
| [#13142 — Generalize eval pipeline and switch endpoint](https://github.com/Azure/azure-sdk-tools/pull/13142) | Pulled hard-coded model endpoints out of the pipeline so it can target different envs. |
| [#13272 — Update auto-documentation.yml](https://github.com/Azure/azure-sdk-tools/pull/13272), [#13315 — Update trigger and PR settings](https://github.com/Azure/azure-sdk-tools/pull/13315), [#13326 — Evals Link Correction](https://github.com/Azure/azure-sdk-tools/pull/13326), [#13351 — Include all `.github` files in eval triggers](https://github.com/Azure/azure-sdk-tools/pull/13351), [#13372 — Evals touchup](https://github.com/Azure/azure-sdk-tools/pull/13372), [#13402 — More Eval Scenarios](https://github.com/Azure/azure-sdk-tools/pull/13402), [#13490 — Update Evaluation Docs](https://github.com/Azure/azure-sdk-tools/pull/13490) | Iterative polish of the Gen-1 system: more scenarios, broader triggers, docs catching up to reality. |
| [#13108 — Automate MCP list documentation](https://github.com/Azure/azure-sdk-tools/pull/13108), [#13373 — Formatting fix for mcp tool doc](https://github.com/Azure/azure-sdk-tools/pull/13373), [#13473 — Auto documentation job after publishing package](https://github.com/Azure/azure-sdk-tools/pull/13473) | Sibling effort: auto-regenerate `mcp-tools.md` / `mcp-commands.md` on every release so docs never drift. Related because the eval system reads tool metadata too. |

### Gen 1.5 — Benchmarks (Feb–Apr 2026)

| PR | One-line summary | Why it matters |
|---|---|---|
| [#14258 — Benchmarks test (ignore)](https://github.com/Azure/azure-sdk-tools/pull/14258), [#14256 — testing workflow (ignore)](https://github.com/Azure/azure-sdk-tools/pull/14256), [#14260 — Azsdk Benchmark CI](https://github.com/Azure/azure-sdk-tools/pull/14260) (closed) | Exploration / spike PRs. The closed ones evolved into #14420. |
| [#14268 — Azsdk Benchmarks parallelism](https://github.com/Azure/azure-sdk-tools/pull/14268) | NUnit `ParallelScope.All` (level 5) for benchmarks. Same trick as #13114 in Gen-1. |
| [#14374 — Benchmarks Reporting](https://github.com/Azure/azure-sdk-tools/pull/14374) | Report formats for the longer scenarios. |
| [#14404 — Benchmarks git sparse checkout](https://github.com/Azure/azure-sdk-tools/pull/14404) | **Key:** benchmarks operate on real repos, so we only sparse-checkout the paths the scenario actually needs. Cuts wall time and disk. |
| [#14420 — Benchmark CI](https://github.com/Azure/azure-sdk-tools/pull/14420) | The successful pipeline replacing the closed #14260. |
| [#14473 — Benchmark small fixes](https://github.com/Azure/azure-sdk-tools/pull/14473) | Polish. |
| [#14507 — Benchmark Eval Migration](https://github.com/Azure/azure-sdk-tools/pull/14507) | **Large restructure of the benchmark suite** — name says "migration" but it's an internal reorg, not the Vally migration. |
| [#14623 — Remove authoring-specific parameters](https://github.com/Azure/azure-sdk-tools/pull/14623) | Decouples benchmarks from one specific workflow's params. |
| [#14717 — Benchmark Report in artifacts](https://github.com/Azure/azure-sdk-tools/pull/14717), [#14734 — fix](https://github.com/Azure/azure-sdk-tools/pull/14734) | Reports surfaced as pipeline artifacts so reviewers can download them. |
| [#14837 — Benchmarks fix reporting going over token count](https://github.com/Azure/azure-sdk-tools/pull/14837) | Long traces were blowing past LLM context windows during report-time summarisation — capped and chunked. |

> **All of the above is scheduled for deletion** once the Vally migration completes. Treat as historical context, not active code.

### Gen 2 — Vally migration (Apr 2026 → present)

| PR | One-line summary | Why it matters |
|---|---|---|
| [#15202 — \[Do not merge\] Testing CI vally](https://github.com/Azure/azure-sdk-tools/pull/15202) | First CI spike: wire `@microsoft/vally-cli` into a pipeline to see if it survives end-to-end. | **The exploratory predecessor of #15376.** Closed but instructive — read the diff to see the minimal CI shape. |
| [#15183 — Migrate Tool Evaluations to vally](https://github.com/Azure/azure-sdk-tools/pull/15183) | Port the Gen-1 "are MCP tool descriptions too similar?" eval into Vally. | **Open, low priority.** Stuck on wiring prompts to their parameters. Picks back up after #15376. |
| [#15309 — azsdk-mcp mock](https://github.com/Azure/azure-sdk-tools/pull/15309) | Landed the `Azure.Sdk.Tools.Mock` MCP server (the one Vally points its mock executor at). | **Foundation for cheap CI runs** — without this, every eval would invoke real Azure DevOps / APIView APIs. |
| [#15313 — azsdk mcp mock but in cli](https://github.com/Azure/azure-sdk-tools/pull/15313) (draft, closed) | Move the mock into the CLI process itself rather than a separate server. | Abandoned direction — Vally's own mock executor may make it moot. |
| **[#15376 — Migration waza skills to vally](https://github.com/Azure/azure-sdk-tools/pull/15376)** | **The flagship migration.** Replaces the deprecated `azd waza` extension and `.waza.yaml` files with `@microsoft/vally-cli`. Sets up Azure DevOps pipeline **definition 8165**, one run per skill (using Vally's tag filtering). | **THE PR to read first** for picking up the migration. Not merged. Blocked on a Vally memory leak (suspected: Copilot CLI not closing the spawned MCP server cleanly). Pipeline 8165 is currently disabled. |

### Adjacent work (not eval infra, but on the things being evaluated)

These PRs change **skills and MCP tools** — which is what the eval system measures. Helpful context for understanding *what* the evals are protecting:

- **Release-plan / SdkReleaseTool tools:** [#13452](https://github.com/Azure/azure-sdk-tools/pull/13452), [#13504](https://github.com/Azure/azure-sdk-tools/pull/13504), [#13562](https://github.com/Azure/azure-sdk-tools/pull/13562), [#13593](https://github.com/Azure/azure-sdk-tools/pull/13593), [#13662](https://github.com/Azure/azure-sdk-tools/pull/13662), [#13681](https://github.com/Azure/azure-sdk-tools/pull/13681), [#14860](https://github.com/Azure/azure-sdk-tools/pull/14860), [#14910](https://github.com/Azure/azure-sdk-tools/pull/14910), [#15009](https://github.com/Azure/azure-sdk-tools/pull/15009), [#15145](https://github.com/Azure/azure-sdk-tools/pull/15145), [#15217](https://github.com/Azure/azure-sdk-tools/pull/15217), [#15255](https://github.com/Azure/azure-sdk-tools/pull/15255), [#15290](https://github.com/Azure/azure-sdk-tools/pull/15290), [#15323](https://github.com/Azure/azure-sdk-tools/pull/15323), [#15327](https://github.com/Azure/azure-sdk-tools/pull/15327) — most of the agent's "real work" lives here.
- **`prepare-release-plan` skill iteration:** [#15105](https://github.com/Azure/azure-sdk-tools/pull/15105), [#15259](https://github.com/Azure/azure-sdk-tools/pull/15259) (closed), [#15566](https://github.com/Azure/azure-sdk-tools/pull/15566) (latest, merged).
- **Contributor + docs:** [#12816 — Contributing guide](https://github.com/Azure/azure-sdk-tools/pull/12816), [#12820 — Language check case sensitivity](https://github.com/Azure/azure-sdk-tools/pull/12820).

---

## 5. The end-state we're aiming at

```
┌───────────────────────────────────────────────────────┐
│                       Vally                            │
│   one framework, one config, one CI definition         │
├───────────────────────────────────────────────────────┤
│   PR check  → vally lint                  (~seconds)   │
│   PR check  → vally eval --mock           (10–20 min)  │
│   Nightly   → vally eval (live executor)  (hours)      │
└────────────┬──────────────────────────────┬───────────┘
             │                              │
             ▼                              ▼
   All SKILL.md files                MCP server quality
   under .github/skills              (description clarity,
                                      tool selection, etc.)
```

Concretely:

1. **One Vally config** drives all skill evals (and ideally the MCP-tool-quality eval too).
2. **Fast lane**: `vally lint` on every PR — purely static, runs in seconds. Catches malformed SKILL.md files immediately.
3. **Medium lane**: full `vally eval` with the **mock executor** on every PR — ~10–20 min, no side effects, no live API calls.
4. **Slow lane**: a **nightly** (or weekly) full live run that actually invokes Copilot SDK + real MCP server. Catches things mocks can't (real LLM drift, real API contract changes).
5. **Old C# eval + benchmarks projects deleted**, along with their pipelines.

---

## 6. Where we actually are vs. that end-state

| Goal | Status |
|---|---|
| Vally framework chosen + adopted | ✅ Decision made |
| `vally lint` integration | ⚠️ Possible today, not yet wired into core CI |
| `vally eval` with mock executor | ⚠️ Working locally; CI pipeline (#15376) disabled due to memory leak |
| Nightly full live run | ❌ Not set up |
| Gen-1 evals migrated to Vally | ⚠️ #15183 in flight, low priority |
| Benchmarks deleted | ❌ Still present, pipelines still wired |
| Unified config (incl. Shanghai's TypeSpec evals) | ❌ Separate, deferred |
| Memory-leak root cause filed upstream | ❌ Not yet filed against `microsoft/evaluate` |

---

## 7. Mental-model glossary (just enough to read PRs and code)

| Term | Meaning |
|---|---|
| **Skill** | A `SKILL.md` file under `.github/skills/`. Teaches the agent how to perform one task. |
| **MCP server** | The tool surface the agent calls into. We own `azsdk-cli`'s. |
| **Eval** | A reproducible test of agent behavior. |
| **Stimulus** | One test case (a prompt + setup + expected behavior). |
| **Trajectory** | The recording of what the agent did during one run (tool calls, messages, tokens). |
| **Grader** | A function that scores a trajectory. Can be static (regex, file check), or an LLM judge. |
| **Executor** | The thing that actually runs the agent. Real (Copilot SDK) or mock. |
| **Vally** | Microsoft's general-purpose agent eval framework. The thing we're migrating to. |
| **Waza** | The deprecated `azd waza` extension we're migrating *away from* (its `.waza.yaml` files). |
| **Benchmarks** | Gen-1.5 long-form scenarios. Being deleted. |
| **`Azure.Sdk.Tools.Mock`** | Our custom MCP-server mock used by both old and new evals. |

---

## 8. Reading order if you want to dig in

1. **This file.**
2. The **Vally repo** you already have cloned (`microsoft/evaluate`) — skim `README.md`, `docs/architecture.md`, `docs/glossary.md`.
3. PR **#15376** — read the description and the disabled pipeline config. This is *the* migration artifact.
4. A real example: [`.github/skills/azure-typespec-author/evaluate`](https://github.com/Azure/azure-sdk-tools/tree/main/.github/skills/azure-typespec-author/evaluate) — Shanghai's working Vally setup.
5. PR **#15183** — the smaller, optional follow-up migration.
6. Old world (only if needed): `tools/azsdk-cli/Azure.Sdk.Tools.Cli.Evaluations/` and `tools/azsdk-cli/Azure.Sdk.Tools.Cli.Benchmarks/` to understand what we're replacing.
7. The mock: `tools/azsdk-cli/Azure.Sdk.Tools.Mock/`.

---

## 9. Big-picture summary in one paragraph

We test our Azure SDK agent by running its **skills** against canned scenarios and grading the resulting **trajectory**. We built two homegrown C# eval systems (regular evals + benchmarks) to do this, but they are being **replaced by Vally**, a shared Microsoft framework that does the same job with less custom code, more graders out-of-the-box, and a clean executor/mock split. The migration is **partially landed**: the framework choice is made, a flagship PR (#15376) is open but blocked on a Vally memory leak, and the eventual target is a three-tier CI (lint on every push, mocked eval on every PR, live nightly run) driving a single unified Vally config — with the old C# projects and benchmark pipelines deleted.
