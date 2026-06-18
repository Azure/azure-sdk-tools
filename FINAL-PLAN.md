# CCR Improvement Loop — Final Plan

A weekly, mostly-agentic system that answers one question — **is Copilot Code Review
actually helping?** — and then uses the answer to make Copilot Code Review better, by
proposing scoped, cited edits to the repo's Copilot instruction files.

This document is self-contained. You do not need to read anything else to understand
or implement it.

---

## 1. Background (read this first)

**Copilot Code Review (CCR)** is GitHub Copilot acting as an automated reviewer on
pull requests: when a PR is opened, CCR posts review comments (inline on specific
lines, and/or a summary) the same way a human reviewer would. Its behavior is shaped
by **Copilot instruction files** checked into the repo:

- `.github/copilot-instructions.md` — always-on, repo-wide guidance.
- `.github/instructions/*.instructions.md` — scoped to file globs via an `applyTo:`
  front-matter key (e.g. `applyTo: '**/*.go'`).
- `AGENTS.md`, `.github/prompts/*.prompt.md`, `.github/agents/*.agent.md` — other
  customization surfaces.

The premise of this project: **the feedback humans leave on PRs is a free training
signal.** If reviewers keep asking for the same thing that CCR didn't catch, that
recurring ask should become an instruction rule so CCR catches it next time. And if a
bug ships and is later fixed, the PR that introduced it is a labeled example of
something CCR *should* have flagged.

The hard part is not editing instruction files — it's knowing whether any of this is
working. This plan is built around **measuring CCR's effectiveness first**, then
feeding those measurements into instruction edits, in a loop that runs weekly and
proves its own value over time.

**Two earlier prototypes informed this plan** (you do not need them to proceed):

- A **TypeScript skill** that mines reviewer comments, clusters them into themes, and
  proposes scoped, cited edits to `.github/` files — strong at *applying* changes, but
  it has no persistence and no metrics.
- A **Python analyzer** that uses an LLM judge to find issues humans caught but CCR
  missed and tracks miss-rate over time — strong at *measuring*, but it can't apply or
  validate changes, and it carries a second language runtime + a SQLite layer.

This plan keeps the good ideas from both and deliberately simplifies.

---

## 2. The central question: does CCR help?

Everything below serves three concrete questions. Each has measurable proxies, and
every proxy is **normalized by PR type and comment type** (§6) so we never compare a
docs PR against a concurrency rewrite, or a "nit: rename this" against "this is a
data race."

1. **Are humans doing less work thanks to CCR?**
   - Human comments per PR, over time.
   - Time to complete a PR (open → merge).
   - Number of commits / review iterations per PR.

2. **How many of Copilot's comments are actually useful?**
   - Share of CCR comments that are **acted on** (a later commit touches the file/lines
     the comment named).
   - Share **resolved** — at merge time, did the author actually address it (thread
     resolved, positive human reply, or an LLM end-of-PR check confirming the concern
     was handled)?
   - Share **ignored or rejected** (dismissed, 👎, explicit "won't fix").
   - Sentiment of human replies to CCR (deferred until we have better data).

3. **Is CCR helping us catch critical issues?**
   - Number of bug-fix PRs merged over time — a proxy for *bugs that slipped through*.
   - Of those bugs, how many were introduced on a PR where CCR was active but silent
     on the exact lines (§7, "verified misses").
   - Share of **critical** CCR comments (not nitpicks) that get acted on.

The design principle behind all three: **a metric is only trustworthy once it is
normalized and de-noised.** Sections 5 and 6 are therefore as important as the metric
formulas themselves.

---

## 3. Design principles

1. **Less code, more agent.** Write deterministic TypeScript only where determinism
   pays off (fetching, filtering, attribution, git blame, arithmetic, JSON assembly).
   Everything needing judgment (is this substantive? what theme? what rule?) is the
   agent's job, governed by pinned prompts.
2. **TypeScript, not Python.** Node ≥ 24 runs `.ts` files directly with no build step,
   the GitHub CLI (`gh`) is already required, and TS/Node is more commonly installed
   on contributor and CI machines than a Python toolchain. One language, one runtime.
3. **A skill orchestrates; it is not a CLI application.** The agent (Copilot
   CLI / coding agent) drives the loop via a `SKILL.md`, calling small TS scripts for
   mechanical steps and reasoning over the rest. No bespoke pipeline framework, no ORM.
4. **JSON run-artifacts are the system of record — not a live database.** Each run
   emits one append-only `run-*.json`. A queryable database is *built on demand* from
   the pile of JSONs only when someone wants trends. Nothing to migrate, lock, or keep
   online; the trend view can never drift from its source.
5. **Prefer evidence over opinion.** A human comment is an opinion that something is
   wrong. A later bug-fix touching the same lines is *evidence*. The bug back-trace
   (§7) is the highest-signal input to the loop.
6. **Automation proposes; humans approve.** The weekly job opens an issue or a PR. It
   never auto-commits instruction changes to a default branch.

---

## 4. Architecture

```
  weekly trigger (Actions schedule / workflow_dispatch / local run)
        │
        ▼
  ┌──────────────────────── SKILL (agent orchestration) ─────────────────────────┐
  │                                                                               │
  │  DETERMINISTIC TYPESCRIPT                  AGENT JUDGMENT (pinned prompts)     │
  │  ─────────────────────────                 ─────────────────────────────      │
  │  fetch-prs.ts ──────────► raw PR cache (reviews, inline, commits, timeline)   │
  │  classify-pr.ts ────────► PR type: bug-fix / feature / refactor / docs / …    │
  │  filter-comments.ts ────► drop noise; tag ask / reply / summary               │
  │  attribute-comments.ts ─► author_kind, (file,line range), CCR overlap,        │
  │        │                  acted_on, resolved, dismissed                       │
  │        │                            judge: substantive? diff-detectable?      │
  │        │                                   category, severity, confidence     │
  │        │                            cluster: gaps → controlled-vocab themes   │
  │  trace-bug-origin.ts ───► bug-fix PR → git blame → introducing PR →           │
  │        │                  was CCR active & silent there? (VERIFIED MISS)      │
  │        │                            prioritize: ask×miss×reviewers (+verified) │
  │        │                            propose: scoped, cited, non-redundant     │
  │  compute-metrics.ts ────► all §6 metrics, normalized by PR & comment type     │
  │  emit-run-json.ts ──────► run-<timestamp>.json ◄── proposed edits ◄───────────┘
  │                                                    [human approves diff]       │
  └──────────────────────────────────────────────────────────────────────────────┘
        │ commit run-*.json to data branch / artifact      open issue OR PR
        ▼
  aggregate-runs.ts (on demand, NOT per run)
        └─► load all run-*.json into ephemeral DuckDB/SQLite → trends, dashboard
```

The two agent columns are the only places an LLM is invoked. Everything else is pure,
unit-tested TypeScript.

---

## 5. Filtering and normalization (the part that makes metrics trustworthy)

Raw counts lie. Before any metric is computed, two things happen.

### 5a. Filter low-signal noise (deterministic, `filter-comments.ts`)

Drop comments that carry no review signal so they never pollute counts or clustering:

- **Bots and automation** — `*[bot]`, known release/automation accounts, and bodies
  containing automation marker HTML comments.
- **Non-contributors** — keep only comments from `OWNER` / `MEMBER` / `COLLABORATOR`
  (people formally joined to the repo); drop drive-by external feedback.
- **Boilerplate / chatter** — `LGTM`, `+1`, `thanks`, emoji-only, quoted-only replies,
  and anything below a minimum length.
- **Replies vs. asks** — tag each surviving comment as `ask` (a reviewer requesting a
  change), `reply` (an author acknowledgement like "fixed in abc123"), or `summary` (a
  review overview). Only `ask` comments count toward "humans asked for X."

### 5b. Normalize by PR type and comment type

Comparing a one-line docs fix to a concurrency rewrite is meaningless, so every metric
is sliced by:

- **PR type** (`classify-pr.ts`): `bug-fix`, `feature`, `refactor`, `docs`, `test`,
  `chore`. Derived deterministically where possible (labels, Conventional-Commit title
  prefix `fix:`/`feat:`/`docs:`, linked issue labels) and by a one-shot agent
  classification only when those signals are absent. Stored on each PR row.
- **Comment type / severity** (from the judge, §6): `critical` (bug, security, data
  loss, concurrency), `substantive` (design, perf, missing test), or `nit`
  (style, naming, docs). Severity lets us weight "useful" by importance — a missed
  critical issue matters far more than a missed nitpick.

Metrics are reported **per (PR-type, comment-type) cell** and as an overall roll-up,
so a trend ("human comments per PR is falling") can't be an artifact of the week's PR
mix shifting toward docs changes.

---

## 6. Metrics, organized by the three questions

`compute-metrics.ts` emits the block below per run; `aggregate-runs.ts` turns the pile
of blocks into time series. Recall numbers are **relative to what humans caught**,
never presented as absolute — except the verified-miss metrics (§7), which are
relative to *confirmed* bugs and are the closest thing to ground truth we have.

### Q1 — Are humans doing less work?

| Metric | Formula | Direction | Notes |
| --- | --- | --- | --- |
| `human_comments_per_pr` | substantive human `ask` comments ÷ PR count | ↓ | Sliced by PR type; the headline "toil" number |
| `pr_cycle_time` | median(merged_at − created_at) | ↓ | Confounded by PR size/type — always slice |
| `iterations_per_pr` | median(commits after first review) | ↓ | Fewer back-and-forth rounds = less rework |

### Q2 — Are Copilot's comments useful?

| Metric | Formula | Direction | Notes |
| --- | --- | --- | --- |
| `acted_on_rate` | CCR comments where a later commit touched the named file/lines ÷ CCR comments | ↑ | Deterministic from the commit timeline |
| `resolved_rate` | CCR comments addressed by merge (thread resolved, positive reply, or merge-time LLM check) ÷ CCR comments | ↑ | "Addressed at merge" needs one LLM pass |
| `ignored_rate` / `dismissed_rate` | ignored or 👎/"won't fix" CCR comments ÷ CCR comments | ↓ | High = noise / overfitting |
| `useful_rate_by_severity` | acted_on ÷ total, split into `critical` / `substantive` / `nit` | ↑ | A useful *critical* comment ≫ a useful nit |
| `sentiment` | classifier over human replies to CCR | ↑ | **Deferred** until reply data is richer |

### Q3 — Are we catching critical issues?

| Metric | Formula | Direction | Notes |
| --- | --- | --- | --- |
| `bug_fix_pr_rate` | bug-fix PRs ÷ all PRs, over time | ↓ | Proxy for bugs slipping through to merge |
| `verified_miss_rate` | verified misses ÷ bug-fix PRs traced | ↓ | **Ground-truth recall** (§7) |
| `preventable_bug_rate` | verified misses where CCR was active ÷ introducing PRs with CCR active | ↓ | Bugs CCR had a real chance to stop |
| `critical_catch_rate` | critical CCR comments acted on ÷ critical issues known (CCR + human + verified) | ↑ | Are the *important* catches landing? |

### Cross-cutting

| Metric | Formula | Direction |
| --- | --- | --- |
| `miss_rate` | gaps (substantive ∧ diff-detectable human asks with no overlapping CCR comment) ÷ substantive-detectable human asks | ↓ |
| `ccr_overlap_rate` | human asks overlapped by a CCR comment ÷ substantive human asks | ↑ |
| `ccr_coverage` | PRs that received a CCR review ÷ all PRs | ↑ |
| `rule_yield` *(closed loop, §10)* | `miss_rate_before − miss_rate_after` for a replayed change | ↑ |

**How "useful" is measured deterministically.** `acted_on` = any commit to the same
path after the comment's timestamp (coarse but cheap; flagged as a soft signal).
Thread `isResolved` and reaction counts come straight from the data `fetch-prs.ts`
already pulls. Only `resolved_rate`'s "addressed at merge" judgment and `sentiment`
need an LLM; everything else in Q1–Q3 is pure arithmetic.

---

## 7. Bug-introduction back-trace (verified misses)

The single highest-signal capability, and the answer to "if a merged PR is a bug fix,
can we look at the PR where the bug was introduced and see why CCR missed it there?"
Yes — and it converts opinion-based gaps into evidence-based ones.

**Pipeline (`trace-bug-origin.ts`, deterministic):**

1. **Identify bug-fix PRs** in the window (label `bug`/`regression`, Conventional-
   Commit `fix:` title, linked closing issue labeled a bug, else a one-shot agent
   classification). Reuses `classify-pr.ts`.
2. **Locate the buggy lines.** From the fix PR's diff, take the removed/changed lines
   (the code being replaced), per file.
3. **Blame to the introducing commit.** `git blame` those pre-fix line ranges (CI
   checkout with full history), or GitHub's GraphQL `blame(path:)` to skip a clone.
4. **Map commit → PR.** `GET /repos/{owner}/{repo}/commits/{sha}/pulls` resolves the
   PR that merged the introducing commit.
5. **Ask the decisive question.** On that introducing PR: was CCR active, and did it
   comment on or overlap the introduced lines? CCR **active and silent** on lines that
   later needed a bug fix = a **verified miss**.
6. **Theme & weight.** Tag each verified miss with the controlled vocabulary and feed
   it into clustering with higher weight than an ordinary gap — it is a *confirmed*
   bug, not a hunch.

**Why it matters:** a verified miss is corroborated by a real, merged fix, so it
sidesteps the usual caveat that "recall is only relative to what humans happened to
comment on." These power `verified_miss_rate` / `preventable_bug_rate` and are the
strongest justification for a new rule.

**Honest limits (recorded as `blame_confidence`):** blame is line-granular and can
mis-attribute across refactors/renames; a "fix" may be a mislabeled tweak; the
introducing PR may predate CCR being enabled. Record blame confidence and the CCR
enablement date, exclude pure-rename hunks, and trend rather than trust any single
trace.

---

## 8. From metrics to instruction edits (without overfitting)

When a theme is both frequently asked by reviewers and frequently missed by CCR, it is
a candidate rule. Turning it into an edit is governed by three guards so we improve CCR
generally rather than memorizing last week's PRs.

**Prioritize.** Rank themes by:

```
priority = (ask_freq × distinct_reviewers) × (miss_freq + W_verified × verified_misses)
                                                              (W_verified ≈ 3)
```

**Promote (hard gate).** A theme earns a rule only at **≥ 2 PRs from ≥ 2 distinct
reviewers**, or **one reviewer flagging it ≥ 3×**. One-off asks never become rules.

**Avoid overfitting and redundancy** — the explicit anti-overfitting guards:

- **Generalize to the repo, not the PR.** A rule must be phrased as a class of issue
  ("wrap returned errors with `%w`"), never tied to a specific file/PR. Reject any
  proposed rule that only makes sense for the originating diff.
- **No redundancy.** Before proposing, inventory the existing `.github/` files; if a
  rule already covers the theme, *strengthen wording* or skip — never add a duplicate.
- **Scope correctly.** Language/path-specific rules go in a scoped
  `*.instructions.md` with `applyTo`; only truly repo-wide rules go in
  `copilot-instructions.md`.
- **Cite sources.** Every rule carries a trailing citation line listing the source PRs
  so a maintainer can later audit *why* it exists.
- **Show the diff first.** Present one consolidated diff for human approval before
  writing; never auto-commit.

---

## 9. Run JSON — the system of record

One file per run, append-only, committed to a `data/` branch (or uploaded as an
artifact). Self-describing, so it can be aggregated years later without the code that
produced it.

```jsonc
{
  "schema_version": "1.0",
  "run": {
    "id": "2026-06-18T00-00Z_Azure_azure-sdk-for-go",
    "repo": "Azure/azure-sdk-for-go",
    "window_start": "2026-06-11", "window_end": "2026-06-18",
    "pr_state": "merged", "pr_count": 42,
    "model": "github-models/gpt-4o",
    "prompt_hashes": { "judge": "sha256:…", "theme": "sha256:…" },
    "config_hash": "sha256:…", "ccr_enabled_since": "2026-01-15",
    "generated_at": "2026-06-18T00:14:03Z"
  },
  "prs": [
    { "number": 7012, "url": "…", "title": "…", "author": "…",
      "merged_at": "…", "additions": 120, "deletions": 8,
      "pr_type": "bug-fix", "ccr_reviewed": true,
      "cycle_time_hours": 31.5, "iterations": 3 }
  ],
  "comments": [
    { "pr": 7012, "external_id": 99887766, "url": "…",
      "author_kind": "human", "kind": "ask", "source": "inline",
      "path": "sdk/foo/client.go", "line_start": 41, "line_end": 41,
      "is_substantive": true, "diff_detectable": true,
      "severity": "critical", "category": "error-handling", "confidence": 0.82,
      "acted_on": true, "resolved": true, "dismissed": false,
      "ccr_overlap": false, "is_gap": true, "theme": "error-handling" }
  ],
  "verified_misses": [
    { "fix_pr": 7300, "fix_url": "…", "path": "sdk/foo/client.go",
      "introduced_by_pr": 7012, "introduced_url": "…",
      "introducing_commit": "abc123",
      "ccr_active_on_introducing_pr": true, "ccr_commented_on_lines": false,
      "theme": "error-handling", "blame_confidence": "high" }
  ],
  "themes": [
    { "label": "error-handling", "gap_count": 9, "verified_miss_count": 3,
      "ask_count": 11, "distinct_reviewers": 4, "promoted": true,
      "priority_score": 132.0, "source_prs": [7012, 7034, 7045] }
  ],
  "metrics": { /* the §6 block, with per-(pr_type,severity) slices */ },
  "proposed_edits": [
    { "file": ".github/instructions/go.instructions.md", "applyTo": "**/*.go",
      "theme": "error-handling", "rule": "Wrap returned errors with %w …",
      "redundant_with": null, "source_prs": [7012, 7034, 7045], "status": "proposed" }
  ],
  "experiment": null
}
```

**Why JSON-as-record beats a live DB here:** the producing job stays stateless and
idempotent (re-emit and overwrite by run id); there is nothing to lock, migrate, or
keep online; a bad run is one file to delete; and the aggregate view is always rebuilt
from source-of-truth files, so it can never silently drift.

---

## 10. Aggregation and (optional) closed-loop validation

**Aggregation (`aggregate-runs.ts`, on demand only).** Loads every `run-*.json` into
an ephemeral DuckDB or stdlib SQLite and answers trend queries — miss_rate over time,
useful_rate by severity, verified_miss_rate by repo, etc. It never runs in the weekly
path, so the producing job has no database to manage.

**Closed-loop validation (`--validate`, opt-in, rate-limited).** To *prove* a proposed
edit helps rather than assume it:

1. Replay a historical PR's review rounds against **baseline vs. candidate** `.github/`
   and wait for live CCR (reuses a replay harness from the earlier skill prototype).
2. Re-run attribution + judge over the replay output → `miss_rate_after`.
3. Record `{ source_themes, files_touched, source_prs, replay_pr_set,
   miss_rate_before, miss_rate_after, benchmark_passed }` in the run JSON's
   `experiment` field.
4. **Benchmark guard (anti-Goodhart):** keep a frozen, labeled held-out PR set as a
   committed `benchmark/*.json`; gate every applied delta on it. A regressing delta is
   blocked. This is the primary defense against prompt overfitting.

---

## 11. GitHub agentic workflow and output modes

A weekly `schedule` + `workflow_dispatch` workflow runs the agent against the skill.
Two output modes, chosen by how it's run:

- **Issue mode (default in CI):** the agent posts/updates a single idempotent issue
  with the metrics summary (sliced by PR/comment type), the ranked theme table,
  verified-miss highlights, and the *proposed* diffs for human approval. No file
  writes — safest for an unattended scheduled run.
- **PR mode (local or `--apply`):** with write permission, the agent edits the
  `.github/` files directly and opens a PR with the consolidated, cited diff, linking
  the run JSON. Reviewers approve via normal PR review.

In **both** modes the run JSON is committed to the `data/` branch (or uploaded as an
artifact). Safety rails: confirm the target repo isn't a fork/mirror before fetching,
write only to the local `.github/`, and never auto-commit to a default branch.

---

## 12. Sequenced implementation plan

Each step ends with a concrete, checkable artifact.

1. **Ingest + normalize.** Wire `fetch-prs.ts` → `filter-comments.ts`; add
   `classify-pr.ts` (PR type) and `attribute-comments.ts` (author kind, line ranges,
   CCR overlap, acted_on/resolved/dismissed). *Done when* a cached window yields
   normalized, typed comment rows.
2. **Metrics + run JSON.** Add `compute-metrics.ts` (all §6 metrics, per-type slices)
   and `emit-run-json.ts` with a zod-validated schema. *Done when* one window produces
   a schema-valid `run-*.json` with metric slices.
3. **Judge + cluster.** Pin the judge/theme prompts in `references/`; the agent fills
   `is_substantive`, `diff_detectable`, `severity`, `category`, and `theme`. *Done
   when* gaps and themes appear with citations and prompt hashes.
4. **Bug back-trace.** Build `trace-bug-origin.ts`; populate `verified_misses`,
   `verified_miss_rate`, `preventable_bug_rate`. *Done when* a known bug-fix PR
   resolves to its introducing PR with a correct CCR-silent verdict.
5. **Prioritize + propose (anti-overfit).** Implement the §8 score, promotion gate,
   redundancy check, and scoped cited proposal. *Done when* an approved theme writes a
   generalized, non-duplicate rule to the correct `.github/` file behind one diff.
6. **Aggregate.** `aggregate-runs.ts` loads all run JSONs into ephemeral DuckDB/SQLite
   and prints trends. *Done when* miss_rate and verified_miss_rate trend across ≥ 2
   runs.
7. **Agentic workflow.** Weekly schedule, issue mode by default, run JSON committed to
   `data/`. *Done when* an unattended run opens an issue and lands an artifact.
8. **(Optional) Closed loop.** Wire `--validate` replay + benchmark guard into the
   `experiment` field. *Done when* a regressing delta is blocked and Δmiss_rate is
   recorded.

---

## 13. Risks and mitigations

- **Metrics confounded by PR mix** — always slice by PR type and severity (§5b); report
  cells plus a roll-up, never a bare overall number.
- **Recall still partly relative to humans** — mitigated, not eliminated, by the
  verified-miss track (§7); present human-relative and bug-verified metrics separately.
- **Blame mis-attribution across refactors** — record `blame_confidence`, exclude
  pure-rename hunks, trend rather than trust single traces.
- **CCR not enabled on older introducing PRs** — store `ccr_enabled_since`; exclude
  pre-enablement PRs from `preventable_bug_rate` denominators.
- **`acted_on` is coarse** — a commit to the same path isn't proof the comment was
  addressed; document it as soft and corroborate with `resolved_rate`.
- **Prompt overfitting / Goodhart** — generalization + redundancy guards (§8) plus the
  benchmark guard (§10); automation only proposes, humans approve.
- **Agent non-determinism** — pin prompts, `temperature=0`, record prompt/config
  hashes; offer a deterministic `gh models` batch path when reproducibility matters.
- **Replay cost / rate limits** — keep validation opt-in and scoped to the prioritized
  + benchmark PR sets only.
- **JSON sprawl** — partition the `data/` branch by repo and month; aggregation globs
  lazily; old runs are superseded by run id, never rewritten.

---

## 14. Open questions

- Default judging path: inline agent reasoning (simplest) vs. a `gh models` batch call
  (more reproducible at scale) — ship inline first, add the batch toggle if drift
  appears?
- Aggregation engine: DuckDB (great for ad-hoc analytics over JSON) vs. stdlib SQLite
  (zero install) — DuckDB for the dashboard with a SQLite fallback?
- Where run JSONs live long-term: a `data/` orphan branch in-repo, a dedicated storage
  repo, or Actions artifacts with retention?
- `resolved_rate`'s "addressed at merge" check is one LLM pass per PR — worth the cost,
  or rely on the deterministic `acted_on` + thread-resolved signals alone at first?
- Sentiment analysis is deferred — what's the minimum reply volume before it's worth
  building?
