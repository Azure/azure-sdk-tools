# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Generate multi-language comment bucket trend charts from APIView data."""

from __future__ import annotations

import argparse
import calendar
import math
from dataclasses import asdict, dataclass
from datetime import date
from pathlib import Path
from typing import Optional

from src._apiview import (
    ActiveReviewMetadata,
    ActiveRevisionMetadata,
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
QUERY_BATCH_SIZE = 100
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


def get_last_n_month_ranges(
    months: int = DEFAULT_MONTHS,
    end_date: Optional[date] = None,
) -> list[tuple[date, date]]:
    """Return calendar-month ranges ending on the supplied end date.

    The month containing the end date counts as one of the requested months,
    even when it is only a partial month.
    """
    if months < 1:
        raise ValueError("months must be at least 1")

    end_date = end_date or date.today()
    end_month_index = end_date.year * 12 + (end_date.month - 1)
    ranges: list[tuple[date, date]] = []

    for month_index in range(end_month_index - months + 1, end_month_index + 1):
        year = month_index // 12
        month = (month_index % 12) + 1
        last_day = calendar.monthrange(year, month)[1]
        month_start = date(year, month, 1)
        month_end = end_date if month_index == end_month_index else date(year, month, last_day)
        ranges.append((month_start, month_end))

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
    """Build one monthly bucket point using current metrics logic plus optional buckets."""
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
        good_count + implicit_good_count + neutral_count + human_count + implicit_bad_count + bad_count + deleted_count
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
    end_date: Optional[date] = None,
    *,
    include_human: bool = False,
    include_neutral: bool = False,
    environment: str = PRODUCTION_ENVIRONMENT,
) -> dict[str, list[dict]]:
    """Build per-language bucket reports for the requested month lookback window."""
    selected_languages = languages or DEFAULT_LANGUAGES
    reports = {language: [] for language in selected_languages}
    month_ranges = get_last_n_month_ranges(months=months, end_date=end_date)
    if not month_ranges:
        return reports

    full_start = month_ranges[0][0]
    full_end = month_ranges[-1][1]

    select_fields = list(METRICS_COMMENT_FIELDS)
    if "CreatedOn" not in select_fields:
        select_fields.append("CreatedOn")

    non_diag = get_comments_in_date_range(
        full_start.isoformat(),
        full_end.isoformat(),
        environment=environment,
        select_fields=select_fields,
        include_deleted=True,
    )

    review_ids: set[str] = set()
    revision_ids: set[str] = set()
    for comment in non_diag:
        review_id = comment.get("ReviewId")
        revision_id = comment.get("APIRevisionId")
        if review_id:
            review_ids.add(review_id)
        if revision_id:
            revision_ids.add(revision_id)

    review_results: list[dict] = []
    revision_results: list[dict] = []

    if review_ids:
        reviews_container = get_apiview_cosmos_client(container_name="Reviews", environment=environment)
        review_results = _query_items_by_id_batches(
            reviews_container,
            review_ids,
            "c.id, c.PackageName, c.Language",
            id_parameter_prefix="id",
        )

    if revision_ids:
        revisions_container = get_apiview_cosmos_client(
            container_name="APIRevisions",
            environment=environment,
        )
        revision_results = _query_items_by_id_batches(
            revisions_container,
            revision_ids,
            "c.id, c.ReviewId, c.packageVersion, c.ChangeHistory, c.HasAutoGeneratedComments",
            id_parameter_prefix="rev_id",
        )

    for start_date, end_date in month_ranges:
        reviews, month_comments = _build_month_metadata(
            start_date,
            end_date,
            non_diag,
            review_results,
            revision_results,
        )
        if OMIT_LANGUAGES:
            omit_lower = {language.lower() for language in OMIT_LANGUAGES}
            reviews = [review for review in reviews if review.language.lower() not in omit_lower]

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


def _query_items_by_id_batches(
    container: object,
    item_ids: set[str],
    select_clause: str,
    *,
    id_parameter_prefix: str,
    batch_size: int = QUERY_BATCH_SIZE,
) -> list[dict]:
    """Query Cosmos items by id using manageable batches."""
    results: list[dict] = []
    ordered_ids = sorted(item_ids)

    for start_index in range(0, len(ordered_ids), batch_size):
        id_batch = ordered_ids[start_index : start_index + batch_size]
        params = [
            {"name": f"@{id_parameter_prefix}_{index}", "value": item_id}
            for index, item_id in enumerate(id_batch)
        ]
        clauses = [f"c.id = @{id_parameter_prefix}_{index}" for index in range(len(id_batch))]
        query = f"SELECT {select_clause} FROM c WHERE ({' OR '.join(clauses)})"
        results.extend(
            list(
                container.query_items(
                    query=query,
                    parameters=params,
                    enable_cross_partition_query=True,
                )
            )
        )

    return results


def _build_month_metadata(
    start_date: date,
    end_date: date,
    all_raw_comments: list[dict],
    review_results: list[dict],
    revision_results: list[dict],
) -> tuple[list[ActiveReviewMetadata], list[dict]]:
    """Partition pre-fetched data into one month's metadata and raw comments."""
    start_iso = to_iso8601(start_date.isoformat())
    end_iso = to_iso8601(end_date.isoformat(), end_of_day=True)

    month_comments = [
        comment for comment in all_raw_comments if start_iso <= (comment.get("CreatedOn") or "") <= end_iso
    ]
    non_diag = [comment for comment in month_comments if comment.get("CommentSource") != "Diagnostic"]

    review_to_revisions: dict[str, set[str]] = {}
    active_revision_ids: set[str] = set()
    for comment in non_diag:
        review_id = comment.get("ReviewId")
        revision_id = comment.get("APIRevisionId")
        if review_id and revision_id:
            active_revision_ids.add(revision_id)
            review_to_revisions.setdefault(review_id, set()).add(revision_id)

    active_review_ids = set(review_to_revisions.keys())

    revision_map: dict[str, dict] = {}
    for revision in revision_results:
        if revision["id"] not in active_revision_ids:
            continue
        approval = None
        change_history = revision.get("ChangeHistory", [])
        if change_history and isinstance(change_history, list):
            for change in sorted(change_history, key=lambda item: item.get("ChangedOn", ""), reverse=True):
                if change.get("ChangeAction") == "Approved":
                    changed_on = change.get("ChangedOn")
                    if changed_on and start_iso <= changed_on <= end_iso:
                        approval = changed_on
                        break
        package_version = revision.get("packageVersion")
        revision_map[revision["id"]] = {
            "review_id": revision.get("ReviewId"),
            "package_version": package_version,
            "approval": approval,
            "has_auto_generated_comments": revision.get("HasAutoGeneratedComments", False),
            "version_type": get_version_type(package_version),
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
            for revision_id in review_to_revisions[review_id]:
                if revision_id in revision_map:
                    package_version = revision_map[revision_id]["package_version"]
                    version_to_revisions.setdefault(package_version, []).append(revision_id)

            for package_version, revision_ids in version_to_revisions.items():
                approvals = [
                    revision_map[revision_id].get("approval")
                    for revision_id in revision_ids
                    if revision_id in revision_map
                ]
                most_recent = max((approval for approval in approvals if approval is not None), default=None)
                has_copilot = any(
                    revision_map[revision_id].get("has_auto_generated_comments", False)
                    for revision_id in revision_ids
                    if revision_id in revision_map
                )
                active_revisions.append(
                    ActiveRevisionMetadata(
                        revision_ids=revision_ids,
                        package_version=package_version,
                        approval=most_recent,
                        has_copilot_review=has_copilot,
                        version_type=get_version_type(package_version),
                    )
                )

        metadata.append(
            ActiveReviewMetadata(
                review_id=review_id,
                name=review_name,
                language=language,
                revisions=active_revisions,
            )
        )

    return metadata, month_comments


def _build_chart_rows(
    report: list[dict],
    *,
    include_human: bool,
    include_neutral: bool,
    raw: bool = False,
) -> list[dict]:
    """Return stacked chart rows in display order for one language report."""
    if raw:
        rows = [
            {"key": "good_count", "label": "Confirmed Good", "color": "darkgreen"},
            {"key": "implicit_good_count", "label": "Implicit Good", "color": "lightgreen"},
        ]
        if include_neutral:
            rows.append({"key": "neutral_count", "label": "Neutral", "color": "lightgray"})
        if include_human:
            rows.append({"key": "human_comment_count", "label": "Human Comment", "color": "lightblue"})
        rows.extend(
            [
                {"key": "implicit_bad_count", "label": "Implicit Bad", "color": "lightcoral"},
                {"key": "bad_count", "label": "Confirmed Bad", "color": "red"},
                {"key": "deleted_count", "label": "Deleted", "color": "darkred"},
            ]
        )
    else:
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


def generate_comment_bucket_chart(
    reports: dict[str, list[dict]],
    output_path: Path = DEFAULT_OUTPUT_PATH,
    *,
    include_human: bool = False,
    include_neutral: bool = False,
    raw: bool = False,
    environment: str = PRODUCTION_ENVIRONMENT,
) -> Optional[Path]:
    """Render one PNG containing a bucket subplot per language."""
    output_path.parent.mkdir(parents=True, exist_ok=True)

    try:
        import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib is not installed; skipping comment bucket chart generation.")
        return None

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

        for row in _build_chart_rows(report, include_human=include_human, include_neutral=include_neutral, raw=raw):
            axis.bar(x_positions, row["values"], bottom=bottom, color=row["color"], label=row["label"])
            bottom = [current + value for current, value in zip(bottom, row["values"])]

        included_counts = [item["total_included_comment_count"] for item in report]
        if raw:
            axis.set_title(language)
            axis.set_xticks(x_positions, labels, rotation=45, ha="right")
            axis.grid(True, axis="y", linestyle="--", alpha=0.4)
            for point_index in range(len(labels)):
                axis.annotate(
                    f"N={included_counts[point_index]}",
                    (x_positions[point_index], bottom[point_index]),
                    textcoords="offset points",
                    xytext=(0, 4),
                    ha="center",
                    fontsize=7,
                )
        else:
            boundary = [item["positive_boundary_percentage"] for item in report]
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
    format_label = "Raw Counts" if raw else "Normalized %"

    environment_label = (environment or PRODUCTION_ENVIRONMENT).strip().lower()

    figure.suptitle(
        f"Comment Buckets by Language{title_suffix} ({format_label})\nLast {month_count} Calendar Months (APIView {environment_label})",
        fontsize=14,
        y=0.985,
    )
    figure.supxlabel("Month")
    figure.supylabel("Comment Count" if raw else "Percent of Included Comments")
    plt.tight_layout(rect=(0.02, 0.03, 1, 0.80))
    figure.savefig(output_path, dpi=150)
    plt.close(figure)
    return output_path


def print_comment_bucket_report(
    reports: dict[str, list[dict]],
    output_path: Optional[Path],
    *,
    include_human: bool = False,
    include_neutral: bool = False,
    environment: str = PRODUCTION_ENVIRONMENT,
) -> None:
    """Print a compact summary for terminal use."""
    environment_label = (environment or PRODUCTION_ENVIRONMENT).strip().lower()
    print(f"Comment bucket percentages by month (APIView {environment_label})")
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

    if output_path and output_path.exists():
        print(f"\nSaved chart: {output_path}")
    else:
        print("\nChart was not generated.")


def main() -> None:
    """Generate the multi-language comment bucket trends using production APIView data."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--months",
        type=int,
        default=DEFAULT_MONTHS,
        help="Number of calendar months to look back from the end date. Defaults to 6.",
    )
    parser.add_argument(
        "--end-date",
        type=date.fromisoformat,
        default=None,
        help="Inclusive query end date in YYYY-MM-DD format. Defaults to today.",
    )
    parser.add_argument(
        "--languages",
        nargs="+",
        default=DEFAULT_LANGUAGES,
        help="Languages to include. Defaults to Python, C#, Java, and JavaScript.",
    )
    parser.add_argument(
        "--exclude-human",
        action="store_true",
        help="Exclude human comments from Copilot-enabled approved revisions.",
    )
    parser.add_argument(
        "--neutral",
        action="store_true",
        help="Include neutral AI comments as a light-gray bucket.",
    )
    parser.add_argument(
        "--format",
        choices=["normalize", "raw"],
        default="normalize",
        dest="chart_format",
        help="Chart format: normalize shows percentage bars; raw shows raw count bars.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=DEFAULT_OUTPUT_PATH,
        help="Output PNG path. Defaults to output/charts/comment_bucket_trends.png.",
    )
    parser.add_argument(
        "--environment",
        type=str,
        default=PRODUCTION_ENVIRONMENT,
        choices=["production", "staging"],
        help="The APIView environment to query. Defaults to production.",
    )
    args = parser.parse_args()
    raw = args.chart_format == "raw"

    include_human = not args.exclude_human

    reports = build_language_comment_bucket_reports(
        languages=args.languages,
        months=args.months,
        end_date=args.end_date,
        include_human=include_human,
        include_neutral=args.neutral,
        environment=args.environment,
    )
    output_path = generate_comment_bucket_chart(
        reports,
        output_path=args.output,
        include_human=include_human,
        include_neutral=args.neutral,
        raw=raw,
        environment=args.environment,
    )
    print_comment_bucket_report(
        reports,
        output_path,
        include_human=include_human,
        include_neutral=args.neutral,
        environment=args.environment,
    )
