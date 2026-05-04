# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Generate APIView platform metrics (versioned-revision tracking, etc.)."""

from __future__ import annotations

import math
from dataclasses import asdict, dataclass
from datetime import date
from pathlib import Path
from typing import Optional

from src._apiview import _KNOWN_REVISION_TYPES, get_apiview_cosmos_client
from src._comment_bucket_trends import get_last_n_month_ranges
from src._utils import get_language_pretty_name, to_iso8601

PRODUCTION_ENVIRONMENT = "production"
DEFAULT_LANGUAGES = ["Python", "C#", "Java", "JavaScript", "Go"]
DEFAULT_MONTHS = 6
DEFAULT_OUTPUT_PATH = Path("output/charts/apiview_version_trends.png")
DEFAULT_COMPLIANCE_OUTPUT_PATH = Path("output/charts/cross_language_compliance.png")
OMIT_LANGUAGES = ["c++", "c", "typespec", "swagger", "xml"]


@dataclass
class RevisionTypeBucket:
    """Version counts for a single revision type within a monthly point."""

    total: int = 0
    versioned: int = 0
    unversioned: int = 0
    versioned_pct: float = 0.0


@dataclass
class MonthlyVersionPoint:
    """Monthly version-coverage point for a single language."""

    label: str
    start_date: str
    end_date: str
    Automatic: RevisionTypeBucket
    Manual: RevisionTypeBucket
    PullRequest: RevisionTypeBucket
    total: int = 0
    versioned: int = 0
    unversioned: int = 0
    versioned_pct: float = 0.0


def _pct(numerator: int, denominator: int) -> float:
    """Convert a count to a percentage safely."""
    if denominator == 0:
        return 0.0
    return round((numerator / denominator) * 100, 2)


def _build_monthly_version_point(
    label: str,
    start_date: date,
    end_date: date,
    revisions: list[dict],
    language: str,
) -> MonthlyVersionPoint:
    """Build a single monthly data point for one language from raw revisions."""
    start_iso = to_iso8601(start_date.isoformat())
    end_iso = to_iso8601(end_date.isoformat(), end_of_day=True)

    buckets: dict[str, RevisionTypeBucket] = {t: RevisionTypeBucket() for t in _KNOWN_REVISION_TYPES}
    total = 0
    versioned = 0

    for rev in revisions:
        lang = get_language_pretty_name(rev.get("Language", "Unknown"))
        if lang != language:
            continue

        created = rev.get("CreatedOn", "")
        if not (start_iso <= created <= end_iso):
            continue

        raw_type = rev.get("APIRevisionType", "Unknown")
        type_name = raw_type if raw_type in _KNOWN_REVISION_TYPES else None
        if type_name is None:
            continue

        has_version = bool(rev.get("packageVersion"))
        bucket = buckets[type_name]
        bucket.total += 1
        if has_version:
            bucket.versioned += 1
        else:
            bucket.unversioned += 1

        total += 1
        if has_version:
            versioned += 1

    for bucket in buckets.values():
        bucket.versioned_pct = _pct(bucket.versioned, bucket.total)

    return MonthlyVersionPoint(
        label=label,
        start_date=start_date.isoformat(),
        end_date=end_date.isoformat(),
        Automatic=buckets["Automatic"],
        Manual=buckets["Manual"],
        PullRequest=buckets["PullRequest"],
        total=total,
        versioned=versioned,
        unversioned=total - versioned,
        versioned_pct=_pct(versioned, total),
    )


def build_version_reports(
    languages: Optional[list[str]] = None,
    months: int = DEFAULT_MONTHS,
    end_date: Optional[date] = None,
    *,
    environment: str = PRODUCTION_ENVIRONMENT,
) -> dict[str, list[dict]]:
    """Build per-language version-coverage reports for the requested month lookback window.

    Returns:
        A dict mapping language name to a list of monthly data-point dicts.
    """
    selected_languages = languages or DEFAULT_LANGUAGES
    month_ranges = get_last_n_month_ranges(months=months, end_date=end_date)
    if not month_ranges:
        return {lang: [] for lang in selected_languages}

    full_start = month_ranges[0][0]
    full_end = month_ranges[-1][1]

    start_iso = to_iso8601(full_start.isoformat())
    end_iso = to_iso8601(full_end.isoformat(), end_of_day=True)

    revisions_container = get_apiview_cosmos_client(container_name="APIRevisions", environment=environment)

    query = (
        "SELECT c.Language, c.APIRevisionType, c.packageVersion, c.CreatedOn "
        "FROM c "
        "WHERE c.CreatedOn >= @start AND c.CreatedOn <= @end"
    )
    params = [
        {"name": "@start", "value": start_iso},
        {"name": "@end", "value": end_iso},
    ]

    all_revisions = list(
        revisions_container.query_items(query=query, parameters=params, enable_cross_partition_query=True)
    )

    # Filter out languages we always omit
    omit_lower = {lang.lower() for lang in OMIT_LANGUAGES}
    all_revisions = [
        rev
        for rev in all_revisions
        if get_language_pretty_name(rev.get("Language", "Unknown")).lower() not in omit_lower
    ]

    reports: dict[str, list[dict]] = {lang: [] for lang in selected_languages}
    for start, end in month_ranges:
        label = f"{start.year}-{start.month:02d}"
        for language in selected_languages:
            point = _build_monthly_version_point(label, start, end, all_revisions, language)
            reports[language].append(asdict(point))

    return reports


def generate_version_chart(
    reports: dict[str, list[dict]],
    output_path: Path = DEFAULT_OUTPUT_PATH,
    *,
    environment: str = PRODUCTION_ENVIRONMENT,
) -> Optional[Path]:
    """Render a PNG chart showing versioned-revision percentage trends per language."""
    output_path.parent.mkdir(parents=True, exist_ok=True)

    try:
        import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib is not installed; skipping chart generation.")
        return None

    languages = list(reports.keys())
    month_count = len(next(iter(reports.values()), [])) if reports else 0
    if month_count == 0:
        return None

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

    type_colors = {"Automatic": "#2196F3", "Manual": "#FF9800", "PullRequest": "#4CAF50"}
    type_names = list(type_colors.keys())
    bar_width = 0.25
    legend_handles = None
    legend_labels = None

    for index, language in enumerate(languages):
        axis = axes[index]
        report = reports[language]
        labels = [item["label"] for item in report]
        x_positions = list(range(len(labels)))

        for bar_index, type_name in enumerate(type_names):
            color = type_colors[type_name]
            offsets = [x + bar_index * bar_width for x in x_positions]
            values = [item[type_name]["versioned_pct"] for item in report]
            _bars = axis.bar(offsets, values, bar_width, color=color, label=type_name)

            # Annotate each bar with count
            for bar_pos, item in zip(offsets, report):
                bucket = item[type_name]
                if bucket["total"] > 0:
                    axis.annotate(
                        f"{bucket['versioned']}/{bucket['total']}",
                        (bar_pos, bucket["versioned_pct"]),
                        textcoords="offset points",
                        xytext=(0, 4),
                        ha="center",
                        fontsize=6,
                    )

        axis.axhline(y=100, color="gray", linestyle=":", linewidth=1.0, alpha=0.5)
        axis.set_title(language)
        # Center x-ticks on the middle bar
        axis.set_xticks([x + bar_width for x in x_positions], labels, rotation=45, ha="right")
        axis.set_ylim(0, 115)
        axis.grid(True, axis="y", linestyle="--", alpha=0.4)

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
            ncol=min(5, len(legend_labels)),
            frameon=False,
        )

    environment_label = (environment or PRODUCTION_ENVIRONMENT).strip().lower()
    figure.suptitle(
        f"Versioned Revision % by Language and Type\nLast {month_count} Calendar Months (APIView {environment_label})",
        fontsize=14,
        y=0.985,
    )
    figure.supxlabel("Month")
    figure.supylabel("% Revisions with PackageVersion")
    plt.tight_layout(rect=(0.02, 0.03, 1, 0.80))
    figure.savefig(output_path, dpi=150)
    plt.close(figure)
    return output_path


def print_version_report(
    reports: dict[str, list[dict]],
    output_path: Optional[Path],
    *,
    environment: str = PRODUCTION_ENVIRONMENT,
    file=None,
) -> None:
    """Print a compact terminal summary of version coverage."""
    environment_label = (environment or PRODUCTION_ENVIRONMENT).strip().lower()
    print(f"Versioned revision % by month (APIView {environment_label})", file=file)

    for language, report in reports.items():
        print(f"\n{language}", file=file)
        header = ["Month", "Auto %", "Auto N", "Manual %", "Manual N", "PR %", "PR N", "Overall %", "Total N"]
        print("  ".join(f"{col:>10}" for col in header), file=file)
        print("  ".join(["----------"] * len(header)), file=file)

        for item in report:
            auto = item["Automatic"]
            manual = item["Manual"]
            pr = item["PullRequest"]
            values = [
                f"{item['label']:>10}",
                f"{auto['versioned_pct']:>10.1f}",
                f"{auto['total']:>10}",
                f"{manual['versioned_pct']:>10.1f}",
                f"{manual['total']:>10}",
                f"{pr['versioned_pct']:>10.1f}",
                f"{pr['total']:>10}",
                f"{item['versioned_pct']:>10.1f}",
                f"{item['total']:>10}",
            ]
            print("  ".join(values), file=file)

    if output_path and output_path.exists():
        print(f"\nSaved chart: {output_path}", file=file)
    else:
        print("\nChart was not generated.", file=file)


# ---------------------------------------------------------------------------
# Cross-language compliance metrics
# ---------------------------------------------------------------------------


@dataclass
class MonthlyCompliancePoint:
    """Monthly cross-language compliance data for a single language."""

    label: str
    start_date: str
    end_date: str
    compliant: int = 0
    non_compliant: int = 0
    total: int = 0
    pct: float = 0.0


def build_compliance_reports(
    languages: Optional[list[str]] = None,
    months: int = DEFAULT_MONTHS,
    end_date: Optional[date] = None,
    *,
    environment: str = PRODUCTION_ENVIRONMENT,
) -> dict[str, list[dict]]:
    """Build per-language cross-language compliance reports for the requested month lookback window.

    Fetches all non-deleted revisions for the full date window in a single Cosmos query,
    then buckets them by month in Python. For each month, groups by ReviewId, picks the
    latest revision per review, and checks whether ``CrossLanguagePackageId`` is populated
    (set from ``CrossLanguageMetadata``).

    Returns:
        A dict mapping language name to a list of monthly data-point dicts.
    """
    selected_languages = languages or DEFAULT_LANGUAGES
    month_ranges = get_last_n_month_ranges(months=months, end_date=end_date)
    if not month_ranges:
        return {lang: [] for lang in selected_languages}

    full_start = month_ranges[0][0]
    full_end = month_ranges[-1][1]

    start_iso = to_iso8601(full_start.isoformat())
    end_iso = to_iso8601(full_end.isoformat(), end_of_day=True)

    revisions_container = get_apiview_cosmos_client(container_name="APIRevisions", environment=environment)

    query = (
        "SELECT c.ReviewId, c.Language, c.APIRevisionType, "
        "c.Files[0].CrossLanguagePackageId AS CrossLanguagePackageId, c.CreatedOn "
        "FROM c "
        "WHERE (NOT IS_DEFINED(c.IsDeleted) OR c.IsDeleted = false) "
        "AND c.CreatedOn >= @start AND c.CreatedOn <= @end"
    )
    params = [
        {"name": "@start", "value": start_iso},
        {"name": "@end", "value": end_iso},
    ]

    all_revisions = list(
        revisions_container.query_items(query=query, parameters=params, enable_cross_partition_query=True)
    )

    omit_lower = {lang.lower() for lang in OMIT_LANGUAGES}

    reports: dict[str, list[dict]] = {lang: [] for lang in selected_languages}
    for start, end in month_ranges:
        label = f"{start.year}-{start.month:02d}"
        month_start_iso = to_iso8601(start.isoformat())
        month_end_iso = to_iso8601(end.isoformat(), end_of_day=True)

        # Filter revisions to this month's window
        month_revisions = [
            rev for rev in all_revisions if month_start_iso <= rev.get("CreatedOn", "") <= month_end_iso
        ]

        # Group by ReviewId and keep only the latest revision per review
        latest_by_review: dict[str, dict] = {}
        for rev in month_revisions:
            review_id = rev.get("ReviewId")
            if not review_id:
                continue
            existing = latest_by_review.get(review_id)
            if existing is None or rev.get("CreatedOn", "") > existing.get("CreatedOn", ""):
                latest_by_review[review_id] = rev

        # Compute compliance per language
        by_language: dict[str, dict] = {}
        for rev in latest_by_review.values():
            lang = get_language_pretty_name(rev.get("Language", "Unknown"))
            if lang.lower() in omit_lower:
                continue
            has_metadata = bool(rev.get("CrossLanguagePackageId"))
            entry = by_language.setdefault(lang, {"compliant": 0, "non_compliant": 0, "total": 0})
            entry["total"] += 1
            if has_metadata:
                entry["compliant"] += 1
            else:
                entry["non_compliant"] += 1

        for entry in by_language.values():
            entry["pct"] = round((entry["compliant"] / entry["total"]) * 100, 2) if entry["total"] else 0.0

        for language in selected_languages:
            entry = by_language.get(language, {"compliant": 0, "non_compliant": 0, "total": 0, "pct": 0.0})
            point = MonthlyCompliancePoint(
                label=label,
                start_date=start.isoformat(),
                end_date=end.isoformat(),
                compliant=entry["compliant"],
                non_compliant=entry["non_compliant"],
                total=entry["total"],
                pct=entry["pct"],
            )
            reports[language].append(asdict(point))

    return reports


def generate_compliance_chart(
    reports: dict[str, list[dict]],
    output_path: Path = DEFAULT_COMPLIANCE_OUTPUT_PATH,
    *,
    environment: str = PRODUCTION_ENVIRONMENT,
) -> Optional[Path]:
    """Render a PNG chart showing cross-language compliance percentage trends per language."""
    output_path.parent.mkdir(parents=True, exist_ok=True)

    try:
        import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib is not installed; skipping chart generation.")
        return None

    languages = list(reports.keys())
    month_count = len(next(iter(reports.values()), [])) if reports else 0
    if month_count == 0:
        return None

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

    for index, language in enumerate(languages):
        axis = axes[index]
        report = reports[language]
        labels = [item["label"] for item in report]
        x_positions = list(range(len(labels)))
        pcts = [item["pct"] for item in report]

        _bars = axis.bar(x_positions, pcts, color="#4CAF50", width=0.6)

        # Annotate each bar with count
        for bar_pos, item in zip(x_positions, report):
            if item["total"] > 0:
                axis.annotate(
                    f"{item['compliant']}/{item['total']}",
                    (bar_pos, item["pct"]),
                    textcoords="offset points",
                    xytext=(0, 4),
                    ha="center",
                    fontsize=7,
                )

        axis.axhline(y=100, color="gray", linestyle=":", linewidth=1.0, alpha=0.5)
        axis.set_title(language)
        axis.set_xticks(x_positions, labels, rotation=45, ha="right")
        axis.set_ylim(0, 115)
        axis.grid(True, axis="y", linestyle="--", alpha=0.4)

    for index in range(len(languages), len(axes)):
        figure.delaxes(axes[index])

    environment_label = (environment or PRODUCTION_ENVIRONMENT).strip().lower()
    figure.suptitle(
        f"Cross-Language Metadata Compliance %\nLast {month_count} Calendar Months (APIView {environment_label})",
        fontsize=14,
        y=0.985,
    )
    figure.supxlabel("Month")
    figure.supylabel("% Reviews with CrossLanguageMetadata")
    plt.tight_layout(rect=(0.02, 0.03, 1, 0.90))
    figure.savefig(output_path, dpi=150)
    plt.close(figure)
    return output_path


def print_compliance_report(
    reports: dict[str, list[dict]],
    output_path: Optional[Path],
    *,
    environment: str = PRODUCTION_ENVIRONMENT,
    file=None,
) -> None:
    """Print a compact terminal summary of cross-language compliance."""
    environment_label = (environment or PRODUCTION_ENVIRONMENT).strip().lower()
    print(f"Cross-language metadata compliance % by month (APIView {environment_label})", file=file)

    for language, report in reports.items():
        print(f"\n{language}", file=file)
        header = ["Month", "Compliant", "Non-Compliant", "Total", "Compliance %"]
        print("  ".join(f"{col:>14}" for col in header), file=file)
        print("  ".join(["----------"] * len(header)), file=file)

        for item in report:
            values = [
                f"{item['label']:>14}",
                f"{item['compliant']:>14}",
                f"{item['non_compliant']:>14}",
                f"{item['total']:>14}",
                f"{item['pct']:>14.1f}",
            ]
            print("  ".join(values), file=file)

    if output_path and output_path.exists():
        print(f"\nSaved chart: {output_path}", file=file)
    else:
        print("\nChart was not generated.", file=file)
