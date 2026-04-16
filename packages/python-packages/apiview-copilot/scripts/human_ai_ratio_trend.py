# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Generate a multi-language human/AI comment trend chart from production APIView data."""

from __future__ import annotations

import argparse
import calendar
import sys
from dataclasses import asdict, dataclass
from datetime import date
from pathlib import Path
from typing import Optional

REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src._apiview import get_active_reviews
from src._metrics import METRICS_COMMENT_FIELDS

PRODUCTION_ENVIRONMENT = "production"
DEFAULT_LANGUAGES = ["C#", "Java", "JavaScript", "Python"]
DEFAULT_MONTHS = 6
DEFAULT_OUTPUT_PATH = Path("output/charts/human_ai_ratio_trends.png")
OMIT_LANGUAGES = ["c++", "c", "typespec", "swagger", "xml"]


@dataclass
class MonthlyRatioPoint:
    """Represents one monthly data point for the Python human/AI ratio trend."""

    label: str
    start_date: str
    end_date: str
    active_review_count: int
    copilot_review_count: int
    human_comment_count: int
    ai_comment_count: int
    total_comment_count: int
    human_ai_ratio: float
    ai_comment_percentage: float
    human_comment_percentage: float


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


def _calculate_ratio(numerator: int, denominator: int) -> float:
    """Calculate a ratio safely, returning 0.0 when the denominator is zero."""
    if denominator == 0:
        return 0.0
    return numerator / denominator


def _calculate_percentage(count: int, total: int) -> float:
    """Calculate a percentage safely, returning 0.0 when the total is zero."""
    if total == 0:
        return 0.0
    return round((count / total) * 100, 2)


def _build_language_month_point(
    language: str,
    start_date: date,
    end_date: date,
    reviews: list[object],
    raw_comments: list[dict],
) -> MonthlyRatioPoint:
    """Build one monthly point using the same active-review and comment-makeup logic as metrics."""
    language_reviews = [review for review in reviews if review.language.lower() == language.lower()]
    all_revisions = [revision for review in language_reviews for revision in review.revisions]
    approved_revisions = [revision for revision in all_revisions if revision.approval is not None]

    all_revision_ids: set[str] = set()
    approved_revision_ids: set[str] = set()
    revision_has_copilot: dict[str, bool] = {}

    for revision in all_revisions:
        for revision_id in revision.revision_ids:
            all_revision_ids.add(revision_id)
            revision_has_copilot[revision_id] = revision.has_copilot_review
            if revision.approval is not None:
                approved_revision_ids.add(revision_id)

    approved_revision_comments = [
        comment
        for comment in raw_comments
        if comment.get("APIRevisionId") in approved_revision_ids and comment.get("CommentSource") != "Diagnostic"
    ]

    human_comment_count = sum(
        1
        for comment in approved_revision_comments
        if revision_has_copilot.get(comment.get("APIRevisionId"), False)
        and comment.get("CommentSource") != "AIGenerated"
    )

    all_ai_comments = [
        comment
        for comment in raw_comments
        if comment.get("APIRevisionId") in all_revision_ids and comment.get("CommentSource") == "AIGenerated"
    ]

    ai_comment_count = 0
    for comment in all_ai_comments:
        if comment.get("IsDeleted"):
            continue
        if comment.get("Downvotes"):
            ai_comment_count += 1
        elif comment.get("Upvotes"):
            ai_comment_count += 1
        elif comment.get("IsResolved"):
            ai_comment_count += 1
        elif comment.get("APIRevisionId") in approved_revision_ids:
            ai_comment_count += 1
        # Neutral comments from unapproved revisions are intentionally excluded,
        # matching the metrics comment_makeup behavior.

    total_comment_count = human_comment_count + ai_comment_count
    ai_comment_percentage = _calculate_percentage(ai_comment_count, total_comment_count)
    human_comment_percentage = round(100.0 - ai_comment_percentage, 2) if total_comment_count else 0.0

    return MonthlyRatioPoint(
        label=start_date.strftime("%Y-%m"),
        start_date=start_date.isoformat(),
        end_date=end_date.isoformat(),
        active_review_count=len(approved_revisions),
        copilot_review_count=sum(1 for revision in approved_revisions if revision.has_copilot_review),
        human_comment_count=human_comment_count,
        ai_comment_count=ai_comment_count,
        total_comment_count=total_comment_count,
        human_ai_ratio=round(_calculate_ratio(human_comment_count, ai_comment_count), 4),
        ai_comment_percentage=ai_comment_percentage,
        human_comment_percentage=human_comment_percentage,
    )


def build_monthly_ratio_report(
    language: str = "Python",
    months: int = DEFAULT_MONTHS,
    today: Optional[date] = None,
) -> list[dict]:
    """Build the human/AI ratio report for one language over the last N discrete calendar months."""
    return build_language_reports(languages=[language], months=months, today=today)[language]


def build_language_reports(
    languages: Optional[list[str]] = None,
    months: int = DEFAULT_MONTHS,
    today: Optional[date] = None,
) -> dict[str, list[dict]]:
    """Build per-language human/AI ratio reports for the last N discrete calendar months."""
    selected_languages = languages or DEFAULT_LANGUAGES
    reports = {language: [] for language in selected_languages}

    for start_date, end_date in get_last_n_month_ranges(months=months, today=today):
        reviews, raw_comments = get_active_reviews(
            start_date.isoformat(),
            end_date.isoformat(),
            environment=PRODUCTION_ENVIRONMENT,
            omit_languages=OMIT_LANGUAGES,
            select_fields=METRICS_COMMENT_FIELDS,
        )
        for language in selected_languages:
            reports[language].append(
                asdict(_build_language_month_point(language, start_date, end_date, reviews, raw_comments))
            )

    return reports


def generate_chart(reports: dict[str, list[dict]], output_path: Path = DEFAULT_OUTPUT_PATH) -> Path:
    """Render one PNG containing one subplot per language."""
    try:
        import matplotlib.pyplot as plt
    except ImportError as exc:
        raise RuntimeError("matplotlib is required to render the chart") from exc

    output_path.parent.mkdir(parents=True, exist_ok=True)

    languages = list(reports.keys())
    month_count = len(next(iter(reports.values()), [])) if reports else 0

    figure, axes = plt.subplots(2, 2, figsize=(16, 10), sharey=True)
    axes = axes.flatten()
    legend_handles = None
    legend_labels = None

    for index, language in enumerate(languages):
        axis = axes[index]
        report = reports[language]
        labels = [item["label"] for item in report]
        ai_percentages = [item["ai_comment_percentage"] for item in report]
        human_percentages = [item["human_comment_percentage"] for item in report]
        human_counts = [item["human_comment_count"] for item in report]
        ai_counts = [item["ai_comment_count"] for item in report]
        x_positions = list(range(len(labels)))

        axis.bar(x_positions, ai_percentages, color="steelblue", label="AI %")
        axis.bar(x_positions, human_percentages, bottom=ai_percentages, color="lightgreen", label="Human %")
        axis.plot(x_positions, ai_percentages, marker="o", linewidth=2.0, color="navy", label="AI % Trend")
        axis.set_title(language)
        axis.set_xticks(x_positions, labels, rotation=45, ha="right")
        axis.set_ylim(0, 105)
        axis.grid(True, axis="y", linestyle="--", alpha=0.4)

        for point_index, ai_percentage in enumerate(ai_percentages):
            axis.annotate(
                f"{ai_percentage:.1f}%",
                (x_positions[point_index], ai_percentage),
                textcoords="offset points",
                xytext=(0, 6),
                ha="center",
                fontsize=7,
            )
            axis.annotate(
                f"H={human_counts[point_index]} A={ai_counts[point_index]}",
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
        figure.legend(legend_handles, legend_labels, loc="upper center", ncol=3, frameon=False)

    figure.suptitle(
        f"AI Share of Total Comments by Language\nLast {month_count} Calendar Months (APIView Production)",
        fontsize=14,
    )
    figure.supxlabel("Month")
    figure.supylabel("Percent of Total Comments")
    plt.tight_layout(rect=(0.02, 0.03, 1, 0.92))
    figure.savefig(output_path, dpi=150)
    plt.close(figure)
    return output_path


def _print_report(reports: dict[str, list[dict]], output_path: Path) -> None:
    """Print a compact monthly summary for terminal use."""
    print("AI percentage by month (APIView production)")
    for language, report in reports.items():
        print(f"\n{language}")
        print("Month     Reviews  Copilot  Human  AI   AI %   Human %")
        print("--------  -------  -------  -----  ---  -----  -------")
        for item in report:
            print(
                f"{item['label']:8}  "
                f"{item['active_review_count']:7}  "
                f"{item['copilot_review_count']:7}  "
                f"{item['human_comment_count']:5}  "
                f"{item['ai_comment_count']:3}  "
                f"{item['ai_comment_percentage']:5.1f}  "
                f"{item['human_comment_percentage']:7.1f}"
            )
    print(f"\nSaved chart: {output_path}")


def main() -> None:
    """Generate the multi-language human/AI trend using production APIView data."""
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
        "--output",
        type=Path,
        default=DEFAULT_OUTPUT_PATH,
        help="Output PNG path. Defaults to output/charts/human_ai_ratio_trends.png.",
    )
    args = parser.parse_args()

    reports = build_language_reports(languages=args.languages, months=args.months)
    output_path = generate_chart(reports, output_path=args.output)
    _print_report(reports, output_path)


if __name__ == "__main__":
    main()
