# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import sys
from dataclasses import asdict, dataclass, field
from datetime import date
from pathlib import Path
from typing import Dict, List, Optional

from src._apiview import (
    ActiveReviewMetadata,
    get_active_reviews,
)
from src._database_manager import DatabaseManager

# Minimal fields needed from the Comments container for metrics computation.
METRICS_COMMENT_FIELDS = [
    "ReviewId",
    "APIRevisionId",
    "CommentSource",
    "IsDeleted",
    "IsGeneric",
    "IsResolved",
    "Upvotes",
    "Downvotes",
    "ConfidenceScore",
    "ThreadId",
    "ElementId",
    "CreatedOn",
]


@dataclass
class MetricsSegment:
    """
    Represents one time-window ("segment") of Copilot metrics
    for a specific dimension (e.g., language) or for all (dimension = {}).
    """

    start_date: date
    end_date: date
    segment_days: Optional[int] = None

    # Core review counts
    active_review_count: Optional[int] = None
    active_copilot_review_count: Optional[int] = None

    # Human comment counts
    human_comment_count_with_ai: Optional[int] = None
    human_comment_count_without_ai: Optional[int] = None

    # AI comment classifications (mutually exclusive, sum to total_ai_comment_count)
    total_ai_comment_count: Optional[int] = None
    upvoted_ai_comment_count: Optional[int] = None
    downvoted_ai_comment_count: Optional[int] = None
    deleted_ai_comment_count: Optional[int] = None
    implicit_good_ai_comment_count: Optional[int] = None
    implicit_bad_ai_comment_count: Optional[int] = None
    neutral_ai_comment_count: Optional[int] = None

    # Average confidence scores per category (None when no confidence scores available)
    avg_confidence_upvoted: Optional[float] = None
    avg_confidence_downvoted: Optional[float] = None
    avg_confidence_deleted: Optional[float] = None
    avg_confidence_implicit_good: Optional[float] = None
    avg_confidence_implicit_bad: Optional[float] = None

    # Dimension (e.g., {"language": "python"}) or {} for "All"
    dimension: Dict[str, str] = field(default_factory=dict)

    # Cosmos metadata
    type: str = field(default="metrics_segment", init=False)
    id: Optional[str] = None
    pk: Optional[str] = None

    def compute_ids(self):
        """Generate deterministic CosmosDB ID and partition key, lowercasing dimension values and normalizing language."""
        start = self.start_date
        end = self.end_date

        def normalize_dim_item(k, v):
            k = k.lower()
            v = v.lower()
            if k == "language" and v == "c#":
                v = "csharp"
            return f"{k}={v}"

        if not self.dimension:
            dim_key = "dim:all"
        else:
            dim_key = "dim:" + ";".join(normalize_dim_item(k, v) for k, v in sorted(self.dimension.items()))
        # Use ISO strings for stable IDs
        self.id = f"{start.isoformat()}|{end.isoformat()}|{dim_key}"
        self.pk = start.strftime("%Y_%m")

    def to_dict(self) -> Dict:
        """Return JSON-serializable dict for CosmosDB upsert."""
        if not self.id or not self.pk:
            self.compute_ids()
        d = asdict(self)
        d["start_date"] = self.start_date.isoformat()
        d["end_date"] = self.end_date.isoformat()
        return d

    def __post_init__(self):
        """Normalize dates, compute segment_days, and always set id/pk."""
        # Normalize to date objects if strings were passed
        if isinstance(self.start_date, str):
            self.start_date = date.fromisoformat(self.start_date)
        if isinstance(self.end_date, str):
            self.end_date = date.fromisoformat(self.end_date)

        # Compute inclusive segment days (at least 1)
        delta_days = (self.end_date - self.start_date).days + 1
        self.segment_days = max(1, delta_days)


def get_metrics_report(
    start_date: str,
    end_date: str,
    environment: str,
    save: bool = False,
    charts: bool = False,
    exclude: Optional[List[str]] = None,
) -> dict:
    data = _build_metrics_data(start_date=start_date, end_date=end_date, environment=environment, exclude=exclude)

    if not data:
        raise ValueError("No data found for metrics report")
    if save:
        db_manager = DatabaseManager.get_instance(environment=environment)
        cosmos_client = db_manager.get_container_client("metrics")
        for doc in data.values():
            # do not save language-agnostic overall metrics to CosmosDB. PowerBI will calculate these.
            if not doc.dimension.get("language", None):
                continue
            try:
                cosmos_client.upsert(doc.id, data=doc.to_dict())
            except Exception as e:
                print(f"Error upserting document {doc.id}: {e}", file=sys.stderr)

    report = _build_metrics_report(data)

    if charts:
        _generate_charts(report, start_date, end_date)

    return report


def _thread_started_in_window(
    comment: dict, thread_start_index: dict, window_start_iso: str, window_end_iso: str
) -> bool:
    """Return True if the thread containing *comment* started within the window.

    *thread_start_index* is produced by :func:`src._apiview.get_thread_start_dates` and
    maps ThreadId (or ElementId for unthreaded comments) to earliest CreatedOn.
    """
    thread_id = comment.get("ThreadId")
    if thread_id:
        thread_start = thread_start_index.get(thread_id)
    else:
        thread_start = thread_start_index.get(comment.get("ElementId"))
    if not thread_start:
        return True
    return window_start_iso <= thread_start <= window_end_iso


def _filter_to_root_comments(comments: list[dict]) -> list[dict]:
    """Reduce comments to thread roots only.

    For comments that share a ``ThreadId``, only the earliest comment (by
    ``CreatedOn``) is kept.  For comments without a ``ThreadId``, only the
    earliest comment per ``(ReviewId, ElementId)`` is kept.  This matches the
    key scheme used by :func:`get_thread_start_dates` which scopes unthreaded
    lookups by ReviewId.
    """
    from collections import defaultdict

    threaded: dict[str, list[dict]] = defaultdict(list)
    unthreaded: dict[tuple, list[dict]] = defaultdict(list)

    for c in comments:
        thread_id = c.get("ThreadId")
        if thread_id:
            threaded[thread_id].append(c)
        else:
            key = (c.get("ReviewId"), c.get("ElementId"))
            unthreaded[key].append(c)

    roots: list[dict] = []

    for group in threaded.values():
        group.sort(key=lambda x: x.get("CreatedOn") or "\uffff")
        roots.append(group[0])

    for group in unthreaded.values():
        group.sort(key=lambda x: x.get("CreatedOn") or "\uffff")
        roots.append(group[0])

    return roots


def _build_metrics_segment(
    *,
    start_date: str,
    end_date: str,
    reviews: list[ActiveReviewMetadata],
    comments: list[dict],
    language: Optional[str] = None,
    thread_start_index: Optional[dict] = None,
) -> MetricsSegment:
    """Build a MetricsSegment from reviews and raw comment dicts.

    Produces three metric groups:

    **adoption** — Based on *approved* package versions only.
      - ``active_review_count``: number of approved package versions.
      - ``active_copilot_review_count``: approved package versions that had a Copilot review.

    **comment_makeup** — Describes the composition of comments (excluding Diagnostic
    comments). Human comments are counted from *approved* package versions only and
    split by whether the package version had Copilot enabled
    (``human_comment_count_with_ai`` vs ``human_comment_count_without_ai``). The AI
    comment count for makeup is derived from the comment_quality buckets:
    ``upvoted + downvoted + implicit_good + implicit_bad``. Because the quality
    buckets span *all* active revisions (approved and unapproved), the AI portion
    of makeup includes signaled comments from any revision, plus unsignaled comments
    from approved revisions (implicit_bad). Only deleted and neutral comments are
    excluded. The ``ai_comment_rate`` is the ratio of that AI count to (AI count +
    human comments from Copilot-enabled package versions).

    **comment_quality** — Evaluates AI comment quality across *all* active package versions
    (approved **and** unapproved). Every AI comment is placed into exactly one
    mutually-exclusive bucket, checked in this priority order:
      1. **deleted** — ``IsDeleted`` is truthy.
      2. **downvoted** — Has at least one downvote (trumps any upvotes).
      3. **upvoted** — Has at least one upvote and no downvotes.
      4. **implicit_good** — ``IsResolved`` is truthy (no votes).
      5. **implicit_bad** — In an *approved* package version, not resolved, no votes.
      6. **neutral** — In an *unapproved* package version, not resolved, no votes.

    The sum of all six buckets equals ``total_ai_comment_count``.
    """
    metrics = MetricsSegment(start_date=start_date, end_date=end_date)
    metrics.type = "metrics_segment"

    if language:
        metrics.dimension = {"language": language}
    else:
        metrics.dimension = {}
    # need language set to compute IDs
    metrics.compute_ids()

    # Extract ALL revisions from get_active_reviews results
    all_revisions = [rev for r in reviews for rev in r.revisions]
    approved_revisions = [rev for rev in all_revisions if rev.approval is not None]

    metrics.active_review_count = len(approved_revisions)
    metrics.active_copilot_review_count = sum(1 for rev in approved_revisions if rev.has_copilot_review)

    # Build mappings for all revisions and approved revisions
    all_revision_ids = set()
    approved_revision_ids = set()
    revision_has_copilot = {}

    for review in reviews:
        for rev in review.revisions:
            for revision_id in rev.revision_ids:
                all_revision_ids.add(revision_id)
                revision_has_copilot[revision_id] = rev.has_copilot_review
                if rev.approval is not None:
                    approved_revision_ids.add(revision_id)

    # Reduce to root comments only (one per thread/line) so replies are not counted separately.
    root_comments = _filter_to_root_comments(
        [c for c in comments if c.get("CommentSource") != "Diagnostic"]
    )

    # When a thread start index is provided, only count threads
    # that originated within the current window (applies to both human and AI).
    if thread_start_index is not None:
        from src._utils import to_iso8601

        window_start_iso = to_iso8601(start_date)
        window_end_iso = to_iso8601(end_date, end_of_day=True)

        root_comments = [
            c for c in root_comments
            if _thread_started_in_window(c, thread_start_index, window_start_iso, window_end_iso)
        ]

    # Filter root comments to only those belonging to approved revisions
    # (for human comment counts - comment_makeup metrics)
    approved_revision_comments = [
        c for c in root_comments
        if c.get("APIRevisionId") in approved_revision_ids
    ]

    # Categorize comments based on whether the revision has Copilot (for comment_makeup)
    human_comments_with_copilot_count = 0
    human_comments_without_copilot_count = 0

    for comment in approved_revision_comments:
        rev_id = comment.get("APIRevisionId")
        has_copilot = revision_has_copilot.get(rev_id, False)
        source = comment.get("CommentSource")

        if source == "AIGenerated":
            continue

        if has_copilot:
            human_comments_with_copilot_count += 1
        else:
            human_comments_without_copilot_count += 1

    metrics.human_comment_count_with_ai = human_comments_with_copilot_count
    metrics.human_comment_count_without_ai = human_comments_without_copilot_count

    # For comment_quality: count ALL AI comments across ALL active revisions (approved + unapproved)
    all_ai_comments = [
        c for c in root_comments
        if c.get("APIRevisionId") in all_revision_ids and c.get("CommentSource") == "AIGenerated"
    ]

    # Categorize AI comments (mutually exclusive, in priority order)
    deleted_count = 0
    downvoted_count = 0
    upvoted_count = 0
    implicit_good_count = 0
    implicit_bad_count = 0
    neutral_count = 0

    # Accumulate confidence scores per category
    scores_upvoted: list[float] = []
    scores_downvoted: list[float] = []
    scores_deleted: list[float] = []
    scores_implicit_good: list[float] = []
    scores_implicit_bad: list[float] = []

    for c in all_ai_comments:
        score = c.get("ConfidenceScore")
        if c.get("IsDeleted"):
            deleted_count += 1
            if score is not None:
                scores_deleted.append(score)
        elif c.get("Downvotes"):
            # Any downvote trumps upvotes
            downvoted_count += 1
            if score is not None:
                scores_downvoted.append(score)
        elif c.get("Upvotes"):
            upvoted_count += 1
            if score is not None:
                scores_upvoted.append(score)
        elif c.get("IsResolved"):
            implicit_good_count += 1
            if score is not None:
                scores_implicit_good.append(score)
        elif c.get("APIRevisionId") in approved_revision_ids:
            # In approved revision, not resolved, no votes = implicit bad
            implicit_bad_count += 1
            if score is not None:
                scores_implicit_bad.append(score)
        else:
            # In unapproved revision, not resolved, no votes = neutral
            neutral_count += 1

    def _avg(scores: list[float]) -> Optional[float]:
        return sum(scores) / len(scores) if scores else None

    metrics.total_ai_comment_count = len(all_ai_comments)
    metrics.deleted_ai_comment_count = deleted_count
    metrics.downvoted_ai_comment_count = downvoted_count
    metrics.upvoted_ai_comment_count = upvoted_count
    metrics.implicit_good_ai_comment_count = implicit_good_count
    metrics.implicit_bad_ai_comment_count = implicit_bad_count
    metrics.neutral_ai_comment_count = neutral_count

    metrics.avg_confidence_upvoted = _avg(scores_upvoted)
    metrics.avg_confidence_downvoted = _avg(scores_downvoted)
    metrics.avg_confidence_deleted = _avg(scores_deleted)
    metrics.avg_confidence_implicit_good = _avg(scores_implicit_good)
    metrics.avg_confidence_implicit_bad = _avg(scores_implicit_bad)

    return metrics


def _calculate_adoption_rate(segment: MetricsSegment) -> float:
    """Calculate the adoption rate of Copilot in a given segment."""
    if segment.active_review_count == 0:
        return 0.0
    return segment.active_copilot_review_count / segment.active_review_count


def _calculate_rate(count: int, total: int) -> float:
    """Calculate a rate as count / total, returning 0.0 if total is 0."""
    if total == 0:
        return 0.0
    return count / total


def _calculate_ai_comment_rate(segment: MetricsSegment) -> float:
    """Calculate the rate of AI comments in comment_makeup (approved revisions only)."""
    # For comment_makeup, we use the sum of non-deleted AI comments from approved revisions
    # This excludes deleted and includes only upvoted + downvoted + implicit_good + implicit_bad
    # (neutral are in unapproved revisions, so excluded from comment_makeup ai_comment_count)
    ai_count = (
        segment.upvoted_ai_comment_count
        + segment.downvoted_ai_comment_count
        + segment.implicit_good_ai_comment_count
        + segment.implicit_bad_ai_comment_count
    )
    total = ai_count + segment.human_comment_count_with_ai
    return _calculate_rate(ai_count, total)


def _build_metrics_report(data: Dict[str, MetricsSegment]) -> dict:
    """Build a metrics report from the raw data."""
    start_date = data["overall"].start_date
    end_date = data["overall"].end_date

    report = {"start_date": start_date, "end_date": end_date, "metrics": {}}

    def fmt(val):
        return round(val, 2) if isinstance(val, float) else val

    for language, segment in data.items():
        report["metrics"][language] = {
            "adoption": {
                "active_review_count": segment.active_review_count,
                "copilot_review_count": segment.active_copilot_review_count,
                "adoption_rate": fmt(_calculate_adoption_rate(segment)),
            },
            "comment_quality": {
                # Total AI comments across ALL active revisions (approved + unapproved)
                "ai_comment_count": segment.total_ai_comment_count,
                # Backward compat fractions
                "good": fmt(_calculate_rate(segment.upvoted_ai_comment_count, segment.total_ai_comment_count)),
                "bad": fmt(_calculate_rate(segment.downvoted_ai_comment_count, segment.total_ai_comment_count)),
                "neutral": fmt(_calculate_rate(segment.neutral_ai_comment_count, segment.total_ai_comment_count)),
                # New detailed counts and fractions
                "good_count": segment.upvoted_ai_comment_count,
                "bad_count": segment.downvoted_ai_comment_count,
                "deleted_count": segment.deleted_ai_comment_count,
                "deleted": fmt(_calculate_rate(segment.deleted_ai_comment_count, segment.total_ai_comment_count)),
                "implicit_good_count": segment.implicit_good_ai_comment_count,
                "implicit_good": fmt(
                    _calculate_rate(segment.implicit_good_ai_comment_count, segment.total_ai_comment_count)
                ),
                "implicit_bad_count": segment.implicit_bad_ai_comment_count,
                "implicit_bad": fmt(
                    _calculate_rate(segment.implicit_bad_ai_comment_count, segment.total_ai_comment_count)
                ),
                "neutral_count": segment.neutral_ai_comment_count,
                # Average confidence scores per category
                "avg_confidence_good": fmt(segment.avg_confidence_upvoted) if segment.avg_confidence_upvoted is not None else None,
                "avg_confidence_bad": fmt(segment.avg_confidence_downvoted) if segment.avg_confidence_downvoted is not None else None,
                "avg_confidence_deleted": fmt(segment.avg_confidence_deleted) if segment.avg_confidence_deleted is not None else None,
                "avg_confidence_implicit_good": fmt(segment.avg_confidence_implicit_good) if segment.avg_confidence_implicit_good is not None else None,
                "avg_confidence_implicit_bad": fmt(segment.avg_confidence_implicit_bad) if segment.avg_confidence_implicit_bad is not None else None,
            },
            "comment_makeup": {
                "human_comment_count_without_copilot": segment.human_comment_count_without_ai,
                "human_comment_count_with_ai": segment.human_comment_count_with_ai,
                # For comment_makeup, ai_comment_count is from approved revisions only (excluding deleted + neutral)
                "ai_comment_count": (
                    segment.upvoted_ai_comment_count
                    + segment.downvoted_ai_comment_count
                    + segment.implicit_good_ai_comment_count
                    + segment.implicit_bad_ai_comment_count
                ),
                "ai_comment_rate": fmt(_calculate_ai_comment_rate(segment)),
            },
        }
    return report


def _build_metrics_data(
    start_date: str, end_date: str, environment: str, exclude: Optional[List[str]] = None
) -> Optional[Dict[str, MetricsSegment]]:
    """Package metrics data for a report or publishing."""
    # filter out C and C++ since they are not supported by Copilot
    # See: https://github.com/Azure/azure-sdk-tools/issues/10465
    pretty_languages_to_omit = ["c++", "c", "typespec", "swagger", "xml"]
    # Add user-specified exclusions (case-insensitive)
    if exclude:
        pretty_languages_to_omit.extend([lang.lower() for lang in exclude])

    active_reviews, raw_comments = get_active_reviews(
        start_date, end_date, environment=environment, omit_languages=pretty_languages_to_omit,
        select_fields=METRICS_COMMENT_FIELDS,
    )

    results = {}
    results["overall"] = _build_metrics_segment(
        start_date=start_date, end_date=end_date, reviews=active_reviews, comments=raw_comments
    )

    languages_to_package = {r.language for r in active_reviews}
    for language in languages_to_package:
        filtered_reviews = [r for r in active_reviews if r.language == language]
        results[language] = _build_metrics_segment(
            start_date=start_date, end_date=end_date, reviews=filtered_reviews, comments=raw_comments, language=language
        )

    return results


def _generate_charts(report: dict, start_date: str, end_date: str) -> None:
    """Generate PNG charts from the metrics report."""
    try:
        import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib is required for chart generation. Install it with: pip install matplotlib", file=sys.stderr)
        return

    metrics = report.get("metrics", {})
    # Get per-language data sorted, then append "overall" at the end
    languages = sorted([lang for lang in metrics.keys() if lang != "overall"])
    if "overall" in metrics:
        languages_with_overall = languages + ["Overall"]
        # Create a metrics lookup that maps "Overall" to the "overall" key
        metrics_lookup = {lang: metrics[lang] for lang in languages}
        metrics_lookup["Overall"] = metrics["overall"]
    else:
        languages_with_overall = languages
        metrics_lookup = metrics

    if not languages:
        print("No language-specific metrics to chart.", file=sys.stderr)
        return

    output_dir = Path("output/charts")
    output_dir.mkdir(parents=True, exist_ok=True)

    # Chart 1: Adoption - stacked bar with copilot (green) and non-copilot (yellow)
    copilot_counts = [metrics_lookup[lang]["adoption"]["copilot_review_count"] for lang in languages_with_overall]
    total_counts = [metrics_lookup[lang]["adoption"]["active_review_count"] for lang in languages_with_overall]
    non_copilot_counts = [t - c for t, c in zip(total_counts, copilot_counts)]

    plt.figure(figsize=(12, 6))
    x = range(len(languages_with_overall))
    plt.bar(x, copilot_counts, color="green", label="Copilot Reviews")
    plt.bar(x, non_copilot_counts, bottom=copilot_counts, color="gold", label="Non-Copilot Reviews")
    plt.xlabel("Language")
    plt.ylabel("Review Count")
    plt.title(f"Copilot Adoption by Language\n({start_date} to {end_date})")
    plt.xticks(x, languages_with_overall, rotation=45, ha="right")
    plt.legend(loc="upper right")
    for i, (c, nc) in enumerate(zip(copilot_counts, non_copilot_counts)):
        total = c + nc
        if total > 0:
            plt.text(i, total + 0.5, str(total), ha="center", va="bottom", fontsize=8)
    plt.tight_layout()
    adoption_path = output_dir / "adoption.png"
    plt.savefig(adoption_path, dpi=150)
    plt.close()
    print(f"Saved: {adoption_path}", file=sys.stderr)

    # Chart 2: Comment Quality - stacked fraction bar chart (excluding neutral)
    # Order from bottom to top: good, implicit_good, implicit_bad, bad, deleted
    count_categories = ["good_count", "implicit_good_count", "implicit_bad_count", "bad_count", "deleted_count"]
    colors = ["darkgreen", "lightgreen", "lightcoral", "red", "darkred"]
    labels = ["Good (upvoted)", "Implicit Good", "Implicit Bad", "Bad (downvoted)", "Deleted"]

    # Calculate totals excluding neutral for each language
    totals_excl_neutral = []
    for lang in languages_with_overall:
        q = metrics_lookup[lang]["comment_quality"]
        total = sum(q.get(cat, 0) for cat in count_categories)
        totals_excl_neutral.append(total)

    plt.figure(figsize=(12, 6))
    x = range(len(languages_with_overall))
    bottom = [0.0] * len(languages_with_overall)

    for cat, color, label in zip(count_categories, colors, labels):
        counts = [metrics_lookup[lang]["comment_quality"].get(cat, 0) for lang in languages_with_overall]
        fractions = [c / t if t > 0 else 0.0 for c, t in zip(counts, totals_excl_neutral)]
        plt.bar(x, fractions, bottom=bottom, color=color, label=label)
        bottom = [b + f for b, f in zip(bottom, fractions)]

    plt.xlabel("Language")
    plt.ylabel("Fraction of AI Comments")
    plt.title(f"AI Comment Quality by Language\n({start_date} to {end_date})")
    plt.xticks(x, languages_with_overall, rotation=45, ha="right")
    plt.ylim(0, 1.05)
    plt.legend(loc="upper center", bbox_to_anchor=(0.5, -0.15), ncol=5, fontsize=8)
    plt.tight_layout()
    plt.subplots_adjust(bottom=0.25)
    quality_path = output_dir / "comment_quality.png"
    plt.savefig(quality_path, dpi=150)
    plt.close()
    print(f"Saved: {quality_path}", file=sys.stderr)

    # Chart 3: Human-Copilot Split - for languages WITH copilot reviews
    # Stacked bar: human comments + AI comments
    langs_with_copilot = [
        lang for lang in languages_with_overall if metrics_lookup[lang]["adoption"]["copilot_review_count"] > 0
    ]

    if langs_with_copilot:
        human_with_ai = [
            metrics_lookup[lang]["comment_makeup"]["human_comment_count_with_ai"] for lang in langs_with_copilot
        ]
        ai_counts = [metrics_lookup[lang]["comment_makeup"]["ai_comment_count"] for lang in langs_with_copilot]

        plt.figure(figsize=(12, 6))
        x = range(len(langs_with_copilot))
        plt.bar(x, ai_counts, color="steelblue", label="AI Comments")
        plt.bar(x, human_with_ai, bottom=ai_counts, color="lightgreen", label="Human Comments")
        plt.xlabel("Language")
        plt.ylabel("Comment Count")
        plt.title(f"Human vs AI Comments (Reviews with Copilot)\n({start_date} to {end_date})")
        plt.xticks(x, langs_with_copilot, rotation=45, ha="right")
        plt.legend(loc="upper right")
        for i, (ai, hum) in enumerate(zip(ai_counts, human_with_ai)):
            total = ai + hum
            if total > 0:
                plt.text(i, total + 0.5, str(total), ha="center", va="bottom", fontsize=8)
        plt.tight_layout()
        split_path = output_dir / "human_copilot_split.png"
        plt.savefig(split_path, dpi=150)
        plt.close()
        print(f"Saved: {split_path}", file=sys.stderr)
    else:
        print("No languages with Copilot reviews for human-copilot split chart.", file=sys.stderr)

    # Chart 4: Human Comments With vs Without Copilot - side-by-side bars
    human_with = [
        metrics_lookup[lang]["comment_makeup"]["human_comment_count_with_ai"] for lang in languages_with_overall
    ]
    human_without = [
        metrics_lookup[lang]["comment_makeup"]["human_comment_count_without_copilot"] for lang in languages_with_overall
    ]

    plt.figure(figsize=(12, 6))
    x = range(len(languages_with_overall))
    width = 0.35
    plt.bar([i - width / 2 for i in x], human_with, width, color="lightgreen", label="With Copilot")
    plt.bar([i + width / 2 for i in x], human_without, width, color="salmon", label="Without Copilot")
    plt.xlabel("Language")
    plt.ylabel("Human Comment Count")
    plt.title(f"Human Comments: With vs Without Copilot\n({start_date} to {end_date})")
    plt.xticks(x, languages_with_overall, rotation=45, ha="right")
    plt.legend(loc="upper right")
    plt.tight_layout()
    compare_path = output_dir / "human_comments_comparison.png"
    plt.savefig(compare_path, dpi=150)
    plt.close()
    print(f"Saved: {compare_path}", file=sys.stderr)
