# CCR Improvement Loop — metrics, and why they're defensible

This workflow support package measures whether **Copilot Code Review (CCR)** is
actually helping on a repo, then feeds the answer back into the repo's Copilot
instruction files. This document defends the metric set: for each number we
report, it states **what it measures, why the underlying signal is valid, its
known limits, and how to read it honestly.**

The guiding principle is **subtractive**: we would rather report three numbers we
can defend than a dozen that look rigorous but rest on weak proxies. An earlier
iteration measured CCR with structural shortcuts — line-range overlap, "a later
commit touched the file", regex sentiment on replies. Each was individually
noisy, and no amount of schema rigor downstream fixes a bad proxy. Those are gone
(see [Rejected metrics](decisions.md#d1--subtractive-metric-set-defend-a-few-not-display-many)
in `decisions.md`). What remains either comes
from a **deterministic fact** or an **explicit LLM judgment made from the actual
evidence**, never from co-location or keyword guessing.

For the engineering decisions behind this design — the phase split, the
`ccrSawCode` gate, the honesty invariants, and the dashboard — see
[`decisions.md`](decisions.md).

## Choosing the target repo

The workflow measures **one target repo per run**. The source lives in
[`.github/workflows/ccr-improvement-loop.md`](../../workflows/ccr-improvement-loop.md)
(gh-aw source); GitHub actually runs the compiled
`ccr-improvement-loop.lock.yml`, so any config change must be followed by a
recompile (`gh aw compile`) and a commit of **both** files.

The target is resolved by
`TARGET_REPO: ${{ github.event.inputs.repo || github.repository }}` and flows
into every prep script as `--repo <owner/name>`. The emitted filename embeds the
target (`run-<windowEnd>_<owner>_<repo>.json`) and the concurrency group is
per-repo, so multiple repos coexist cleanly — both in Actions and in the
dashboard.

- **One-off run against any repo (no config change):** dispatch with the `repo`
  input.
  ```bash
  gh workflow run ccr-improvement-loop --repo <workflow-repo> -f repo=Azure/azure-sdk-for-python
  ```
  (Or use the Actions UI "Run workflow" button and set the `repo` input.)
- **Change the standing/scheduled target:** the weekly `schedule` cannot pass an
  input, so it falls back to `github.repository` (the repo the workflow lives
  in). To point the schedule at a real repo, edit the fallback in the `.md`:
  ```yaml
  env:
    TARGET_REPO: ${{ github.event.inputs.repo || 'Azure/azure-sdk-tools' }}
  ```
  then recompile and commit.
- **Several repos on a schedule:** add a build matrix over a repo list. Only do
  this if you need multiple standing targets; the per-repo concurrency group and
  filename scheme already make concurrent repos safe.

Notes:

- The default `GITHUB_TOKEN` can read **public** target repos cross-repo; a
  **private** target needs a PAT / GitHub App with `pull-requests: read` and
  `issues: read` on that repo.
- A run refuses **fork/mirror** targets (see
  [`references/upstream-fork-check.md`](references/upstream-fork-check.md)).

## Backfilling historical windows

To build a historical trend (e.g. the whole year for one repo), dispatch the
workflow once per window with explicit `window_start` / `window_end`
(`YYYY-MM-DD`). Each dispatch fetches that window's PRs, runs the agent judgment,
and emits `run-<window_end>_<owner>_<repo>.json` — distinct windows produce
distinct files, so the dashboard plots them as separate points.

```bash
# One month of Azure/azure-sdk-for-python
gh workflow run ccr-improvement-loop --repo <workflow-repo> \
  -f repo=Azure/azure-sdk-for-python \
  -f window_start=2026-01-01 -f window_end=2026-01-31
```

Repeat per month (Feb, Mar, …). Guidance:

- **Monthly windows** give a busy repo ~180–300 PRs each — large, comparable
  denominators and clean calendar points. Bi-weekly doubles the points but halves
  per-point `n`, worsening the `n/a` problem for thin metrics.
- Backfill runs are **uncapped** (full cohort). GitHub search caps at 1000 hits
  per window, so keep windows ≤ ~1 month on the highest-volume repos.
- Trend points key on `windowEnd`, **not** the run date, so all backfilled runs
  (generated the same day) still spread across their real windows. After
  collecting the JSONs, drop them in `dashboard/data/`, add each filename to
  `data/manifest.json`, and refresh.

## Dashboard

A static, zero-backend web dashboard visualizes these metrics and their trends
across many `run-*.json` files. See [`dashboard/README.md`](dashboard/README.md)
for how to run it locally (`python3 -m http.server --directory dashboard`), feed it
data, and enable GitHub Pages later.

## Reading rules (apply to every metric)

These make the difference between a number that means something and one that
doesn't:

1. **Trend, don't snapshot.** A single window is a data point, not a verdict.
   Every metric is designed to be read across runs (`aggregate-runs`), because
   one week's PR mix is noise.
2. **n ≥ 5 or it's suppressed.** Any overall figure or slice with a denominator
   below 5 is reported as low-confidence and never headlined. Thin weeks produce
   warnings, not false precision.
3. **Normalized by PR type; sliced by severity where it applies.** A miss-rate
   move must survive the question "was this just a heavier bug-fix week?" — so
   every rate carries per-`prType` slices, and slices only appear once they clear
   n ≥ 5.
4. **Null, never zero, for an empty denominator.** "We couldn't measure it" and
   "it was zero" are different facts and are reported differently.
5. **The instrument must be stable to compare across time.** We record the judge
   model, the pinned prompt hashes, and the vocabulary hash on every run. A shift
   in any of them breaks comparability by design, so a trend line can't silently
   mix two different rulers.

## What we count as ground truth

Two things anchor everything:

- **A CCR comment** — attributed deterministically from the configured CCR bot
  logins. Not guessed.
- **A substantive human review ask** — a human reviewer requesting a real code
  change (correctness, security, design, maintainability, test gap), judged from
  the comment plus its diff hunk. Style nits, acknowledgements, and questions are
  excluded.

We deliberately do **not** treat "a human commented here" as "CCR should have
caught this." That inference is only made under two guards, described next.

---

## Q1 — Are humans doing less review work?

### `humanReviewBurden` — substantive human asks per PR, by PR type

**Measures:** the average number of _distinct, substantive_ human review asks per
PR, sliced by PR type.

**Why it's valid:** it counts only asks the judge marked substantive (dropping
LGTM/nit/question noise), and it de-duplicates by finding — the same concern
raised inline and restated in a review summary counts once — so it can't be
inflated by chatty threads. Normalizing by PR type prevents a docs-heavy week
from masquerading as a quiet-reviewers week.

**Limits:** reviewer culture and team size confound the absolute value; a repo
that reviews thoroughly will always look "busier". Read the **trend within one
repo**, never compare repos on the raw number.

### `prCycleTime` and `iterationsPerPr` — contextual only

**Measures:** median hours from open to merge, and median commits after the first
review event.

**Why we keep them:** they are cheap, deterministic, and occasionally corroborate
a burden change (if CCR is catching issues pre-human-review, iterations should
fall).

**Limits (stated loudly):** both are **noisy and multiply-confounded** — PR size,
CI flakiness, reviewer availability, release freezes. They are reported as
**contextual, low-confidence** signals and never used alone to claim CCR is
helping. If they disagree with `humanReviewBurden`, believe the burden metric.

---

## Q2 — Are CCR's comments useful?

This is where the previous design was weakest. "A later commit touched the file"
told us the file was still being edited, not that the comment was addressed. We
replaced it with a judgment made from the **actual change at the commented
lines**.

### `ccrOutcome` → `addressedRate` / `rejectedRate` / `ignoredRate`, by severity

**Measures:** for each CCR comment, an LLM assigns exactly one outcome from a
closed set:

- **`addressed`** — the author changed the code at those lines to satisfy the
  comment.
- **`rejected`** — the author explicitly declined it (with a reason / "by
  design").
- **`ignored`** — no change and no engagement.
- **`unclear`** — evidence insufficient to decide.

The three headline rates are the mutually-exclusive shares of the first three
buckets, sliced by comment severity.

**Why it's valid:** the judge is given the **line-level diff of what changed after
the comment** plus any author replies — the same evidence a human would use to
decide "did they fix it?" — instead of a path-level heuristic that is true on
essentially every active PR. `rejected` and `ignored` give the _negative_ signal
the old `actedOnRate` couldn't: a high `ignoredRate` on critical comments is a
real problem a single "useful rate" would hide. Slicing by severity is the point
— an 80% addressed rate is great for `critical` and irrelevant for `nit`.

**Limits:** it costs an LLM call per CCR comment and depends on judge calibration;
the `unclear` bucket is kept explicitly so the judge can abstain rather than guess
(and `unclear` is excluded from the rate denominators, reported separately). We do
not claim `addressed` means the fix was _correct_ — only that the author acted on
the comment.

---

## Q3 — Is CCR catching what humans catch?

This is the headline "is CCR missing things?" story, and it carries the two
guards that make it defensible.

### `missRate` — substantive human asks CCR didn't raise, gated twice

**Measures:** of the substantive, diff-detectable human asks that **CCR had a
real opportunity to catch**, the fraction where **CCR did not raise the same
concern**. Lower is better.

**Why it's valid — guard 1 (same concern, not same line):** whether CCR "already
caught it" is an **LLM judgment that the CCR comment addresses the same concern**
as the human ask — not line-range proximity. The old overlap check counted two
unrelated comments three lines apart as a catch, and missed CCR flagging the same
class of issue at a different location. Same-concern judgment removes that
co-location fallacy in both directions.

**Why it's valid — guard 2 (`ccrSawCode`):** a human ask only enters the
denominator if CCR actually reviewed the version of the code it anchors to. This
is computed deterministically: CCR must have posted a review on the PR, and the
latest commit touching the ask's file must be at or before CCR's most recent
review that precedes the human comment. If the human is commenting on code pushed
_after_ CCR's last look, CCR never had the chance — that ask is **excluded**, not
scored as a miss. This closes the "blamed for a round it never reviewed" hole.

**Limits:** the judge sees a **truncated diff hunk**, so `diffDetectable` — and
therefore `missRate` — covers issues detectable from **local context**. Bugs that
require whole-repo or runtime knowledge are deliberately out of scope and marked
not-detectable. So `missRate` answers _"of the locally-detectable issues CCR
could see, how many did it miss?"_ — a fair bar for an automated reviewer, but not
"how good is CCR at everything." We state that framing rather than hide it.

### `ccrCoverage` — PRs CCR reviewed, of those eligible

**Measures:** the share of eligible PRs (post-enablement, non-bot-authored) that
received any CCR review.

**Why it's valid:** it's a pure deterministic fact from review attribution, and it
frames every other Q3 number — a great miss-rate on 30% coverage is a different
story than on 95%. PRs merged before CCR was enabled (`ccrEnabledSince`) are
excluded so coverage can't be dragged down by history.

**Limits:** "reviewed" means CCR posted something, not that it reviewed deeply.
Pair it with `missRate`, never read it alone.

---

## Q4 — Bug-fix PR rate (merged-bug signal)

**Measures:** `bugFixPrRate` — the share of PRs in the window classified as
`bug-fix`. A rising bug-fix rate on stable coverage is a signal worth watching
next to `missRate`.

**Why it's a proxy, not proof:** it counts merged bugs but does not attribute them
to a review CCR could have caught. That's deliberate. An earlier design traced a
bug-fix's lines back to the introducing PR via `git blame` to build a
"verified miss," but that attribution is fragile across squashes, renames, and
force-pushes, a single weekly window yields maybe zero to two cleanly-traceable
fixes (so the rate was almost always n < 5), and in practice it produced no
signal. We removed it in favor of the simpler deterministic count and may revisit
causal tracing once it can be made robust.

---

## Sampling window — time-based, full cohort by default

Each run measures a **time window** of merged PRs that have settled for at least
`windowLagDays` (default 14):

- **Weekly schedule (default):** a rolling settled window — `end = today −
settleDays`, `start = end − windowDays` (both 14 → the fortnight ending two
  weeks ago). No count cap: the **full cohort** in the window is measured.
- **Backfill (manual):** pass explicit `window_start` / `window_end` to measure
  any historical window — see
  [Backfilling historical windows](#backfilling-historical-windows).

`prep-run.ts` resolves the window and `fetch-prs.ts` lists
`merged:>=<start> merged:<=<end>`. A count cap exists but is **off by default**
(`--max-prs 0` = uncapped); set `--max-prs N` only to bound cost on a window too
large to fetch, accepting a recency-biased sample (GitHub returns
most-recent-first).

**Why full cohorts, not a fixed count.** A complete time window is an unbiased
sample of that period, so window-to-window trends are comparable and the rate
denominators are as large as traffic allows — the best chance of clearing the
n ≥ 10 confidence bar instead of `n/a`. The settle lag ensures threads are
resolved and follow-up commits have landed before we read a PR. The trade-off:
throughput varies, so a light window may still fall below the n ≥ 5 / n ≥ 10 bars
and honestly surface `n/a`, and the run **no-ops below 50 settled PRs** rather
than reporting on a handful.

---

## The judge — done in the agentic workflow, not a bespoke API client

The classification judgments above (substantive? severity? outcome? same concern?)
run in the **agent's own turn loop**, governed by the pinned prompt in
[references/judge.prompt.md](references/judge.prompt.md) and the closed vocabulary.
There is deliberately **no `judge.ts` model client**. For a weekly diagnostic
trend, standing up an HTTP client (endpoint, auth, batching, retries, response
parsing) is maintenance surface that rots the moment the model or API changes —
and the agentic workflow already has model access, so judging in-loop costs no
client code at all.

What protects the numbers is **not** the call mechanism but the **closed
vocabulary enforced at emit**: `emit-run-json` validates every label through the
zod schema and rejects the run on an invalid `severity`/`category`/`outcome`, so a
hallucinated label can't silently skew a metric. Reproducibility is a _recorded
model_, not a guarantee — run-to-run label variance is acceptable noise for a trend
read at n ≥ 5, and it averages out across runs.

The one place we **do** pin the judge (fixed model + prompt + temperature 0) is the
optional **closed-loop replay validation**, where comparing a candidate rule
against a baseline needs a stable ruler. That is off the weekly path by design.
Agentic sessions are also the wrong tool for the _labeling_ specifically — but
they are exactly right for the _orchestration_ around the loop, which is the whole
point: **scripts produce reproducible facts; the agent supplies judgment.**

## Anti-Goodhart note

Every metric here can be gamed if it becomes a target — the fix for "CCR misses
things" must not be "tell humans to comment less." That is why the loop's output
is **instruction-file rule proposals for human approval**, gated on a frozen
benchmark, and why the metrics are framed as a **diagnostic trend**, not a KPI to
optimize. If you find yourself tuning the repo to move a number, re-read
[Reading rules](#reading-rules-apply-to-every-metric).
