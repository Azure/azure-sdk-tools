# Metrics

AVC collects and reports usage and quality metrics about Copilot-assisted API reviews. These metrics are used to track adoption, evaluate AI comment quality, and measure the impact of Copilot on the review process.

## What Is Measured

Metrics are computed per language and overall ("All"), across a user-specified date range.

### Adoption Metrics

Tracks how many API reviews used Copilot versus those that did not.

| Metric | Description |
|--------|-------------|
| `active_review_count` | Number of approved package versions in the date range |
| `active_copilot_review_count` | Approved package versions that had a Copilot review |
| `adoption_rate` | `active_copilot_review_count / active_review_count` |

> **Note:** Counts are based on **approved** package versions only (i.e., revisions that were approved via the APIView approval mechanism).

### Comment Makeup Metrics

Compares the number of human vs. AI comments in reviews.

| Metric | Description |
|--------|-------------|
| `human_comment_count_with_ai` | Human comments on approved revisions that had a Copilot review |
| `human_comment_count_without_ai` | Human comments on approved revisions without Copilot |
| `ai_comment_count` | Total AI comments from quality buckets (excludes neutral and deleted) |
| `ai_comment_rate` | Proportion of AI comments among all comments in Copilot-enabled reviews |

### Comment Quality Metrics

Every AI-generated comment is assigned to exactly one mutually exclusive quality bucket. The buckets are evaluated in priority order:

| Bucket | Condition | Interpretation |
|--------|-----------|----------------|
| `deleted` | `IsDeleted = true` | Reviewer deleted the comment |
| `downvoted` (`bad`) | Has ≥1 downvote (trumps upvotes) | Reviewer explicitly disagreed |
| `upvoted` (`good`) | Has ≥1 upvote and no downvotes | Reviewer explicitly agreed |
| `implicit_good` | `IsResolved = true`, no votes | Comment resolved without explicit feedback — likely acted on |
| `implicit_bad` | In an **approved** revision, not resolved, no votes | Comment was ignored after approval — likely not useful |
| `neutral` | In an **unapproved** revision, not resolved, no votes | No signal yet (review still in progress) |

The sum of all six buckets equals `total_ai_comment_count`.

### Confidence Score Metrics

For each quality bucket (except `neutral`), the average LLM confidence score is also tracked. This allows correlation between confidence and quality outcomes.

| Metric | Description |
|--------|-------------|
| `avg_confidence_upvoted` | Avg. confidence of upvoted comments |
| `avg_confidence_downvoted` | Avg. confidence of downvoted comments |
| `avg_confidence_deleted` | Avg. confidence of deleted comments |
| `avg_confidence_implicit_good` | Avg. confidence of implicitly-good comments |
| `avg_confidence_implicit_bad` | Avg. confidence of implicitly-bad comments |

## How Metrics Are Collected

Metrics are not collected in real-time during reviews. Instead, they are computed **on demand** by querying the APIView Cosmos DB (`Comments` container) and the APIView API for active review metadata. The `avc report metrics` command:

1. Calls the APIView API (`get_active_reviews`) to get all approved package versions in the date range, filtered by language.
2. Queries the Cosmos DB `Comments` container for comments on those revisions.
3. Computes per-language and aggregate `MetricsSegment` objects from the raw data.
4. Optionally saves the segments to the AVC `metrics` Cosmos DB container for downstream consumption by Power BI.
5. Optionally generates PNG charts.

The `MetricsSegment` model is stored in Cosmos DB with a deterministic `id` of the form `{start_date}|{end_date}|dim:{key=value}` and partitioned by month (`{YYYY_MM}`).

## How to Access Metrics

### Power BI Dashboard (recommended)

The primary reporting surface is a [Power BI dashboard](https://msit.powerbi.com/groups/3e17dcb0-4257-4a30-b843-77f47f1d4121/reports/d8fdff73-ac33-49dd-873a-3948d7cb3c48?ctid=72f988bf-86f1-41af-91ab-2d7cd011db47&pbi_source=linkShare). This dashboard reads from the AVC `metrics` Cosmos DB container (which is populated when `--save` is used).

### CLI

Generate a text report for a date range:

```bash
avc report metrics -s 2026-01-01 -e 2026-01-31
```

Generate and save metrics to the database (for Power BI consumption):

```bash
avc report metrics -s 2026-01-01 -e 2026-01-31 --save
```

Generate PNG charts:

```bash
avc report metrics -s 2026-01-01 -e 2026-01-31 --charts
```

Exclude specific languages:

```bash
avc report metrics -s 2026-01-01 -e 2026-01-31 --exclude Java Go
```

Use staging environment:

```bash
avc report metrics -s 2026-01-01 -e 2026-01-31 --environment staging
```

Full option reference:

| Option | Description |
|--------|-------------|
| `-s/--start-date` | Start date (YYYY-MM-DD, inclusive) |
| `-e/--end-date` | End date (YYYY-MM-DD, inclusive) |
| `--environment` | `production` (default) or `staging` |
| `--save` | Persist metrics segments to Cosmos DB |
| `--charts` | Generate PNG charts in `output/charts/` |
| `--exclude` | Languages to exclude (e.g., `--exclude Java Go`) |

### Generated Charts

When `--charts` is passed, four PNG files are written to `output/charts/`:

| File | Description |
|------|-------------|
| `adoption.png` | Stacked bar chart: Copilot vs. non-Copilot reviews per language |
| `comment_quality.png` | Stacked percent bar chart: AI comment quality buckets per language |
| `human_copilot_split.png` | Human vs. AI comments for Copilot-enabled reviews |
| `human_comments_comparison.png` | Human comments with vs. without Copilot, side-by-side |

## Additional Reporting Commands

### Active Reviews

Query active reviews for a language and date range:

```bash
avc report active-reviews -l python -s 2026-01-01 -e 2026-01-31
```

### AI Comment Feedback

Audit AI comments and their reviewer feedback (votes, resolution status):

```bash
avc report feedback -l python -s 2026-01-01 -e 2026-01-31
```

### Memory Audit

List memories stored in the knowledge base, with optional filters:

```bash
avc report memory -l python
```

### Analyze Comments

Analyze AI comment quality for a review or set of comments:

```bash
avc report analyze-comments --review-id <REVIEW_ID>
```

## OpenTelemetry Metrics

In addition to the business metrics described above, the AVC service emits **OpenTelemetry (OTLP) metrics** for operational monitoring. These are shipped to Azure Application Insights and are not directly accessible via the CLI.

| Metric Name | Type | Description |
|-------------|------|-------------|
| `apiview.review.duration` | Histogram (seconds) | Total wall-clock duration of a review |
| `apiview.review.normalized_duration` | Histogram (seconds) | Review duration divided by number of sections processed |
| `apiview.review.requests` | Counter | Total number of review requests received |

All metrics include `review.language`, `review.mode` (full/diff), and `review.status` (success/error) attributes.
