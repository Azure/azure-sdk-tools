# Design: Self-Improving Skill Agent (AI Foundry)

## Overview

This document describes the architecture of an **AI Foundry agent** that continuously
improves the [`azure-typespec-author`](../SKILL.md) skill. The agent closes the loop
between how authors actually use the skill (telemetry), what authoritative sources say
(public websites), and the skill's own quality bar (the [benchmark](../evaluate/README.md)).

The agent is a scheduled, autonomous pipeline: it ingests signals, edits the skill,
measures the effect with the existing Vally benchmark, produces a report, and identifies
which documentation the skill is still missing. Its output is a proposed change to the
skill plus an evidence-backed gap analysis for human review.

## Goals

- **Copilot codes, Foundry learns — leverage the strengths of each.** GitHub Copilot
  handles interactive, in-editor authoring where fast human-in-the-loop coding shines,
  while the AI Foundry agent runs the autonomous, long-horizon learning loop (telemetry
  analysis, benchmarking, and documentation-gap discovery). The two are complementary:
  Copilot produces the changes, Foundry continuously learns from real usage and measures
  what actually improves the skill.
- **The skill self-evolves.** The [`azure-typespec-author`](../SKILL.md) skill improves
  itself over time — turning real usage telemetry and refreshed authoritative documentation
  into measured, evidence-backed edits — rather than depending solely on manual, one-off
  updates.

## Goals & Non-Goals

**Goals**

- Turn real usage telemetry and refreshed public documentation into concrete skill edits.
- Quantify every change against the existing forced / trigger / no-skill benchmark suites.
- Surface documentation gaps the skill cannot yet cover, with citations.

**Non-Goals**

- Auto-merging skill changes to `main`. The agent proposes; humans approve.
- Replacing the Vally evaluation harness — the agent *drives* it, it does not reimplement it.
- Authoring TypeSpec for end users. This agent improves the skill, not customer specs.

## Inputs

1. **User telemetry** — anonymized signals from real `azure-typespec-author` sessions:
   the user prompts plus session outcomes (whether the skill triggered, whether it asked
   clarifying questions, tool-call errors, retries, and final success/failure). Used to find
   where the skill underperforms and to seed new benchmark cases.
2. **Public websites** — authoritative TypeSpec / Azure API guidance (the curated catalog in
   [reference-document-links.md](../references/reference-document-links.md) plus new pages
   discovered from telemetry). Fetched with the same
   [agentic search](../references/agentic-search.md) mechanism the skill itself uses, so the
   agent and the skill stay grounded in the same sources.

## Architecture

```
        ┌────────────────────┐        ┌─────────────────────┐
        │   User telemetry   │        │   Public websites   │
        │ (prompts, outcomes)│        │(TypeSpec/Azure docs)│
        └─────────┬──────────┘        └──────────┬──────────┘    benchmark test results 1.  
                  │                               │
                  ▼                               ▼
        ┌──────────────────────────────────────────────────┐
        │            Skill evolvement(orchestrator)         │
        │                                                   │
        │  Step 1  Update the reference/skill               │
        │  Step 2  Run the benchmark test                   │
        │  Step 3  Generate the test report                 │
        │  Step 4  Analyze current document gaps            │
        └───────────────────────┬──────────────────────────┘
                                 │
                                 ▼
             Proposed skill diff + benchmark report + gap analysis
                        (opened as a PR for human review)

realtime: user -> feedback agent ->  user telemetry 

user telemetry -> feedback agent 

benchmark runs -> 

1. flaky test 2. always failed test 


analyze agent

```

The orchestrator is a single AI Foundry agent that runs the four steps in order, carrying
state (telemetry findings, fetched docs, benchmark scores) between steps. Each step is a
tool-backed action rather than free-form reasoning.

## Agent Procedure

### Step 1 — Update the skill

**Input:** user telemetry + fetched public documentation.
**Action:**

1. Cluster telemetry into failure themes (e.g. "agent asks clarifying questions on ARM
   extension resources", "trigger mode misses versioning requests").
2. For each theme, run [agentic search](../references/agentic-search.md) over the matched
   catalog URLs from
   [reference-document-links.md](../references/reference-document-links.md) to gather
   authoritative guidance. Every edit must be grounded in fetched content, not internal
   knowledge.
3. Edit the skill surface — [`SKILL.md`](../SKILL.md) and the
   [`references/`](../references) procedures — to close the theme (clarify a procedure, add a
   missing case, tighten a trigger phrase).
4. If a theme is not covered by any existing benchmark case, add a stimulus under
   [`evaluate/evals/`](../evaluate/evals) so the improvement is measurable.

**Output:** a candidate skill diff on the working branch.

### Step 2 — Run the benchmark test

**Input:** the candidate skill diff.
**Action:** run the existing Vally benchmark across all three modes so the change is measured
against code quality, trigger detection, and the no-skill baseline:

```bash
vally eval --suite forced   --skill-dir .. --output-dir ./result --workspace ./debug --verbose
vally eval --suite trigger  --skill-dir .. --output-dir ./result --workspace ./debug --verbose
vally eval --suite no-skill --skill-dir /tmp/no-skills --output-dir ./result --workspace ./debug --verbose
```

The agent runs the candidate branch **and** the pre-change baseline so it can compute a delta,
not just an absolute score. Runs use the standard `azsdk-mcp` environment described in the
[evaluation README](../evaluate/README.md).

**Output:** raw Vally results (`results.jsonl`, `eval-results.md`) for baseline and candidate.

### Step 3 — Generate the test report

**Input:** baseline and candidate Vally results.
**Action:** aggregate results into a human-readable report:

- Per-suite and per-mode pass rates, with the **delta vs. baseline**.
- Regressions called out explicitly (any case that passed before and now fails).
- Per-case detail: turns, tool calls, and the specific grader that failed
  (e.g. a missing `azure-sdk-mcp-azsdk_run_typespec_validation` call).
- A verdict: **improved / neutral / regressed**, gating whether the diff is worth proposing.

**Output:** `report.md` attached to the eventual PR.

> Note run-to-run variance: single-run deltas below the benchmark's noise floor are not
> conclusive. Prefer multiple runs before declaring an improvement.

### Step 4 — Analyze current document gaps

**Input:** telemetry themes from Step 1 + benchmark failures from Step 3 + the curated
catalog in [reference-document-links.md](../references/reference-document-links.md).
**Action:** identify what the skill still cannot answer:

1. **Uncovered requests** — telemetry prompts that matched no catalog case and fell back to
   the KB path in [authoring-plan.md](../references/authoring-plan.md).
2. **Stale or thin docs** — cases where agentic search fetched a page but it did not contain
   the guidance the failing benchmark case needed.
3. **Missing catalog entries** — authoritative public pages seen in telemetry that are not yet
   listed in [reference-document-links.md](../references/reference-document-links.md).

For each gap, record: the triggering prompt, the failing benchmark case (if any), the
source URL that *should* exist or be added, and a recommended action (add catalog link,
extend a reference procedure, or file a docs request upstream).

**Output:** `gap-analysis.md` — a prioritized, citation-backed list of documentation gaps.

## Output & Review

The agent emits three artifacts as a single pull request:

- The **skill diff** (`SKILL.md`, `references/`, new `evaluate/evals/` cases).
- The **benchmark report** (`report.md`) with baseline deltas.
- The **gap analysis** (`gap-analysis.md`) with citations.

A human reviewer approves or rejects. The agent never merges autonomously; the benchmark
verdict and gap analysis exist precisely to make that human decision fast and evidence-based.

## Feedback Loop

Each run's gap analysis feeds the next run: newly identified catalog links and benchmark
cases become inputs to Step 1, so the skill's coverage and its measurable quality bar grow
together over time. Telemetry closes the loop by revealing whether shipped changes actually
reduced failures in production sessions.
