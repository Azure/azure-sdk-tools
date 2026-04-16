# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Generate multi-language comment bucket trend charts from production APIView data."""

from __future__ import annotations

import argparse
import calendar
import math
import sys
from dataclasses import asdict, dataclass
from datetime import date
from pathlib import Path
from typing import Optional

REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src._apiview import (
    ActiveReviewMetadata,
    ActiveRevisionMetadata,
    get_active_reviews,
    get_apiview_cosmos_client,
    get_comments_in_date_range,
    get_version_type,
)
from src._metrics import METRICS_COMMENT_FIELDS, _build_metrics_segment
from src._utils import get_language_pretty_name, to_iso8601

PRODUCTION_ENVIRONMENT = "production"
DEFAULT_LANGUAGES = ["Python", "C#", "Java", "JavaScript"]
DEFAULT_MONTHS = 6
DEFAULT_OUTPUT_PATH = Path("output/charts/comment_bucket_trends.png")
OMIT_LANGUAGES = ["c++", "c", "typespec", "swagger", "xml"]


@dataclass
class MonthlyCommentBucketPoint:
    """Represents one monthly stacked comment-bucket point for a language."""

    label: str
    start_date: str
    end_date: str
    total_included_comment_count: int
    good_count: int
    implicit_good_count: int
    neutral_count: int
    human_comment_count: int
    implicit_bad_count: int
    bad_count: int
    deleted_count: int
    good_percentage: float
    implicit_good_percentage: float
    neutral_percentage: float
    human_percentage: float
    implicit_bad_percentage: float
    bad_percentage: float
    deleted_percentage: float
    positive_boundary_percentage: float


def get_last_n_month_ranges(months: int = DEFAULT_MONTHS, today: Optional[date] = None) -> list[tuple[date, date]]:
    """Return discrete calendar-month ranges for the current month and the previous months."""
    if months < 1:
        raise ValueError("months must be at least 1")

    today = today or date.today()
    current_month_index = today.year * 12 + (today.month - 1)
    ranges: list[tuple[date, date]] = []

    for month_index in range(current_month_index - months + 1, current_month_index + 1):
        year = month_index // 12
        month = (month_index % 12) + 1
        last_day = calendar.monthrange(year, month)[1]
        ranges.append((date(year, month, 1), date(year, month, last_day)))

    return ranges


def _to_percentage(count: int, total: int) -> float:
    """Convert a count to a percentage safely."""
    if total == 0:
        return 0.0
    return round((count / total) * 100, 2)


def _build_language_comment_bucket_point(
    language: str,
    start_date: date,
    end_date: date,
    reviews: list[object],
    raw_comments: list[dict],
    *,
    include_human: bool,
    include_neutral: bool,
) -> MonthlyCommentBucketPoint:
    """Build one monthly bucket point using the current metrics.py logic plus optional buckets."""
    filtered_reviews = [review for review in reviews if review.language.lower() == language.lower()]
    segment = _build_metrics_segment(
        start_date=start_date.isoformat(),
        end_date=end_date.isoformat(),
        reviews=filtered_reviews,
        comments=raw_comments,
        language=language,
    )

    good_count = segment.upvoted_ai_comment_count or 0
    implicit_good_count = segment.implicit_good_ai_comment_count or 0
    neutral_count = (segment.neutral_ai_comment_count or 0) if include_neutral else 0
    human_count = (segment.human_comment_count_with_ai or 0) if include_human else 0
    implicit_bad_count = segment.implicit_bad_ai_comment_count or 0
    bad_count = segment.downvoted_ai_comment_count or 0
    deleted_count = segment.deleted_ai_comment_count or 0

    total_included = (
        good_count
        + implicit_good_count
        + neutral_count
        + human_count
        + implicit_bad_count
        + bad_count
        + deleted_count
    )

    good_percentage = _to_percentage(good_count, total_included)
    implicit_good_percentage = _to_percentage(implicit_good_count, total_included)
    neutral_percentage = _to_percentage(neutral_count, total_included)
    human_percentage = _to_percentage(human_count, total_included)
    implicit_bad_percentage = _to_percentage(implicit_bad_count, total_included)
    bad_percentage = _to_percentage(bad_count, total_included)
    deleted_percentage = _to_percentage(deleted_count, total_included)
    positive_boundary_percentage = round(good_percentage + implicit_good_percentage, 2)

    return MonthlyCommentBucketPoint(
        label=start_date.strftime("%Y-%m"),
        start_date=start_date.isoformat(),
        end_date=end_date.isoformat(),
        total_included_comment_count=total_included,
        good_count=good_count,
        implicit_good_count=implicit_good_count,
        neutral_count=neutral_count,
        human_comment_count=human_count,
        implicit_bad_count=implicit_bad_count,
        bad_count=bad_count,
        deleted_count=deleted_count,
        good_percentage=good_percentage,
        implicit_good_percentage=implicit_good_percentage,
        neutral_percentage=neutral_percentage,
        human_percentage=human_percentage,
        implicit_bad_percentage=implicit_bad_percentage,
        bad_percentage=bad_percentage,
        deleted_percentage=deleted_percentage,
        positive_boundary_percentage=positive_boundary_percentage,
    )


def build_language_comment_bucket_reports(
    languages: Optional[list[str]] = None,
    months: int = DEFAULT_MONTHS,
    today: Optional[date] = None,
    *,
    include_human: bool = False,
    include_neutral: bool = False,
) -> dict[str, list[dict]]:
    """Build per-language bucket reports for the last N discrete calendar months.

    Fetches all Cosmos data once for the full date range (3 queries), then
    partitions comments by month locally to avoid repeated round-trips.
    """
    selected_languages = languages or DEFAULT_LANGUAGES
    reports = {language: [] for language in selected_languages}
    month_ranges = get_last_n_month_ranges(months=months, today=today)
    if not month_ranges:
        return reports

    full_start = month_ranges[0][0]
    full_end = month_ranges[-1][1]

    # --- Single fetch for the entire window (3 Cosmos queries) ---
    # Need CreatedOn in the projection so we can partition by month locally.
    select_fields = list(METRICS_COMMENT_FIELDS)
    if "CreatedOn" not in select_fields:
        select_fields.append("CreatedOn")

    all_raw_comments = get_comments_in_date_range(
        full_start.isoformat(),
        full_end.isoformat(),
        environment=PRODUCTION_ENVIRONMENT,
        select_fields=select_fields,
    )
    non_diag = [c for c in all_raw_comments if c.get("CommentSource") != "Diagnostic"]

    review_ids: set[str] = set()
    revision_ids: set[str] = set()
    for comment in non_diag:
        rid = comment.get("ReviewId")
        rev_id = comment.get("APIRevisionId")
        if rid:
            review_ids.add(rid)
        if rev_id:
            revision_ids.add(rev_id)

    review_results: list[dict] = []
    revision_results: list[dict] = []

    if review_ids:
        reviews_container = get_apiview_cosmos_client(container_name="Reviews", environment=PRODUCTION_ENVIRONMENT)
        params = [{"name": f"@id_{i}", "value": rid} for i, rid in enumerate(review_ids)]
        clauses = [f"c.id = @id_{i}" for i in range(len(review_ids))]
        query = f"SELECT c.id, c.PackageName, c.Language FROM c WHERE ({' OR '.join(clauses)})"
        review_results = list(
            reviews_container.query_items(query=query, parameters=params, enable_cross_partition_query=True)
        )

    if revision_ids:
        revisions_container = get_apiview_cosmos_client(
            container_name="APIRevisions", environment=PRODUCTION_ENVIRONMENT
        )
        rev_params = [{"name": f"@rev_id_{i}", "value": rev_id} for i, rev_id in enumerate(revision_ids)]
        rev_clauses = [f"c.id = @rev_id_{i}" for i in range(len(revision_ids))]
        rev_query = (
            "SELECT c.id, c.ReviewId, c.packageVersion, c.ChangeHistory, c.HasAutoGeneratedComments "
            f"FROM c WHERE ({' OR '.join(rev_clauses)})"
        )
        revision_results = list(
            revisions_container.query_items(
                query=rev_query, parameters=rev_params, enable_cross_partition_query=True
            )
        )

    # --- Partition per month locally ---
    for start_date, end_date in month_ranges:
        reviews, month_comments = _build_month_metadata(
            start_date, end_date, all_raw_comments, review_results, revision_results
        )
        if OMIT_LANGUAGES:
            omit_lower = {lang.lower() for lang in OMIT_LANGUAGES}
            reviews = [r for r in reviews if r.language.lower() not in omit_lower]

        for language in selected_languages:
            reports[language].append(
                asdict(
                    _build_language_comment_bucket_point(
                        language,
                        start_date,
                        end_date,
                        reviews,
                        month_comments,
                        include_human=include_human,
                        include_neutral=include_neutral,
                    )
                )
            )

    return reports


def _build_month_metadata(
    start_date: date,
    end_date: date,
    all_raw_comments: list[dict],
    review_results: list[dict],
    revision_results: list[dict],
) -> tuple[list[ActiveReviewMetadata], list[dict]]:
    """Partition pre-fetched data into one month's ActiveReviewMetadata + raw comments.

    Mirrors the logic in ``get_active_reviews`` but operates on already-fetched data
    instead of issuing new Cosmos queries.
    """
    start_iso = to_iso8601(start_date.isoformat())
    end_iso = to_iso8601(end_date.isoformat(), end_of_day=True)

    # Filter comments to this month
    month_comments = [c for c in all_raw_comments if start_iso <= (c.get("CreatedOn") or "") <= end_iso]
    non_diag = [c for c in month_comments if c.get("CommentSource") != "Diagnostic"]

    review_to_revisions: dict[str, set[str]] = {}
    active_revision_ids: set[str] = set()
    for comment in non_diag:
        rid = comment.get("ReviewId")
        rev_id = comment.get("APIRevisionId")
        if rid and rev_id:
            active_revision_ids.add(rev_id)
            review_to_revisions.setdefault(rid, set()).add(rev_id)

    active_review_ids = set(review_to_revisions.keys())

    # Build revision map with month-scoped approval windowing
    revision_map: dict[str, dict] = {}
    for rev in revision_results:
        if rev["id"] not in active_revision_ids:
            continue
        approval = None
        change_history = rev.get("ChangeHistory", [])
        if change_history and isinstance(change_history, list):
            for change in sorted(change_history, key=lambda x: x.get("ChangedOn", ""), reverse=True):
                if change.get("ChangeAction") == "Approved":
                    changed_on = change.get("ChangedOn")
                    if changed_on and start_iso <= changed_on <= end_iso:
                        approval = changed_on
                        break
        pkg_version = rev.get("packageVersion")
        revision_map[rev["id"]] = {
            "review_id": rev.get("ReviewId"),
            "package_version": pkg_version,
            "approval": approval,
            "has_auto_generated_comments": rev.get("HasAutoGeneratedComments", False),
            "version_type": get_version_type(pkg_version),
        }

    metadata: list[ActiveReviewMetadata] = []
    for result in review_results:
        review_id = result["id"]
        if review_id not in active_review_ids:
            continue
        review_name = result.get("PackageName")
        language = get_language_pretty_name(result.get("Language", "Unknown"))
        if language == "Java" and review_name and review_name.startswith("com.azure.android:"):
            language = "Android"

        active_revisions: list[ActiveRevisionMetadata] = []
        if review_id in review_to_revisions:
            version_to_revisions: dict[str, list[str]] = {}
            for rev_id in review_to_revisions[review_id]:
                if rev_id in revision_map:
                    pkg = revision_map[rev_id]["package_version"]
                    version_to_revisions.setdefault(pkg, []).append(rev_id)

            for pkg_version, rev_ids in version_to_revisions.items():
                approvals = [revision_map[r].get("approval") for r in rev_ids if r in revision_map]
                most_recent = max((a for a in approvals if a is not None), default=None)
                has_copilot = any(
                    revision_map[r].get("has_auto_generated_comments", False)
                    for r in rev_ids
                    if r in revision_map
                )
                active_revisions.append(
                    ActiveRevisionMetadata(
                        revision_ids=rev_ids,
                        package_version=pkg_version,
                        approval=most_recent,
                        has_copilot_review=has_copilot,
                        version_type=get_version_type(pkg_version),
                    )
                )

        metadata.append(
            ActiveReviewMetadata(review_id=review_id, name=review_name, language=language, revisions=active_revisions)
        )

    return metadata, month_comments


def _build_chart_rows(report: list[dict], *, include_human: bool, include_neutral: bool) -> list[dict]:
    """Return stacked chart rows in display order for one language report."""
    rows = [
        {"key": "good_percentage", "label": "Confirmed Good", "color": "darkgreen"},
        {"key": "implicit_good_percentage", "label": "Implicit Good", "color": "lightgreen"},
    ]

    if include_neutral:
        rows.append({"key": "neutral_percentage", "label": "Neutral", "color": "lightgray"})

    if include_human:
        rows.append({"key": "human_percentage", "label": "Human Comment", "color": "lightblue"})

    rows.extend(
        [
            {"key": "implicit_bad_percentage", "label": "Implicit Bad", "color": "lightcoral"},
            {"key": "bad_percentage", "label": "Confirmed Bad", "color": "red"},
            {"key": "deleted_percentage", "label": "Deleted", "color": "darkred"},
        ]
    )

    for row in rows:
        row["values"] = [item[row["key"]] for item in report]

    return rows


def generate_chart(
    reports: dict[str, list[dict]],
    output_path: Path = DEFAULT_OUTPUT_PATH,
    *,
    include_human: bool = False,
    include_neutral: bool = False,
) -> Path:
    """Render one PNG containing a bucket subplot per language."""
    try:
        import matplotlib.pyplot as plt
    except ImportError as exc:
        raise RuntimeError("matplotlib is required to render the chart") from exc

    output_path.parent.mkdir(parents=True, exist_ok=True)

    languages = list(reports.keys())
    month_count = len(next(iter(reports.values()), [])) if reports else 0
    cols = 2 if len(languages) > 1 else 1
    rows = max(1, math.ceil(len(languages) / cols))

    figure, axes = plt.subplots(rows, cols, figsize=(8 * cols, 5 * rows), sharey=True)
    if not isinstance(axes, (list, tuple)):
        try:
            axes = axes.flatten()
        except AttributeError:
            axes = [axes]
    else:
        axes = list(axes)

    legend_handles = None
    legend_labels = None

    for index, language in enumerate(languages):
        axis = axes[index]
        report = reports[language]
        labels = [item["label"] for item in report]
        x_positions = list(range(len(labels)))
        bottom = [0.0] * len(labels)

        for row in _build_chart_rows(report, include_human=include_human, include_neutral=include_neutral):
            axis.bar(x_positions, row["values"], bottom=bottom, color=row["color"], label=row["label"])
            bottom = [current + value for current, value in zip(bottom, row["values"])]

        boundary = [item["positive_boundary_percentage"] for item in report]
        included_counts = [item["total_included_comment_count"] for item in report]
        axis.plot(x_positions, boundary, marker="o", linewidth=2.0, color="navy", label="Top of Implicit Good")
        axis.set_title(language)
        axis.set_xticks(x_positions, labels, rotation=45, ha="right")
        axis.set_ylim(0, 105)
        axis.grid(True, axis="y", linestyle="--", alpha=0.4)

        for point_index, boundary_value in enumerate(boundary):
            axis.annotate(
                f"{boundary_value:.1f}%",
                (x_positions[point_index], boundary_value),
                textcoords="offset points",
                xytext=(0, 6),
                ha="center",
                fontsize=7,
            )
            axis.annotate(
                f"N={included_counts[point_index]}",
                (x_positions[point_index], 2),
                textcoords="offset points",
                xytext=(0, 0),
                ha="center",
                fontsize=6,
            )

        if legend_handles is None:
            legend_handles, legend_labels = axis.get_legend_handles_labels()

    for index in range(len(languages), len(axes)):
        figure.delaxes(axes[index])

    if legend_handles and legend_labels:
        figure.legend(
            legend_handles,
            legend_labels,
            loc="upper center",
            bbox_to_anchor=(0.5, 0.935),
            ncol=min(4, len(legend_labels)),
            frameon=False,
        )

    title_suffix_parts = []
    if include_neutral:
        title_suffix_parts.append("neutral")
    if include_human:
        title_suffix_parts.append("human")
    title_suffix = f" + {' + '.join(title_suffix_parts)}" if title_suffix_parts else ""

    figure.suptitle(
        f"Comment Buckets by Language{title_suffix}\nLast {month_count} Calendar Months (APIView Production)",
        fontsize=14,
        y=0.985,
    )
    figure.supxlabel("Month")
    figure.supylabel("Percent of Included Comments")
    plt.tight_layout(rect=(0.02, 0.03, 1, 0.80))
    figure.savefig(output_path, dpi=150)
    plt.close(figure)
    return output_path


def _print_report(
    reports: dict[str, list[dict]],
    output_path: Path,
    *,
    include_human: bool = False,
    include_neutral: bool = False,
) -> None:
    """Print a compact summary for terminal use."""
    print("Comment bucket percentages by month (APIView production)")
    print(f"Include human: {include_human} | Include neutral: {include_neutral}")

    for language, report in reports.items():
        print(f"\n{language}")
        header = ["Month", "Good", "Impl+", "Impl-", "Bad", "Del", "Boundary"]
        if include_neutral:
            header.insert(3, "Neutral")
        if include_human:
            human_index = 4 if include_neutral else 3
            header.insert(human_index, "Human")
        print("  ".join(f"{column:>8}" for column in header))
        print("  ".join(["--------"] * len(header)))

        for item in report:
            values = [
                f"{item['label']:>8}",
                f"{item['good_percentage']:>8.1f}",
                f"{item['implicit_good_percentage']:>8.1f}",
            ]
            if include_neutral:
                values.append(f"{item['neutral_percentage']:>8.1f}")
            if include_human:
                values.append(f"{item['human_percentage']:>8.1f}")
            values.extend(
                [
                    f"{item['implicit_bad_percentage']:>8.1f}",
                    f"{item['bad_percentage']:>8.1f}",
                    f"{item['deleted_percentage']:>8.1f}",
                    f"{item['positive_boundary_percentage']:>8.1f}",
                ]
            )
            print("  ".join(values))

    print(f"\nSaved chart: {output_path}")


def main() -> None:
    """Generate the multi-language comment bucket trends using production APIView data."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--months",
        type=int,
        default=DEFAULT_MONTHS,
        help="Number of calendar months to include. Defaults to 6.",
    )
    parser.add_argument(
        "--languages",
        nargs="+",
        default=DEFAULT_LANGUAGES,
        help="Languages to include. Defaults to C#, Java, JavaScript, and Python.",
    )
    parser.add_argument(
        "--human",
        action="store_true",
        help="Include human comments from Copilot-enabled approved revisions as a light-blue bucket.",
    )
    parser.add_argument(
        "--neutral",
        action="store_true",
        help="Include neutral AI comments as a light-gray bucket.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=DEFAULT_OUTPUT_PATH,
        help="Output PNG path. Defaults to output/charts/comment_bucket_trends.png.",
    )
    args = parser.parse_args()

    reports = build_language_comment_bucket_reports(
        languages=args.languages,
        months=args.months,
        include_human=args.human,
        include_neutral=args.neutral,
    )
    output_path = generate_chart(
        reports,
        output_path=args.output,
        include_human=args.human,
        include_neutral=args.neutral,
    )
    _print_report(reports, output_path, include_human=args.human, include_neutral=args.neutral)


if __name__ == "__main__":
    main()
