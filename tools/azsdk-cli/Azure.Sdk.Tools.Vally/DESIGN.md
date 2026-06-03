# Vally Tool-Scenario Evaluation — Design

> Companion to [REQUIREMENTS.md](./REQUIREMENTS.md). Where REQUIREMENTS says
> *what* and *why*, this doc says *how*.

---

## 0. Scope

This design covers the eval framework that lives in
[`tools/azsdk-cli/Azure.Sdk.Tools.Vally/`](./) — i.e. the scenarios that
verify the Azure SDK agent picks the right skill, calls the right MCP tools,
in the right order, with the right arguments, and returns the right answer.

---

## 1. Layering

### 1.1 Three levels of testing

Aligned with the 2026-06 design review. Three named levels,
differentiated by **what they exercise** and **what backend they hit**:

| Level | Name | What it proves | Agent | MCP | Lives in |
|---|---|---|---|---|---|
| 0 | **Routing evals** | Prompt X routes to skill Y | live | none (no MCP server) | `.github/skills/<skill>/evals/` |
| 1 | **Workflow scenarios (mock)** | Agent picks the right skills, calls the right tools in the right order with the right args, returns the right answer | live | **mock** | `evals/scenarios/` *(default)* |
| 2 | **Live scenarios** | Same as level 1, but against the real backend — catches drift the mock can't see (TypeSpec ordering, real codegen output, real DevOps state) | live | **live** | `evals/scenarios/` + `tags: { live-safe: "true" }` |

Plus a hermetic tool-shape layer that isn't agent-driven:

| | Name | What it proves | Lives in |
|---|---|---|---|
| — | **Unit evals** | "Tool X exists and returns the right shape for these inputs." Cross-skill trigger tables. | `evals/unit/` |

**Mock is the default. Live is the exception.** Both modes drive the
same live agent (LLM), so **both incur agent token cost**; the mock
itself is a deterministic C# stub with no LLM inside it. The cost delta
between mock and live is on three other axes:

1. **Wall time.** Real backends (DevOps, codegen pipelines, GitHub) add
   seconds-to-minutes per tool call; the mock returns instantly.
2. **Backend side effects + quota.** Live hits real ADO work items,
   real pipeline runs, real PRs. Mock does none of that.
3. **Agent turn count (indirect token cost).** Real tool responses are
   larger and more variable, which expands per-turn input and provokes
   more retry / polling turns. The headline 1.78M tokens on the live
   release-planner-e2e run is mostly this effect, not the mock saving
   tokens directly.

Reviewer framing, paraphrased: *live MCP incurs significant token cost, so
most testing — including release plan and SDK generation — should use
mock; live is reserved for scenarios mock can't deterministically cover.*
The "token cost" pointed at there is (3) above plus the wall-time fan-out,
not a claim that the mock is free.

```
evals/
├── unit/            tool-shape + cross-skill triggers (hermetic)
├── scenarios/      level 1 by default; level 2 when tagged live-safe
├── setup/          shared fixture scripts (repo clone, etc.)
└── fixtures/       pinned SHAs + per-eval mocks
```

**Key property: scenarios are environment-agnostic.** A scenario YAML
declares the prompt, expected skills, expected tool sequence, and graders
— nothing about whether MCP is mock or live. Same file, same graders;
the MCP backend is picked at run time.

| Run mode | MCP | Repos? | When | Coverage | Cost |
|---|---|---|---|---|---|
| Level 1 (workflow / mock) | mock (deterministic stub, no LLM) | none | **every PR** | every scenario | agent tokens only; ~1m / scenario |
| Level 2 (live) | live (real backends) | shallow + sparse | **nightly** | scenarios tagged `live-safe` (curated subset) | agent tokens + real backend latency + more turns from real responses; 10-20m / scenario, ~2M agent tokens observed |

When the live and mock results disagree, the mock lied — exact bisect.
That's also how mock coverage gaps surface: every scenario that runs on
mock forces the mock to grow handlers for the tools it exercises
(see §4).

### 1.2 Relationship to `.github/skills/*/evals/` — split by ownership

Two homes, split on a simple rule:

| What it tests | Lives in | Owned by |
|---|---|---|
| **One skill** (does *this* skill route + call its tools + return a sensible answer) | `.github/skills/<skill>/evals/` | Skill author |
| **Cross-skill / cross-tool** (multi-step chains, e2e flows, mock-server integration, anything that doesn't belong to one skill) | `tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/` | Eval-framework owner |

Per-skill evals stay **next to `SKILL.md`** — that's the convention skill
authors expect, and it keeps "everything about my skill in one folder."
Today's per-skill `eval.yaml` files don't move.

This project owns:

- **The runner config** ([`.vally.yaml`](./.vally.yaml)): environments
  (`azsdk-mcp-live`, `azsdk-mcp-mock`), suites, MCP server definitions.
  Per-skill evals reference these environments by name.
- **Shared fixtures** ([`evals/setup/`](./evals/setup/),
  [`evals/fixtures/`](./evals/fixtures/)): the specs-clone hook, SHA locks,
  language-repo cache scripts. Per-skill evals can reuse them via `setup:`.
- **Cross-skill scenarios**: `evals/scenarios/` — multi-step flows like
  release-planner that span release-plan + generate-sdk. These have no
  single skill owner, so they live here.
- **Tier-1 `unit/` tool-trigger + tool-shape evals** that aren't owned by
  any one skill (e.g. `triggers-pipeline.eval.yaml` covers tools used by
  three different skills).

The runner picks up both:

```
vally eval \
  --eval-spec '.github/skills/**/evals/*.eval.yaml' \
  --eval-spec 'tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/**/*.eval.yaml' \
  --skill-dir .github/skills
```

Or, equivalently, suites in `.vally.yaml` glob both paths.

#### What skill authors get without moving anything

Per-skill evals already use Vally graders. To unlock the §4.6 trifecta
(skill + tool-calls + correctness in one scenario), a skill author edits
their existing `evals/eval.yaml` to **add** the missing graders — they
don't relocate the file:

```yaml
# .github/skills/<skill>/evals/eval.yaml
environment: azsdk-mcp-mock   # references env defined in our .vally.yaml
graders:
  - type: skill-invocation
    config: { required: [<skill-name>] }
  - type: tool-calls
    config: { required: [...], disallowed: [...] }
  - type: prompt
    config: { rubric: ... }
```

#### Why this split (not "move everything here")

| Concern | Skill evals here (move) | Skill evals stay + cross-cuts here (this proposal) |
|---|---|---|
| Skill author finds their evals | filtered by tag, ~5 dirs away | next to SKILL.md ✓ |
| Skill + tool-calls + correctness in same scenario | ✓ | ✓ (add graders to existing file) |
| Cross-skill chains have a clear home | ✓ | ✓ (`evals/scenarios/`) |
| New skill author understands the layout | needs to learn tag filtering | "evals go next to your SKILL.md, like other skills" |
| Per-skill CI workflow unchanged | needs rewrite | ✓ |
| Mock vs. live opt-in works for skill evals | ✓ | ✓ (env defined here, referenced from skill eval) |
| New contributor adding cross-skill scenario | unclear (which tag?) | `evals/scenarios/` here |



### 1.3 Folder → suite → trigger mapping

Suites in `.vally.yaml`:

| Suite | Globs | Env | Used by |
|---|---|---|---|
| `unit` | `evals/unit/**/*.eval.yaml` | `azsdk-mcp-mock` | PR + nightly |
| `scenarios-mock` | `evals/scenarios/**/*.eval.yaml` | `azsdk-mcp-mock` | PR + nightly |
| `scenarios-live` | `evals/scenarios/**/*.eval.yaml` (filtered by tag `live-safe`) | `azsdk-mcp-live` | nightly + label |
| `pr-gate` | `unit` + `scenarios-mock` | mock | every PR |
| `nightly` | `unit` + `scenarios-mock` + `scenarios-live` | mixed | nightly + label |

Live runs are tag-gated (`--tag live-safe`) so destructive / production-only
scenarios stay opted out by default.

### 1.4 Decision tree for "where does my new eval go?"

```
Does it only test that the right skill is picked (no tool calls)?
└── yes → Level 0: .github/skills/<skill>/evals/   (not this project)

Is it a single-tool shape test or a trigger table covering tools used by ≥2 skills?
└── yes → evals/unit/

Is it a multi-step / multi-tool agent flow?
└── yes → evals/scenarios/
        ├── Level 1 by default: runs against MOCK on every PR.
        │   *Use this unless the mock can't faithfully cover the behavior.*
        └── Level 2: add `tags: { live-safe: "true" }` to ALSO run nightly
            against live MCP. Reserve for cases where the real backend's
            behavior matters (TypeSpec ordering, real codegen output,
            real DevOps state).
```

---

## 2. CI

### 2.1 Today

- The skill evals (`.github/skills/**/evals/`) run via
  [`.github/workflows/skill-eval.yml`](../../../.github/workflows/skill-eval.yml).
- The tool-scenario evals in this project: **run nowhere in CI**. They run
  by hand today. This is the gap [#15829](https://github.com/Azure/azure-sdk-tools/issues/15829)
  closes.

### 2.2 Next (issue #15829)

Extend `.github/workflows/skill-eval.yml` — **do not** create a parallel
workflow. Two new jobs join the existing per-skill matrix:

```yaml
jobs:
  # existing: skill-evals (matrix per skill, unchanged)

  tool-scenarios-pr:
    # PR-gate: unit + scenarios-mock. Hermetic: no live MCP, no repo clones.
    if: github.event_name == 'pull_request'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-dotnet-and-node
      - run: dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Mock
      - working-directory: tools/azsdk-cli/Azure.Sdk.Tools.Vally
        run: |
          npx --yes @microsoft/vally-cli eval \
            --suite pr-gate \
            --skill-dir ../../../.github/skills \
            --junit --output-dir vally-results
      - uses: actions/upload-artifact@v4
        with: { name: vally-pr, path: tools/azsdk-cli/Azure.Sdk.Tools.Vally/vally-results }

  tool-scenarios-nightly:
    # Nightly: full suite incl. scenarios-live (tag-gated by `live-safe`).
    if: github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-dotnet-and-node
      - name: Restore repo cache
        uses: actions/cache@v4
        with:
          path: ~/.vally-cache/repos
          key: vally-repos-${{ hashFiles('tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/scenarios/*.yaml') }}
      - name: Prime repo cache (clones any missing live-safe deps)
        run: pwsh tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/setup/ensure-repos.ps1
        env: { VALLY_REPO_CACHE: ~/.vally-cache/repos }
      - run: dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli
      - run: dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Mock
      - working-directory: tools/azsdk-cli/Azure.Sdk.Tools.Vally
        run: |
          npx --yes @microsoft/vally-cli eval \
            --suite nightly \
            --skill-dir ../../../.github/skills \
            --junit --output-dir vally-results
        env:
          AZSDKTOOLS_AGENT_TESTING: "true"
          AZURE_DEVOPS_PAT: ${{ secrets.AZURE_DEVOPS_PAT }}
          VALLY_REPO_CACHE: ~/.vally-cache/repos
        continue-on-error: true   # advisory for first week
      - uses: actions/upload-artifact@v4
        with: { name: vally-nightly, path: tools/azsdk-cli/Azure.Sdk.Tools.Vally/vally-results }
```

Triggers:

| Trigger | What runs |
|---|---|
| `pull_request` | `pr-gate` (unit + scenarios-mock) |
| `schedule:` nightly | `nightly` (adds scenarios-live) |
| `workflow_dispatch` | manual escape hatch (suite picker) |

### 2.3 Repo-caching strategy (issue #15831)

**Problem.** Live-env scenarios call real tools that read files from
`azure-rest-api-specs` (and sometimes a language repo like
`azure-sdk-for-python`). Cloning from scratch on every run is slow
(~30s sparse spec clone, minutes for a full SDK repo). We need them
available without paying that cost per eval.

**Who needs this:** only the `scenarios-live` nightly job. PR-gate
(`unit` + `scenarios-mock`) runs entirely against the mock server — no
clones, no network, no cache.

**Constraint.** Vally itself does not clone repos. Its
`environment.git.source` field expects a worktree to already exist at
the given path. So cloning must happen as a pre-step *before*
`vally eval` runs.

**Solution — pre-step script, scoped to live scenarios only.** Each
scenario tagged `live-safe` declares its repo deps in a sidecar block.
(Vally's `tags:` is a mapping, not an array; `metadata:` is accepted by
the linter as a passthrough — we use it for our own bookkeeping.)

```yaml
# evals/scenarios/release-planner.eval.yaml
tags:
  live-safe: "true"
metadata:
  repos:
    - name: Azure/azure-rest-api-specs
    - name: Azure/azure-sdk-for-python
stimuli:
  - environment:
      git:
        source: ${VALLY_REPO_CACHE}/Azure/azure-rest-api-specs   # filled by pre-step
        ref: main
```

Live runs select these scenarios via `--tag live-safe=true`.

One generic script — [`evals/setup/ensure-repos.ps1`](./evals/setup/ensure-repos.ps1)
— walks `evals/scenarios/*.yaml`, **filters to scenarios tagged
`live-safe`**, collects the union of `metadata.repos`, and ensures each
listed repo is cloned into `$VALLY_REPO_CACHE/<owner>/<repo>`.
Idempotent: existing checkouts are skipped. Scenarios without the
`live-safe` tag are not scanned — their repos (if any are declared)
are never cloned, because they never run live.

- **Local dev:** `$VALLY_REPO_CACHE` defaults to
  `$env:USERPROFILE\.vally-cache\repos`. Reused across local runs.
- **CI:** `$VALLY_REPO_CACHE` points at whatever `actions/cache` mounts.
  Cache key = hash of the collected `metadata.repos` list across all
  `live-safe` scenarios, so the cache only invalidates when a scenario
  adds/removes a repo dependency.

**On pinning (optional for v1).** By default the script clones `main`,
which means live-env evals can flake if upstream merges a breaking change
between nightly runs. Scenarios that want reproducibility can opt in per
repo by adding a `ref:` field under `metadata.repos`:

```yaml
metadata:
  repos:
    - name: Azure/azure-rest-api-specs
      ref: <sha-or-branch>          # optional; default = main
    - name: Azure/azure-sdk-for-python
```

No central lock file, no bot PR — just an optional field on the repo
entry. "What version did this run use?" is always recoverable from the
`git rev-parse HEAD` recorded in `results.jsonl`. If per-scenario `ref:`
entries get unwieldy we can promote them to a shared lock file later.

**Upstream wish.** A native Vally `fixtures.git:` block that clones for
us would let us drop the pre-step script and the `metadata` sidecar.
Filed separately; until then, the pre-step is the pragmatic v1.

---

## 3. Live vs. mock — what runs where

### 3.1 Decision matrix

| What | Env | Why |
|---|---|---|
| Tool triggers (`evals/unit/triggers-*.eval.yaml`) | **mock** | No real tools called, just verifies prompt → tool name mapping. |
| Single-tool shape (`evals/unit/<tool>.eval.yaml`) | **mock** | Hermetic, fast, deterministic. |
| Scenario, default PR run (`evals/scenarios/*.eval.yaml`) | **mock** | Cheap, hermetic, no repo clones, safe for write tools (mocked). Catches tool-sequence regressions on every PR. |
| Scenario tagged `live-safe`, nightly run | **live** w/ `AZSDKTOOLS_AGENT_TESTING=true` | Catches real DevOps / GitHub drift. Work items route to test area path so re-runs are safe. PR creation hits real `azure-sdk-for-*` (proven 2026-06-02). |
| Scenario touching **production** systems (e.g. `azsdk_release_sdk` shipping to NuGet) | **mock only** — no `live-safe` tag | Never run live in CI. |

The `live-safe` tag is the opt-in: by default a new scenario runs only on
mock. To also have it run live nightly, the author adds
`tags: { live-safe: "true" }`
and confirms the scenario is safe to repeat against the real systems.

### 3.2 How it's expressed in YAML

`.vally.yaml` declares two environments:

```yaml
environments:
  azsdk-mcp-live:
    mcpServers:
      azure-sdk-mcp:
        command: dotnet
        args: ["run", "--project", "../Azure.Sdk.Tools.Cli", "--", "start"]
        env:
          AZSDKTOOLS_AGENT_TESTING: "true"   # safe-mode for write tools
  azsdk-mcp-mock:
    mcpServers:
      azure-sdk-mcp:
        command: dotnet
        args: ["run", "--project", "../Azure.Sdk.Tools.Mock", "--", "start"]
```

Each eval is environment-agnostic; the env is bound at run time by the
suite (see §1.3). Default suite uses **mock**; `scenarios-live` swaps in
`azsdk-mcp-live` and filters via `--tag live-safe=true`.

`AZSDKTOOLS_AGENT_TESTING=true` is the safety net — even on the live MCP,
write operations route to test work-item area paths. This is what made
today's release-planner scenario safe to re-run.

---

## 4. Mock MCP server status

### 4.1 What's there today

[`Azure.Sdk.Tools.Mock`](../Azure.Sdk.Tools.Mock/) has handlers for **10 tools**:

| Tool | Handler |
|---|---|
| `azsdk_create_release_plan` | [`Handlers/ReleasePlan/CreateReleasePlanHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/ReleasePlan/CreateReleasePlanHandler.cs) |
| `azsdk_get_release_plan` | [`GetReleasePlanHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/ReleasePlan/GetReleasePlanHandler.cs) |
| `azsdk_update_sdk_details_in_release_plan` | [`UpdateSdkDetailsHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/ReleasePlan/UpdateSdkDetailsHandler.cs) |
| `azsdk_update_release_plan` | [`UpdateReleasePlanTargetHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/ReleasePlan/UpdateReleasePlanTargetHandler.cs) |
| `azsdk_run_generate_sdk` | [`RunGenerateSdkHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/ReleasePlan/RunGenerateSdkHandler.cs) |
| `azsdk_link_sdk_pull_request_to_release_plan` | [`LinkSdkPrToReleasePlanHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/ReleasePlan/LinkSdkPrToReleasePlanHandler.cs) |
| `azsdk_link_namespace_approval_issue` | [`LinkNamespaceApprovalHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/ReleasePlan/LinkNamespaceApprovalHandler.cs) |
| `azsdk_get_sdk_pull_request_link` | [`GetSdkPullRequestLinkHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/ReleasePlan/GetSdkPullRequestLinkHandler.cs) |
| `azsdk_get_pipeline_status` | [`GetPipelineStatusHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/Pipeline/GetPipelineStatusHandler.cs) |
| `azsdk_release_sdk` | [`ReleaseSdkHandler.cs`](../Azure.Sdk.Tools.Mock/Handlers/Package/ReleaseSdkHandler.cs) |

Today's release-planner scenario used **15 distinct tools** when run live.
So at minimum the mock is missing handlers for:
`azsdk_get_release_plan_for_spec_pr`, `azsdk_run_typespec_validation`,
`azsdk_check_api_spec_ready_for_sdk`, `azsdk_typespec_generate_authoring_plan`,
plus other `azsdk_*` tools referenced by the `unit/` evals.

### 4.2 Is it up to date?

No — there's no mechanism to detect drift. The mock is a hand-authored
allowlist; if `Azure.Sdk.Tools.Cli` adds a tool, no one knows the mock is
missing it until an eval fails.

### 4.3 How we keep it up to date

Three layers:

1. **Inventory diff check** (lightweight, lands first):
   - New script `eng/scripts/Get-McpToolInventory.ps1` enumerates tools
     advertised by both `Azure.Sdk.Tools.Cli` and `Azure.Sdk.Tools.Mock` over
     stdio (both already expose `tools/list` via MCP).
   - Writes `tools/azsdk-cli/Azure.Sdk.Tools.Mock/COVERAGE.md` (checked in)
     with three columns: tool, live ✓, mock ✓.
   - CI job `mock-coverage-check` runs the script and fails if `COVERAGE.md`
     is stale (regenerate → `git diff --exit-code`).

2. **Per-eval enforcement** (already free via the runner):
   - Any eval with `environment: azsdk-mcp-mock` that calls a tool the mock
     doesn't handle will fail at runtime ("tool not found"). This is the
     functional backstop — once an eval references a missing tool, CI red.

---

## 5. Results UX — beyond "pass / fail"

### 5.1 What we have today

Per run, Vally writes:

- `results.jsonl` — full trajectory: every tool call, args, return values,
  events, metrics.
- `eval-results.md` — markdown summary table (one row per stimulus, grader
  scores, links to details).
- JUnit XML (with `--junit`) — for CI test-results widgets.

Both produced today for the release-planner-e2e run; see
[`vally-results/2026-06-03T03-06-41-076Z/`](./vally-results/2026-06-03T03-06-41-076Z/).

The gap: `results.jsonl` is great for engineers but useless for non-engineer
stakeholders who want to *see* why a run failed (or what the agent actually did).

### 5.2 What non-engineer stakeholders actually want

From the meeting: a way to slice/filter results across many runs ("how often
does the release-planner skill fire in the last 30 nightlies?") and to drill
into a single failing run without parsing JSON.

Two artifacts cover that:

#### (a) CSV export — the spreadsheet layer

Thin post-processor: `eng/scripts/Export-VallyResultsCsv.ps1`. Reads
`results.jsonl`, emits one row per stimulus with columns:

```
timestamp, suite, scenario, tier, model, verdict, score,
skill-invocation, tool-calls, prompt,
skills_used, tool_call_count, turns, tokens, duration_s,
trajectory_url, eval_results_url
```

`trajectory_url` is a link to the rendered HTML (see (b) below). Append-only
file at `vally-results/history.csv` (committed to a separate `vally-history`
branch or pushed to an Azure Storage container — TBD).

This is the artifact non-engineer stakeholders get — one file, pivot-table friendly.

#### (b) Trajectory viewer — the "what did the agent actually do" layer

`results.jsonl` already contains the full event stream:

```
skill → tool_call → tool_result → assistant_message → tool_call → ...
```

We render it as a single static HTML page per stimulus:

- Timeline view: vertical events with timestamps and durations.
- Each tool_call collapsible: arguments + return value side-by-side.
- Skill changes highlighted as section headers.
- Final assistant message at the bottom with the grader rubric + judge verdict.
- Graders shown as a pill row at the top (✅/❌ with hover for details).

Implementation: `eng/scripts/Render-VallyTrajectory.ps1` (or a tiny Node
script) that templates a single self-contained HTML. CI uploads the directory
as an artifact; the CSV links into it.

This is essentially what `agentviz` (referenced in
`--keep-executor-session-logs`) does, but standalone — no extra tool to
install.

#### (c) Future: shared dashboard

Once (a) and (b) are stable, the CSV can feed a Power BI / Kusto dashboard
that lives outside this repo. Out of scope for v1.

### 5.3 Pipeline

```
vally eval ──> results.jsonl ─┬─> Export-VallyResultsCsv.ps1 ──> history.csv ──> dashboard
                              │
                              └─> Render-VallyTrajectory.ps1 ──> *.html ──> artifact + link from csv
```

Both scripts read `results.jsonl` only — no Vally-side changes required.
If/when upstream Vally adds native CSV / HTML output, drop the scripts.

---

## 6. Performance & cost controls

### 6.1 Principle

The framework must make expensive evals **fail loudly**, not silently bleed
CI minutes and tokens. An author writing a new scenario should not have to
know in advance how much it costs; the runner tells them, and refuses to
keep running it if it crosses policy. Polishing individual scenarios is
not a substitute for this — it doesn't scale to the next ten authors.

The release-planner e2e run (17 min wall / 1.78M tokens / 41 turns) is the
existence proof: nothing in the framework today would have stopped it
landing as a "passing" scenario that quietly costs a full hour of CI per
nightly trigger.

### 6.2 Budgets and enforcement

Every scenario carries a budget. The runner measures actual cost and
enforces the budget in three bands:

| Band | Trigger | Effect |
|---|---|---|
| Soft (warn) | actual ≥ 50% of budget | Logged + surfaced in `eval-results.md` |
| Hard (fail) | actual > 100% of budget | Scenario marked **failed**, CI job fails |
| Kill (abort) | actual > 200% of budget | Run aborted mid-flight, partial trajectory saved |

Budgeted dimensions, in declining order of importance:

1. **`maxTurns`** — single best proxy for cost; bounds the agent loop.
2. **`maxWallSec`** — protects CI minutes regardless of where time goes.
3. **`maxBillableTokens`** — input (uncached) + output. Cache hits don't
   count, so the number tracks real $.
4. **`maxToolCalls`** — catches exploration spirals.

Defaults are set globally in `.vally.yaml`, overridable per scenario:

```yaml
# .vally.yaml
defaults:
  limits:
    maxTurns: 20
    maxWallSec: 120
    maxBillableTokens: 100_000
    maxToolCalls: 30
```

A scenario that *needs* more must opt in explicitly with a comment
explaining why. The opt-in itself is reviewable in code:

```yaml
# evals/scenarios/release-planner.eval.yaml
limits:
  maxTurns: 60          # multi-step chain; see DESIGN §6.4
  maxWallSec: 600       # waits on real ADO pipeline status
  maxBillableTokens: 250_000
```

If the opt-in budget gets reviewed and rejected, the author's recourse
is to **switch to mock**, not to widen the budget. This is the lever
that pushes cost-blind scenarios off the live path.

### 6.3 Tiered policy: PR vs nightly

Budgets differ by tier. The PR gate is the strict one because it runs on
every push; nightly can be looser because it runs once.

| Tier | maxTurns | maxWallSec | maxBillableTokens | Opt-out |
|---|---|---|---|---|
| PR gate (unit + mock) | 20 | 120 | 100k | not allowed |
| Nightly mock | 30 | 300 | 200k | reviewable |
| Nightly live | 60 | 600 | 500k | reviewable, requires justification comment |

A scenario that wants to exceed the PR-gate ceiling **must** drop down
to nightly. The runner refuses to load over-budget scenarios into the
PR-gate suite. No way to silently land a slow scenario.

### 6.4 General guardrails (framework-level, not per-scenario)

These apply to every scenario the runner executes. None require the
scenario author to know about them.

| # | Guardrail | Layer | What it prevents |
|---|---|---|---|
| G1 | **Hard turn / wall / token / tool-call caps** (§6.2) | runner | Runaway scenarios |
| G2 | **Virtual clock**: executor intercepts `Start-Sleep` / `Wait-*` / `sleep` and fast-forwards | executor adapter | Wall-time waste on polling loops |
| G3 | **Tool-result truncation** above N tokens with `…[truncated]` marker | executor adapter | Context blow-up from chatty tool responses |
| G4 | **Narration / meta-tool suppression**: tools that only echo intent (`report_intent` etc.) stripped from the tool list the model sees | executor config | Doubled turn count from pure-narration calls |
| G5 | **Polling tools default to terminal state under `AZSDKTOOLS_AGENT_TESTING=true`** (`*_get_*_status` returns `Succeeded` on first poll) | mock MCP policy | Any future polling tool inherits the fix |
| G6 | **Cheaper judge model** — LLM-judge graders default to a smaller model than the agent | runner config | Judge tokens dominating output cost |
| G7 | **CI concurrency cancel** — superseded PR runs killed immediately | CI workflow | Wasted compute on rapid pushes |
| G8 | **Honest cost reporting** — `eval-results.md` splits cached vs billable input and wall time into LLM / tool / wait | results renderer | Headline-token illusions hiding real cost |
| G9 | **Suite-level cost ceiling** — if any single scenario exceeds 25% of its suite's total budget, suite run fails with a "rebalance" error | runner | One scenario silently dominating the suite |

G2, G3, G4, G6 also have the effect of making per-scenario budgets
*achievable*. Without them, an honest scenario can blow past `maxTurns`
just by being routed through a chatty executor.

### 6.5 Where each guardrail lives

| Guardrail | Owner |
|---|---|
| G1, G2, G3, G8, G9 | **Upstream Vally** — file as feature requests |
| G4, G6 | Copilot SDK executor / `.vally.yaml` runner config in this repo |
| G5 | `Azure.Sdk.Tools.Mock` in this repo |
| G7 | `.github/workflows/skill-eval.yml` in this repo |

The local guardrails (G4–G7) can land immediately. The upstream
guardrails (G1–G3, G8, G9) are blocked on Vally; until they ship, we
approximate G1 with a thin post-run check that reads `results.jsonl`
and fails the CI step if any scenario exceeded its declared budget.

### 6.6 Author-facing rule of thumb

> **If the agent's loop talks to a real backend that takes more than a
> few seconds to respond, mock it.** The runner will let you know when
> you've crossed the line — you don't have to guess.

The budget machinery exists so authors don't need to read this document
to write a cheap eval. They write the scenario; the runner fails it if
it costs too much; the CI message points them at the mock path.

---

## 7. Open design questions

1. **Mock auto-generation.** Replace hand-written handlers with a single
   generic handler that synthesizes a response from each tool's JSON
   schema.

   **How.** `Azure.Sdk.Tools.Mock` starts up, calls the live MCP's
   `tools/list` once at boot (or reads a checked-in snapshot of it), and
   for every tool registers a fallback handler that:

   1. Validates incoming args against `inputSchema`.
   2. Walks `outputSchema` (or the `result` JSON Schema) and emits a
      default-value tree: `string` → `"mock-<field>"`, `integer` → `0`,
      `array` → `[]`, `object` → recurse, `$ref` → resolve. For ID-shaped
      fields (e.g. `workItemId`), return a deterministic counter so
      multi-step scenarios can chain (`create` returns `1538`, next
      `get(1538)` returns the same shape).
   3. Hand-written handlers in `Handlers/` still win when present — they
      override the generated default for the few tools whose realistic
      response shape matters (e.g. `azsdk_get_pipeline_status` needs
      a believable build-status sequence).

   **Trade-off.** Solves drift (any new tool gets a mock for free) but
   default-value responses miss domain quirks (real pipeline status
   transitions, real PR URLs). Mitigation: keep hand-written handlers
   for the ≥5 tools whose responses scenarios actually assert on.

   **Defer until.** §4 manual coverage gap is closed (so we know which
   tools actually need realistic shapes vs. which can take defaults).
2. **CSV storage.** Per-branch artifact (cheap, no infra), commit to a
   `vally-history` branch (versioned, awkward), or push to Azure Storage
   (best UX, needs infra). Default plan: artifact + Storage upload from
   nightly only.
3. **Cross-org repo cache in CI.** `actions/cache` keyed on the hash of
   `metadata.repos` across live-safe scenarios is fine for
   `azure-rest-api-specs`, but the pull from GitHub still costs ~30s on
   cache miss. For 5 language repos + specs, cold-start could approach 3
   min. Worth it vs. an Azure-hosted pre-baked image? Defer until we have
   data.

