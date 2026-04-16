# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Generate multi-language AI quality bucket trend charts from production APIView data."""

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
from src._metrics import METRICS_COMMENT_FIELDS, _build_metrics_segment

PRODUCTION_ENVIRONMENT = "production"
DEFAULT_LANGUAGES = ["C#", "Java", "JavaScript", "Python"]
DEFAULT_MONTHS = 6
DEFAULT_OUTPUT_PATH = Path("output/charts/quality_bucket_trends.png")
OMIT_LANGUAGES = ["c++", "c", "typespec", "swagger", "xml"]


@dataclass
class MonthlyQualityPoint:
    """Represents one monthly quality-bucket point for a language."""

    label: str
    start_date: str
    end_date: str
    ai_comment_count: int
    good_percentage: float
    implicit_good_percentage: float
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


def _build_language_quality_point(
    language: str,
    start_date: date,
    end_date: date,
    reviews: list[object],
    raw_comments: list[dict],
) -> MonthlyQualityPoint:
    """Build one monthly quality point using the current metrics.py bucket logic."""
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
    implicit_bad_count = segment.implicit_bad_ai_comment_count or 0
    bad_count = segment.downvoted_ai_comment_count or 0
    deleted_count = segment.deleted_ai_comment_count or 0

    total_excluding_neutral = good_count + implicit_good_count + implicit_bad_count + bad_count + deleted_count

    good_percentage = _to_percentage(good_count, total_excluding_neutral)
    implicit_good_percentage = _to_percentage(implicit_good_count, total_excluding_neutral)
    implicit_bad_percentage = _to_percentage(implicit_bad_count, total_excluding_neutral)
    bad_percentage = _to_percentage(bad_count, total_excluding_neutral)
    deleted_percentage = _to_percentage(deleted_count, total_excluding_neutral)
    positive_boundary_percentage = round(good_percentage + implicit_good_percentage, 2)

    return MonthlyQualityPoint(
        label=start_date.strftime("%Y-%m"),
        start_date=start_date.isoformat(),
        end_date=end_date.isoformat(),
        ai_comment_count=segment.total_ai_comment_count or 0,
        good_percentage=good_percentage,
        implicit_good_percentage=implicit_good_percentage,
        implicit_bad_percentage=implicit_bad_percentage,
        bad_percentage=bad_percentage,
        deleted_percentage=deleted_percentage,
        positive_boundary_percentage=positive_boundary_percentage,
    )


def build_language_quality_reports(
    languages: Optional[list[str]] = None,
    months: int = DEFAULT_MONTHS,
    today: Optional[date] = None,
) -> dict[str, list[dict]]:
    """Build per-language quality-bucket reports for the last N discrete calendar months."""
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
                asdict(_build_language_quality_point(language, start_date, end_date, reviews, raw_comments))
            )

    return reports


def generate_chart(reports: dict[str, list[dict]], output_path: Path = DEFAULT_OUTPUT_PATH) -> Path:
    """Render one PNG containing a quality-bucket subplot per language."""
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

    bucket_specs = [
        ("good_percentage", "darkgreen", "Good"),
        ("implicit_good_percentage", "lightgreen", "Implicit Good"),
        ("implicit_bad_percentage", "lightcoral", "Implicit Bad"),
        ("bad_percentage", "red", "Bad"),
        ("deleted_percentage", "darkred", "Deleted"),
    ]

    for index, language in enumerate(languages):
        axis = axes[index]
        report = reports[language]
        labels = [item["label"] for item in report]
        x_positions = list(range(len(labels)))
        bottom = [0.0] * len(labels)

        for key, color, label in bucket_specs:
            values = [item[key] for item in report]
            axis.bar(x_positions, values, bottom=bottom, color=color, label=label)
            bottom = [current + value for current, value in zip(bottom, values)]

        boundary = [item["positive_boundary_percentage"] for item in report]
        ai_counts = [item["ai_comment_count"] for item in report]
        axis.plot(x_positions, boundary, marker="o", linewidth=2.0, color="navy", label="Good/Bad Boundary")
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
                f"AI={ai_counts[point_index]}",
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
        figure.legend(legend_handles, legend_labels, loc="upper center", ncol=6, frameon=False)

    figure.suptitle(
        f"AI Quality Buckets by Language\nLast {month_count} Calendar Months (APIView Production)",
        fontsize=14,
    )
    figure.supxlabel("Month")
    figure.supylabel("Percent of Non-Neutral AI Comments")
    plt.tight_layout(rect=(0.02, 0.03, 1, 0.92))
    figure.savefig(output_path, dpi=150)
    plt.close(figure)
    return output_path


def _print_report(reports: dict[str, list[dict]], output_path: Path) -> None:
    """Print a compact summary for terminal use."""
    print("AI quality bucket percentages by month (APIView production)")
    for language, report in reports.items():
        print(f"\n{language}")
        print("Month     Good  Impl+  Impl-  Bad  Del  Boundary")
        print("--------  ----  -----  -----  ---  ---  --------")
        for item in report:
            print(
                f"{item['label']:8}  "
                f"{item['good_percentage']:4.1f}  "
                f"{item['implicit_good_percentage']:5.1f}  "
                f"{item['implicit_bad_percentage']:5.1f}  "
                f"{item['bad_percentage']:3.1f}  "
                f"{item['deleted_percentage']:3.1f}  "
                f"{item['positive_boundary_percentage']:8.1f}"
            )
    print(f"\nSaved chart: {output_path}")


def main() -> None:
    """Generate the multi-language quality-bucket trend using production APIView data."""
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
        help="Output PNG path. Defaults to output/charts/quality_bucket_trends.png.",
    )
    args = parser.parse_args()

    reports = build_language_quality_reports(languages=args.languages, months=args.months)
    output_path = generate_chart(reports, output_path=args.output)
    _print_report(reports, output_path)


if __name__ == "__main__":
    main()
