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

### 1.1 How many layers, and why

Two layers, in one place. They are **not** two separate projects — they are
two sub-folders under [`evals/`](./evals/) plus matching suites in
[`.vally.yaml`](./.vally.yaml). Skill-only dispatch ("does prompt X route to
skill Y?") is **not** a tier here — it lives next to the skill in
`.github/skills/<skill>/evals/` (see §1.2).

```
evals/
├── unit/            tier 1: cross-skill tool triggers + single-tool happy path
├── scenarios/      tier 2: multi-tool agent flows (mock OR live, same YAML)
├── setup/          shared fixture scripts (specs clone, etc.)
└── fixtures/       pinned SHAs + per-eval mocks
```

| Tier | Folder | Agent? | What it proves | Wall time | Failure semantics |
|---|---|---|---|---|---|
| 1 unit | `evals/unit/` | none | "Tool X exists and returns the right shape for these inputs." Cross-skill trigger tables (tools used by ≥2 skills). | < 30s each | **required**, every PR |
| 2 scenarios | `evals/scenarios/` | **live (gpt-5.x)** | "Agent picks the right skills, calls the right tools in the right order with the right args, and returns the right answer" for a multi-step ask. | depends on env (see below) | depends on env (see below) |

**The key insight: scenarios are environment-agnostic.** A scenario YAML
declares the prompt, expected skills, expected tool sequence, and graders
— nothing about whether MCP is mock or live. The MCP backend is picked at
run time:

| Run mode | MCP | Repos? | When | Coverage | Cost |
|---|---|---|---|---|---|
| `scenarios` + `azsdk-mcp-mock` | mock | none | **every PR** | every scenario | ~1m / scenario, ~0 tokens beyond agent |
| `scenarios` + `azsdk-mcp-live` | live | shallow + sparse | **nightly** | scenarios tagged `live-safe` (curated subset) | 10-20m / scenario, ~2M tokens |

Same file, same graders — just a different environment binding. A scenario
like `release-planner` runs on **mock** every PR (catches tool-sequence
regressions cheaply) **and** on **live** nightly (catches real DevOps
drift). When the live and mock results disagree, you have an exact bisect:
the mock lied. That's also how mock coverage gaps surface automatically —
every scenario that runs on mock forces the mock to grow handlers for the
tools it exercises (see §4).

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
| Shanghai team adding cross-skill scenario | unclear (which tag?) | `evals/scenarios/` here |



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
Does it test ONE skill's routing + tools + answer?
└── yes → .github/skills/<skill>/evals/   (not this project)

Is it a single-tool shape test or a trigger table covering tools used by ≥2 skills?
└── yes → evals/unit/

Is it a multi-step / multi-tool agent flow?
└── yes → evals/scenarios/
        ├── default: runs against mock on every PR
        └── add `tags: { live-safe: "true" }` to also run against live nightly
```

---

## 2. CI

### 2.1 Today

- The skill evals (`.github/skills/**/evals/`) run via
  [`.github/workflows/skill-eval.yml`](../../../.github/workflows/skill-eval.yml).
- The tool-scenario evals in this project: **run nowhere in CI**. Helen runs
  them by hand. This is the gap [#15829](https://github.com/Azure/azure-sdk-tools/issues/15829)
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

The gap: `results.jsonl` is great for engineers but useless for Laurent or
anyone wanting to *see* why a run failed (or what the agent actually did).

### 5.2 What Laurent / non-engineers actually want

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

This is the artifact Laurent gets — one file, pivot-table friendly.

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

## 6. Open design questions

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

