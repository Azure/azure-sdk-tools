# API Review Algorithm

This document describes the full API review pipeline implemented in `src/_apiview_reviewer.py` (`ApiViewReview.run()`). Understanding this pipeline is important for diagnosing review quality issues, tuning prompts, and extending the system.

## Overview

When a review is requested, the `ApiViewReview` class:

1. Splits the API text into sections
2. Runs two LLM prompt types in parallel across all sections (guideline and context)
3. Filters, deduplicates, and scores the resulting comments
4. Returns a sorted, deduplicated list of `Comment` objects

The pipeline supports two **review modes**:

| Mode | When Used | Input |
|------|-----------|-------|
| `full` | No base API provided | Full text of the target API |
| `diff` | Base API provided | Numbered diff between base and target |

## Stages

### Stage 1 — Sectioning

**Purpose:** The full API text may be too large to fit in a single LLM prompt. Sectioning splits it into manageable, semantically coherent chunks.

**Implementation:** `SectionedDocument` (`src/_sectioned_document.py`) splits lines based on indentation and structure. Each section receives a line-number prefix so the LLM can reference exact lines.

**Configuration:**
- Default max chunk size: **500 lines**
- Java / Android: **450 lines** (denser API surfaces)

---

### Stage 2 — Parallel Prompt Evaluation

For each section, prompts run concurrently in a thread pool. Each prompt type targets a different source of review knowledge. Currently only **two prompts** run per section (guideline and context); the generic review is disabled for all languages (see Stage 2c).

#### 2a — Guideline Review (`guidelines_review.prompty` / `guidelines_diff_review.prompty`)

**Purpose:** Check the section against the full set of language-specific design guidelines.

**Context:** All guidelines for the target language, pre-fetched once before section processing begins. Guideline retrieval uses `SearchManager.language_guidelines`, which loads all guidelines filtered by language (excluding `documentation` and `vague` tagged guidelines by default).

**Output:** Comments that cite one or more guideline IDs (`guideline_ids`). Comments with no guideline ID are discarded.

#### 2b — Context Review (`context_review.prompty` / `context_diff_review.prompty`)

**Purpose:** Check the section against the most semantically relevant guidelines, examples, and memories for that specific section.

**Context:** Per-section RAG query: the section text is submitted to Azure AI Search and the top results (guidelines, examples, memories) are assembled into a `Context` object and converted to Markdown for the prompt.

**Output:** Comments that cite one or more memory IDs (`memory_ids`). Comments with no memory ID are discarded.

#### 2c — Generic Review (`generic_review.prompty` / `generic_diff_review.prompty`)

**Purpose:** Apply language-specific custom rules and generic best practices that may not be captured in the formal guidelines.

**Context:** Custom rules from the per-language `metadata/<lang>/guidance.yaml` file.

**Output:** Comments flagged as `is_generic = True`. These undergo additional filtering in Stage 3.

> **Note:** The generic review stage is **disabled for all languages**. Only the guideline and context reviews run. Stages that operate exclusively on generic comments (Stage 3) are effectively no-ops.

---

### Stage 3 — Generic Comment Filtering

> **Note:** This stage is effectively skipped for all languages because the generic review prompt (Stage 2c) is disabled, so no generic comments exist to filter.

**Purpose:** Generic comments are less anchored to documented guidelines and carry higher false-positive risk. This stage validates each generic comment against the knowledge base before keeping it.

**Implementation:** Each generic comment is submitted to `filter_generic_comment.prompty` with:
- The comment text
- A semantic search result from the KB for that comment's content as additional context

The prompt returns `KEEP` or `DISCARD`. Discarded comments are removed; kept comments proceed.

This stage runs in parallel across all generic comments.

---

### Stage 4 — Deduplication

**Purpose:** Multiple prompt runs (guideline, context, generic) often produce comments about the same line. This stage merges comments on the same line.

**Implementation:**
1. Comments are grouped by `line_no`.
2. Lines with a single comment pass through unchanged.
3. Lines with multiple comments are submitted to `merge_comments.prompty`, which merges them into a single comment preserving the strongest evidence.

This stage runs in parallel across all multi-comment lines.

---

### Stage 5 — Hard Filtering (Metadata Filter)

**Purpose:** Remove comments that conflict with known exceptions or do not apply to the current API scope.

**Context provided to the prompt:**
- The comment
- The API outline (if provided), which describes the package structure
- Per-language filter exceptions from `metadata/<lang>/filter.yaml`

**Implementation:** Each comment is submitted to `filter_comment_with_metadata.prompty`, which returns `KEEP` or `DISCARD`.

This stage runs in parallel across all remaining comments.

---

### Stage 6 — Pre-existing Comment Filtering

**Purpose:** If the APIView already has human comments on the same line as a proposed AI comment, the AI comment may be redundant or contradictory. This stage resolves conflicts.

**Context provided to the prompt:**
- The proposed AI comment
- All existing human comments on the same line (passed in from the API request)

**Implementation:** Each conflicting pair is submitted to `filter_existing_comment.prompty`, which returns:
- `KEEP` — Keep the AI comment (possibly with a refined text)
- `DISCARD` — Remove the AI comment (superseded by human comment)

This stage runs in parallel across all comments with conflicts.

---

### Stage 7 — Judge Scoring

**Purpose:** Assign a **confidence score** (0.0–1.0) and **severity level** to each surviving comment. Both values are stored on the comment and included in the output, but the current implementation does not filter or rank by confidence.

**Implementation:** Each comment is submitted to `judge_comment_confidence.prompty` with the comment and relevant KB context (from `guideline_ids` and `memory_ids`). The prompt performs a multi-question review and returns:
- `results`: a list of `YES`/`NO`/`UNKNOWN` answers from several internal reviewers
- `severity`: `MUST`, `SHOULD`, `SUGGESTION`, or `QUESTION`

The confidence score is: `yes_votes / total_votes`.

This stage runs in parallel across all remaining comments.

---

### Stage 8 — Correlation ID Assignment

**Purpose:** Group semantically similar comments across different lines so that APIView can batch-display or collapse them. This is purely a presentation layer optimization and does not remove comments.

**Implementation:** `CommentGrouper` (`src/_comment_grouper.py`) submits groups of comments to an LLM and assigns a shared `correlation_id` to similar ones.

---

## Final Output

After all stages, comments are sorted (by line number) and returned as a `ReviewResult` containing a list of `Comment` objects:

| Field | Description |
|-------|-------------|
| `line_no` | Line number in the API text |
| `bad_code` | The verbatim offending code snippet |
| `suggestion` | Suggested fix (code or description) |
| `comment` | Human-readable review comment |
| `guideline_ids` | Guidelines violated (if any) |
| `memory_ids` | Memories referenced (if any) |
| `is_generic` | Whether the comment came from the generic review |
| `confidence_score` | Judge score (0.0–1.0) |
| `severity` | `MUST`, `SHOULD`, `SUGGESTION`, or `QUESTION` |
| `correlation_id` | ID for grouping similar comments |

## Prompt Files

All prompts live under `prompts/api_review/`:

| File | Stage |
|------|-------|
| `guidelines_review.prompty` | Guideline review (full mode) |
| `guidelines_diff_review.prompty` | Guideline review (diff mode) |
| `context_review.prompty` | Context review (full mode) |
| `context_diff_review.prompty` | Context review (diff mode) |
| `generic_review.prompty` | Generic review (full mode) |
| `generic_diff_review.prompty` | Generic review (diff mode) |
| `filter_generic_comment.prompty` | Generic comment filter |
| `merge_comments.prompty` | Deduplication merge |
| `filter_comment_with_metadata.prompty` | Hard filter |
| `filter_existing_comment.prompty` | Pre-existing comment filter |
| `judge_comment_confidence.prompty` | Judge scoring |

## Configuration Hooks

| Hook | Location | Description |
|------|----------|-------------|
| `metadata/<lang>/guidance.yaml` | `custom_rules` key | Extra language-specific rules for the generic review prompt |
| `metadata/<lang>/filter.yaml` | `exceptions` key | Patterns that should never be flagged for this language |
| API outline (`--outline`) | CLI / request body | Package structure text to help filter out-of-scope comments |
| Existing comments (`--existing-comments`) | CLI / request body | Pre-existing human comments used in pre-existing comment filtering |

## Debugging a Review Locally

Run with `--debug-log` to write intermediate results to `scratch/output/<job_id>/`:

```bash
avc review generate -l python -t scratch/apiviews/python/myapi.txt --debug-log
```

Debug files include:
- `filter_generic_comments_KEEP.json` / `_DISCARD.json` — Generic comment filter decisions
- `filter_comments_with_metadata_KEEP.json` / `_DISCARD.json` — Hard filter decisions
- `judge_comments/judge_comment_<N>.json` — Per-comment judge results
- `output.json` — Final review output
