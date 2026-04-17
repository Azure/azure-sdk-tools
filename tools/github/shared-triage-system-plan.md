# Shared Triage & Workflow System — Plan (v1)

**Status:** Draft proposal — starting point for team discussion. Not a final design.

**Owner (proposed):** Azure SDK DevExp / Tools team

**Last updated:** Initial draft

---

## Summary

We propose a shared library of **triage and workflow instruction files (a.k.a. "skills")** maintained centrally by the Azure SDK Tools / DevExp team and consumed by individual SDK language repos (and any other Azure repo that wants to opt in). Language teams build their own agent-driven workflows in their repos and *point at* the shared skills, optionally layering repo-specific overrides on top. A monitoring layer verifies that consumer workflows actually perform the minimum required actions (labeling, triage comments, codeowner pings, etc.) on real issues.

This is intentionally a **v1 / starter plan**. The goal is to align on shape and primitives so we can prototype one end-to-end loop quickly, then iterate.

---

## Goals

- **Codify triage and workflow knowledge once.** Today every SDK language repo reinvents triage rules, label taxonomies, escalation paths, and "what an agent should do on a new issue." We want a single canonical source.
- **Let teams own their automation.** Each repo configures *its own* agent runner / GitHub workflow. We don't push a runtime on anyone — we just provide the instructions the runner consumes.
- **Make customization first-class.** Repo-specific overrides are expected, not an afterthought. Teams can extend or partially override shared skills without forking them.
- **Verify it actually works.** "We shipped a skill" is not success. Success is "issue #1234 in repo X got labeled within N hours and the right codeowner was pinged." We need monitoring + contract tests to prove the loop is closed.
- **Stay agent-runtime agnostic.** Skills should be readable by Copilot coding agent, custom GitHub Actions runners, or future agent platforms. Markdown + a small amount of metadata, not a proprietary DSL.

## Non-goals

- **We are not replacing human triage decisions.** Agents propose; humans (or codeowners) decide on anything ambiguous or high-impact.
- **We are not mandating a specific agent runtime.** Teams can use Copilot coding agent, a custom workflow, or even run skills manually as a checklist.
- **We are not building a new label taxonomy from scratch.** We'll reference and extend the existing `common-labels.csv` work.
- **We are not building a centralized triage bot that runs cross-repo.** Each repo runs its own automation; we provide instructions and monitoring.
- **We are not solving PR review automation in v1.** Issue triage and lifecycle only. PR workflows are a likely v2.

---

## Architecture

### High-level shape

```
┌─────────────────────────────────────────────────┐
│  Azure/azure-sdk-tools (this repo)              │
│  ┌───────────────────────────────────────────┐  │
│  │ tools/github/shared-skills/               │  │
│  │   issue-triage/SKILL.md                   │  │
│  │   issue-workflow/SKILL.md                 │  │
│  │   codeowner-routing/SKILL.md              │  │
│  └───────────────────────────────────────────┘  │
│                       │                          │
│                       │ sync (TBD mechanism)     │
│                       ▼                          │
└─────────────────────────────────────────────────┘
            │              │             │
            ▼              ▼             ▼
   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
   │ azure-sdk-   │ │ azure-sdk-   │ │ azure-sdk-   │
   │   for-python │ │   for-js     │ │   for-net    │
   │              │ │              │ │              │
   │ .github/     │ │ .github/     │ │ .github/     │
   │  shared-     │ │  shared-     │ │  shared-     │
   │  skills/     │ │  skills/     │ │  skills/     │
   │  overrides/  │ │  overrides/  │ │  overrides/  │
   │  workflow.yml│ │  workflow.yml│ │  workflow.yml│
   └──────────────┘ └──────────────┘ └──────────────┘
            │              │             │
            └──────────────┴─────────────┘
                           │
                           ▼
            ┌─────────────────────────────────┐
            │ Monitoring & compliance tests   │
            │ (scheduled, runs from this repo)│
            └─────────────────────────────────┘
```

### Where shared skills live

Proposed path in this repo:

```
tools/github/shared-skills/
  README.md                        # how to consume
  issue-triage/
    SKILL.md                       # the instruction content
    metadata.yml                   # version, owners, applicability
    examples/                      # before/after examples
  issue-workflow/
    SKILL.md
    metadata.yml
  codeowner-routing/
    SKILL.md
    metadata.yml
```

Each skill is a self-contained directory with a single `SKILL.md` (the instructions an agent reads), a `metadata.yml` (version, semver, owners, intended consumer surfaces), and optional `examples/`. The directory layout is deliberately small — we want the cost of authoring a new skill to be low.

### Sync mechanism — options

How the shared skills get from this repo into consumer repos. We are **not picking a winner yet** — listing tradeoffs so the team can decide.

**Option A — Push-based GitHub Action (this repo opens PRs into consumers)**

A workflow in `azure-sdk-tools` watches `tools/github/shared-skills/`. On change, it opens a PR in each registered consumer repo that updates `.github/shared-skills/` to the new content.

- ✅ Consumers get a reviewable PR — they see exactly what changed and can block.
- ✅ Skills are local files in the consumer repo, so agents and humans can read them without network calls.
- ✅ Plays well with branch protection and CODEOWNERS.
- ❌ Requires a registry of consumer repos and a token with write access to each.
- ❌ Drift is possible if a team merges, then edits the synced files directly.

**Option B — Pull-based (consumer repos fetch on a schedule or on-demand)**

Each consumer repo has a workflow that pulls the latest skill content from this repo (e.g., via `actions/checkout` with `repository:` or a raw HTTP fetch) at workflow run time.

- ✅ No central registry — consumers opt in by adding a workflow.
- ✅ Always reads latest; no stale local copies.
- ❌ No PR-level review of skill changes — a change here ships to everyone on next run.
- ❌ Network failure = no triage. Need caching.
- ❌ Harder to pin to a known-good version.

**Option C — Direct reference (skills fetched at agent runtime by URL or pinned tag)**

The consumer's workflow doesn't sync files at all — it points the agent at a raw URL (e.g., `https://raw.githubusercontent.com/Azure/azure-sdk-tools/v1.2.0/tools/github/shared-skills/issue-triage/SKILL.md`) pinned to a tag.

- ✅ Versioning is explicit — consumer pins to `v1.2.0` and upgrades intentionally.
- ✅ Zero file drift in consumer repos.
- ❌ Skills aren't visible in the consumer repo without inspecting workflow files.
- ❌ Same network-failure concern as Option B.

**Likely answer:** A hybrid. Option A (push PRs) for the canonical "installed" copy, with Option C (pinned tag references) as the fallback / power-user mode. To be confirmed in prototyping.

### Consumer repo shape

A consumer repo opts in by adding three things:

1. **`.github/shared-skills/`** — synced copies of the skills they consume (managed by the sync mechanism, not edited by hand).
2. **`.github/shared-skills/overrides/`** — repo-owned override files (see Customization below). Edited freely by the repo team.
3. **A workflow / agent runner** — e.g., `.github/workflows/triage.yml` or a Copilot coding agent configuration that names which skills to apply on which events.

Example minimal consumer workflow (sketch):

```yaml
# .github/workflows/triage.yml in azure-sdk-for-python
on:
  issues:
    types: [opened, reopened]
jobs:
  triage:
    uses: Azure/azure-sdk-tools/.github/workflows/run-skills.yml@v1
    with:
      skills: issue-triage,codeowner-routing
      overrides-path: .github/shared-skills/overrides/
```

The reusable workflow is provided by *this* repo so consumers don't have to wire up the agent runner themselves.

### Customization layer

Two mechanisms, in order of preference:

1. **Override files.** Next to each synced skill, a consumer can drop `overrides/issue-triage.md`. The agent reads the shared skill *and then* the override, with the override taking precedence on any conflicting instructions. Override files are short ("In this repo, also apply the `client` label when X"), not full rewrites.
2. **Workflow inputs.** For simple toggles (e.g., "skip codeowner pinging," "label set: minimal"), expose them as `with:` inputs on the reusable workflow. These are repo-level config, not per-issue.

If a team needs more than overrides + inputs can express, that's a signal the shared skill is too prescriptive and should be relaxed upstream.

---

## Skill categories (initial)

Start small. Three skills, each doing one thing well. Add more once we've proved the model.

1. **Issue triage** — On a new (or reopened) issue: identify the affected service / package, apply the right service label, set severity/priority based on heuristics, decide if it needs human triage or can be auto-routed, post a triage comment summarizing the decision.
2. **Issue workflow / lifecycle** — Move issues through their lifecycle: detect stale issues, request more info, close-with-reason when criteria met, escalate when SLAs are missed, hand off between states (e.g., `needs-team-attention` → `in-progress` → `awaiting-customer`). Defines the canonical state machine; consumer overrides can add states.
3. **Codeowner routing** — Given a labeled issue, identify the right codeowner / team / individual based on `CODEOWNERS`, label-to-owner mappings, and on-call rotations. Post the @-mention. This is split from triage so it can be reused by other workflows (PR routing later).

Future candidates (not in v1): PR review triage, release-note generation, customer-reported-issue intake, security issue handling.

---

## Monitoring & tests

This is the part most likely to get skipped. We should not skip it.

### What "minimum required actions" looks like

For each skill, we define a small set of **observable contract assertions** — things that should be true about a real issue after the workflow runs. Examples for issue-triage:

- Every new issue in a consumer repo has at least one **service label** within **4 hours** of opening.
- Every new issue has a **triage comment** from the agent within **4 hours**.
- Every issue labeled `needs-team-attention` has a **codeowner @-mention** within **8 hours**.
- No issue sits in the `untriaged` state for more than **3 business days** without escalation.

Thresholds are illustrative — the real numbers come from each team's existing SLAs. The point is they are *measurable from the GitHub API* without trusting the workflow's self-report.

### Monitoring approach

A scheduled job in `azure-sdk-tools` (daily or hourly) that:

1. Reads the registry of consumer repos and which skills each has opted into.
2. Queries the GitHub API for issues opened in the relevant window.
3. Checks each contract assertion for that skill.
4. Emits results to a dashboard (Kusto / Azure Monitor / GitHub Pages — TBD) and opens an issue in the consumer repo if a threshold is breached.

This is read-only and runs centrally; no consumer setup needed beyond opting in.

### Compliance tests (contract tests)

Distinct from monitoring (which watches production), a **contract test suite** verifies a consumer's workflow on synthetic inputs *before* it ships:

- Provided as a **reusable workflow** consumers call from a PR check: `Azure/azure-sdk-tools/.github/workflows/skill-contract-tests.yml@v1`.
- Spins up a sandbox / mock-issues fixture and runs the consumer's triage workflow against it.
- Asserts the same contract assertions as monitoring, but deterministically.
- Required check on PRs that modify the consumer's `.github/shared-skills/` or workflow files.

This gives consumers a fast feedback loop ("did my override break triage?") without waiting for real issues.

---

## Open questions

A non-exhaustive list of things we'll need to answer during prototyping:

- **Sync mechanism:** Push PRs (Option A), pull at runtime (Option B), pinned URL refs (Option C), or a hybrid? What's the upgrade story when a skill goes from v1 → v2 with breaking changes?
- **Skill format spec:** Pure markdown, or markdown + frontmatter, or markdown + a sidecar `metadata.yml`? How do we declare "this skill expects these inputs / produces these labels"?
- **Ownership of shared skills:** Does DevExp own all shared skills, or do skill-owning sub-teams exist (e.g., security team owns the security-issue skill)? How do we handle review and SLAs on skill PRs?
- **Customization versioning:** When we ship a new shared skill version, do consumer overrides get re-validated? How do we surface "your override conflicts with the new skill"?
- **Agent runtime portability:** What's the minimum API surface a runtime must support to run our skills? (Read files, call GitHub API, post comments, apply labels.) Do we publish a small conformance test for runtimes?
- **Consumer registry:** Where does the list of opted-in repos live? In this repo as a YAML file? Inferred from a topic / label on the consumer repo? A dedicated config repo?
- **Monitoring privacy:** Some repos are private. Does the central monitor have access? Or do private repos run a self-hosted variant of the monitor?
- **Failure mode when an agent runtime is unavailable:** If Copilot is down or a consumer's runner is broken, do we fail open (issue stays untriaged) or fail closed (page someone)? Probably per-repo config, but worth deciding the default.

---

## Next steps

A small, concrete sequence to prove the model end-to-end before generalizing:

1. **Land this plan as a draft PR.** Get team feedback. Iterate the shape *before* writing skills.
2. **Author one skill (`issue-triage`) end-to-end** in `tools/github/shared-skills/` with a `SKILL.md`, `metadata.yml`, and at least two before/after examples. Treat this as the reference implementation that defines the format.
3. **Pick one consumer repo as the pilot** (e.g., `azure-sdk-for-python` or whichever team is most willing). Wire up the sync mechanism (start with Option A — push PRs — because it's the most reviewable), the reusable workflow, and a single override file. Run it on real new issues for two weeks.
4. **Stand up the monitoring job** in this repo against the pilot consumer. Validate the contract assertions catch real failures (e.g., manually break the workflow and confirm we detect it). Publish results somewhere the team can see them.

If steps 2-4 work, generalize to a second skill (`codeowner-routing`) and a second consumer repo. If they don't work, the plan was wrong — revise this doc, not the skills.

---

## Appendix: things deliberately left out of v1

Captured here so reviewers know they were considered, not forgotten:

- A full skill DSL or schema language. Markdown + tiny metadata is enough until it isn't.
- Cross-repo issue linking / dedupe.
- Auto-generated changelog / release-note skills.
- A web UI for browsing / searching skills. The repo's directory listing is fine for v1.
- Telemetry on agent reasoning (why did the agent pick this label?). Useful eventually, not blocking now.
- Multi-language skill content (skills are English-only in v1).
