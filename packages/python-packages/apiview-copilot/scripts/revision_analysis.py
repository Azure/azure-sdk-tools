# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Analyzes APIView revision data over a 30-day lookback period.

Reports:
  - Revisions created per day
  - Active vs. inactive revisions per day (active = has non-Diagnostic comments)
  - Revisions by type (Manual / Automatic / PullRequest) per day
  - Unique reviews represented per day and top reviews by revision count
  - Average revisions per review
  - Breakdown of revision data by language

Usage:
    python scripts/revision_analysis.py [--days 30] [--environment production]
"""

import argparse
import re
import sys
from collections import Counter, defaultdict
from datetime import datetime, timedelta, timezone
from pathlib import Path

# Ensure project root is on sys.path so `src` is importable
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from src._apiview import APIVIEW_COMMENT_SELECT_FIELDS, get_apiview_cosmos_client
from src._utils import get_language_pretty_name

# APIView C# enum APIRevisionType: Manual = 0, Automatic = 1, PullRequest = 2
REVISION_TYPE_MAP = {
    0: "Manual",
    1: "Automatic",
    2: "PullRequest",
}


def fetch_revisions(days: int, environment: str) -> list[dict]:
    """Query APIRevisions created in the last `days` days."""
    container = get_apiview_cosmos_client(container_name="APIRevisions", environment=environment)
    cutoff = (datetime.now(timezone.utc) - timedelta(days=days)).isoformat()

    query = (
        "SELECT c.id, c.ReviewId, c.CreatedOn, c.IsDeleted, c.APIRevisionType, "
        "c.packageVersion, c.Label "
        "FROM c WHERE c.CreatedOn >= @cutoff"
    )
    params = [{"name": "@cutoff", "value": cutoff}]

    print(f"Querying APIRevisions created since {cutoff[:10]} ...")
    items = list(container.query_items(query=query, parameters=params, enable_cross_partition_query=True))
    print(f"  Retrieved {len(items)} revisions.\n")
    return items


def analyse(revisions: list[dict], active_revision_ids: set[str]):
    """Build per-day aggregations and a top-reviews summary.

    A revision is "active" if its ID is in *active_revision_ids* (has non-Diagnostic
    comments).  Otherwise it is counted as "inactive".
    """
    per_day_total = Counter()
    per_day_active = Counter()
    per_day_inactive = Counter()
    per_day_type = defaultdict(Counter)  # day -> {type_name: count}
    per_day_reviews = defaultdict(set)  # day -> set of review IDs
    review_revision_count = Counter()  # review_id -> total revisions
    review_type_count = defaultdict(Counter)  # review_id -> {type_name: count}

    for rev in revisions:
        created_on = rev.get("CreatedOn", "")
        if not created_on:
            continue
        day = created_on[:10]  # "YYYY-MM-DD"

        per_day_total[day] += 1

        rev_id = rev.get("id", "")
        if rev_id in active_revision_ids:
            per_day_active[day] += 1
        else:
            per_day_inactive[day] += 1

        rev_type_raw = rev.get("APIRevisionType")
        rev_type = REVISION_TYPE_MAP.get(rev_type_raw, str(rev_type_raw) if rev_type_raw is not None else "Unknown")
        per_day_type[day][rev_type] += 1

        review_id = rev.get("ReviewId")
        if review_id:
            per_day_reviews[day].add(review_id)
            review_revision_count[review_id] += 1
            review_type_count[review_id][rev_type] += 1

    return (
        per_day_total,
        per_day_active,
        per_day_inactive,
        per_day_type,
        per_day_reviews,
        review_revision_count,
        review_type_count,
    )


def fetch_review_metadata(review_ids: list[str], environment: str) -> dict[str, dict]:
    """Query the Reviews container to get PackageName and Language for review IDs.

    Returns a dict mapping review_id -> {"name": str, "language": str}.
    Batches queries in groups of 200 to avoid Cosmos query limits.
    """
    if not review_ids:
        return {}
    container = get_apiview_cosmos_client(container_name="Reviews", environment=environment)
    result_map: dict[str, dict] = {}
    batch_size = 200
    for start in range(0, len(review_ids), batch_size):
        batch = review_ids[start : start + batch_size]
        params = []
        clauses = []
        for i, rid in enumerate(batch):
            pname = f"@id_{i}"
            clauses.append(f"c.id = {pname}")
            params.append({"name": pname, "value": rid})
        query = f"SELECT c.id, c.PackageName, c.Language FROM c WHERE ({' OR '.join(clauses)})"
        results = list(container.query_items(query=query, parameters=params, enable_cross_partition_query=True))
        for r in results:
            lang = get_language_pretty_name(r.get("Language", "Unknown"))
            name = r.get("PackageName", "(unknown)")
            # Heuristic: Java packages starting with com.azure.android are Android
            if lang == "Java" and name and name.startswith("com.azure.android:"):
                lang = "Android"
            result_map[r["id"]] = {"name": name, "language": lang}
    return result_map


def fetch_revision_comment_flags(revision_ids: list[str], environment: str) -> set[str]:
    """Return the set of revision IDs that have at least one non-Diagnostic comment.

    A revision is considered "active" only if it has human or AI-generated comments
    (i.e. CommentSource != 'Diagnostic').
    """
    if not revision_ids:
        return set()
    container = get_apiview_cosmos_client(container_name="Comments", environment=environment)
    active_ids: set[str] = set()
    batch_size = 200
    for start in range(0, len(revision_ids), batch_size):
        batch = revision_ids[start : start + batch_size]
        params = []
        clauses = []
        for i, rev_id in enumerate(batch):
            pname = f"@rev_{i}"
            clauses.append(f"c.APIRevisionId = {pname}")
            params.append({"name": pname, "value": rev_id})
        query = (
            f"SELECT DISTINCT c.APIRevisionId FROM c "
            f"WHERE ({' OR '.join(clauses)}) "
            f"AND (NOT IS_DEFINED(c.CommentSource) OR c.CommentSource != 'Diagnostic')"
        )
        results = list(container.query_items(query=query, parameters=params, enable_cross_partition_query=True))
        for r in results:
            rid = r.get("APIRevisionId")
            if rid:
                active_ids.add(rid)
    return active_ids


def print_report(
    per_day_total,
    per_day_active,
    per_day_inactive,
    per_day_type,
    per_day_reviews,
    review_revision_count,
    review_type_count,
    review_metadata: dict[str, dict],
    revisions: list[dict],
    days: int,
):
    all_days = sorted(per_day_total.keys())
    if not all_days:
        print("No revisions found in the lookback period.")
        return

    # ── Daily breakdown ──────────────────────────────────────────────
    header = (
        f"{'Date':<12} {'Total':>6} {'Active':>7} {'Inactive':>9} "
        f"{'Manual':>7} {'Auto':>6} {'PR':>5} {'Reviews':>8}"
    )
    print("=" * len(header))
    print(f" Revision Activity  --  last {days} days")
    print("=" * len(header))
    print("  (Active = has non-Diagnostic comments)")
    print(header)
    print("-" * len(header))

    sum_total = sum_act = sum_inact = 0
    sum_type = Counter()

    for day in all_days:
        total = per_day_total[day]
        active = per_day_active[day]
        inactive = per_day_inactive[day]
        manual = per_day_type[day].get("Manual", 0)
        auto = per_day_type[day].get("Automatic", 0)
        pr = per_day_type[day].get("PullRequest", 0)
        reviews = len(per_day_reviews[day])

        print(
            f"{day:<12} {total:>6} {active:>7} {inactive:>9} "
            f"{manual:>7} {auto:>6} {pr:>5} {reviews:>8}"
        )

        sum_total += total
        sum_act += active
        sum_inact += inactive
        sum_type["Manual"] += manual
        sum_type["Automatic"] += auto
        sum_type["PullRequest"] += pr

    print("-" * len(header))
    total_reviews = len(review_revision_count)
    print(
        f"{'TOTAL':<12} {sum_total:>6} {sum_act:>7} {sum_inact:>9} "
        f"{sum_type['Manual']:>7} {sum_type['Automatic']:>6} {sum_type['PullRequest']:>5} {total_reviews:>8}"
    )
    print()

    # ── Top reviews by revision count ────────────────────────────────
    top_n = 15
    print(f"Top {top_n} reviews by revision count:")
    print(f"  {'ReviewId':<40} {'Name':<40} {'Lang':<12} {'Total':>6} {'Auto':>6} {'Manual':>7} {'PR':>5}")
    print(f"  {'-'*40} {'-'*40} {'-'*12} {'-'*6} {'-'*6} {'-'*7} {'-'*5}")
    for review_id, count in review_revision_count.most_common(top_n):
        meta = review_metadata.get(review_id, {})
        name = meta.get("name", "(unknown)")
        lang = meta.get("language", "(unknown)")
        if len(name) > 39:
            name = name[:36] + "..."
        tc = review_type_count[review_id]
        print(
            f"  {review_id:<40} {name:<40} {lang:<12} {count:>6} "
            f"{tc.get('Automatic', 0):>6} {tc.get('Manual', 0):>7} {tc.get('PullRequest', 0):>5}"
        )
    print()

    # ── Summary stats ────────────────────────────────────────────────
    avg_per_day = sum_total / max(len(all_days), 1)
    avg_per_review = sum_total / max(total_reviews, 1)
    print("Summary:")
    print(f"  Days with activity:    {len(all_days)}")
    print(f"  Total revisions:       {sum_total}")
    print(f"  Avg revisions/day:     {avg_per_day:.1f}")
    print(f"  Avg revisions/review:  {avg_per_review:.1f}")
    print(f"  Active revisions:      {sum_act}  ({100*sum_act/max(sum_total,1):.1f}%)")
    print(f"  Inactive revisions:    {sum_inact}  ({100*sum_inact/max(sum_total,1):.1f}%)")
    print(f"  Manual:                {sum_type['Manual']}  ({100*sum_type['Manual']/max(sum_total,1):.1f}%)")
    print(f"  Automatic:             {sum_type['Automatic']}  ({100*sum_type['Automatic']/max(sum_total,1):.1f}%)")
    print(f"  PullRequest:           {sum_type['PullRequest']}  ({100*sum_type['PullRequest']/max(sum_total,1):.1f}%)")
    print(f"  Unique reviews:        {total_reviews}")
    print()

    # ── Per-language breakdown ────────────────────────────────────────
    print_language_breakdown(revisions, review_metadata, review_revision_count)

    # ── C# & Java automatic revision version breakdown ───────────────
    print_version_breakdown(revisions, review_metadata, ["C#", "Java"])


def print_language_breakdown(
    revisions: list[dict],
    review_metadata: dict[str, dict],
    review_revision_count: Counter,
):
    """Print revision stats grouped by language."""
    lang_revisions = Counter()  # language -> revision count
    lang_reviews = defaultdict(set)  # language -> set of review IDs
    lang_type = defaultdict(Counter)  # language -> {type_name: count}

    for rev in revisions:
        review_id = rev.get("ReviewId")
        if not review_id:
            continue
        meta = review_metadata.get(review_id, {})
        lang = meta.get("language", "(unknown)")

        lang_revisions[lang] += 1
        lang_reviews[lang].add(review_id)

        rev_type_raw = rev.get("APIRevisionType")
        rev_type = REVISION_TYPE_MAP.get(rev_type_raw, str(rev_type_raw) if rev_type_raw is not None else "Unknown")
        lang_type[lang][rev_type] += 1

    if not lang_revisions:
        return

    lang_header = (
        f"{'Language':<16} {'Revisions':>10} {'Reviews':>8} {'Avg Rev/Rev':>11} "
        f"{'Manual':>7} {'Auto':>6} {'PR':>5}"
    )
    print("=" * len(lang_header))
    print(" Revisions by Language")
    print("=" * len(lang_header))
    print(lang_header)
    print("-" * len(lang_header))

    for lang in sorted(lang_revisions, key=lang_revisions.get, reverse=True):
        rev_count = lang_revisions[lang]
        review_count = len(lang_reviews[lang])
        avg = rev_count / max(review_count, 1)
        manual = lang_type[lang].get("Manual", 0)
        auto = lang_type[lang].get("Automatic", 0)
        pr = lang_type[lang].get("PullRequest", 0)
        print(
            f"{lang:<16} {rev_count:>10} {review_count:>8} {avg:>11.1f} "
            f"{manual:>7} {auto:>6} {pr:>5}"
        )

    print("-" * len(lang_header))
    total_rev = sum(lang_revisions.values())
    total_rev_set = set()
    for s in lang_reviews.values():
        total_rev_set |= s
    total_reviews = len(total_rev_set)
    print(
        f"{'TOTAL':<16} {total_rev:>10} {total_reviews:>8} "
        f"{total_rev / max(total_reviews, 1):>11.1f} "
        f"{sum(lang_type[l].get('Manual', 0) for l in lang_revisions):>7} "
        f"{sum(lang_type[l].get('Automatic', 0) for l in lang_revisions):>6} "
        f"{sum(lang_type[l].get('PullRequest', 0) for l in lang_revisions):>5}"
    )


def classify_version_stage(version: str) -> str:
    """Classify a package version string into alpha, beta, or GA.

    - "alpha" if the version contains "alpha"
    - "beta" if it contains beta/preview/rc/dev/snapshot/nightly/canary/pre,
      or matches Python-style beta (e.g. 1.0.0b1), or is 0.x.x
    - "GA" if >= 1.0.0 with no pre-release indicators
    - "unknown" if version is empty/missing
    """
    if not version:
        return "unknown"

    version_lower = version.lower()

    if "alpha" in version_lower:
        return "alpha"

    beta_indicators = ["beta", "rc", "dev", "preview", "snapshot", "nightly", "canary", "pre"]
    for indicator in beta_indicators:
        if indicator in version_lower:
            return "beta"

    # Python-style beta: e.g. 1.0.0b1
    if re.search(r"\d+b\d+", version_lower):
        return "beta"

    # 0.x.x is not GA per semver
    if re.match(r"^0\.", version):
        return "beta"

    return "GA"


def print_version_breakdown(
    revisions: list[dict],
    review_metadata: dict[str, dict],
    languages: list[str],
):
    """Print version-stage breakdown (alpha/beta/GA) for automatic revisions
    of the specified languages."""
    target_langs = {l.lower() for l in languages}

    # Per-language counters
    lang_stage = defaultdict(Counter)  # language -> {stage: count}
    lang_stage_reviews = defaultdict(lambda: defaultdict(set))  # language -> {stage: set of review IDs}

    for rev in revisions:
        # Only automatic revisions (value may be int 1 or string "Automatic")
        rev_type_raw = rev.get("APIRevisionType")
        rev_type_name = REVISION_TYPE_MAP.get(rev_type_raw, str(rev_type_raw) if rev_type_raw is not None else "")
        if rev_type_name != "Automatic":
            continue
        review_id = rev.get("ReviewId")
        if not review_id:
            continue
        meta = review_metadata.get(review_id, {})
        lang = meta.get("language", "(unknown)")
        if lang.lower() not in target_langs:
            continue

        version = rev.get("packageVersion", "") or rev.get("Label", "") or ""
        stage = classify_version_stage(version)

        lang_stage[lang][stage] += 1
        lang_stage_reviews[lang][stage].add(review_id)

    if not lang_stage:
        print("\nNo matching automatic revisions found for version breakdown.")
        return

    langs_label = " & ".join(languages)
    ver_header = (
        f"{'Language':<12} {'Stage':<10} {'Revisions':>10} {'%':>7} {'Reviews':>8}"
    )
    print()
    print("=" * len(ver_header))
    print(f" Automatic Revision Version Breakdown -- {langs_label}")
    print("=" * len(ver_header))
    print(ver_header)
    print("-" * len(ver_header))

    for lang in sorted(lang_stage, key=lambda l: sum(lang_stage[l].values()), reverse=True):
        total_lang = sum(lang_stage[lang].values())
        for stage in ["alpha", "beta", "GA", "unknown"]:
            count = lang_stage[lang].get(stage, 0)
            if count == 0:
                continue
            pct = 100 * count / max(total_lang, 1)
            review_count = len(lang_stage_reviews[lang][stage])
            print(f"{lang:<12} {stage:<10} {count:>10} {pct:>6.1f}% {review_count:>8}")
        print(f"{'':<12} {'TOTAL':<10} {total_lang:>10} {'100.0%':>7}")
        print()

    # Combined totals
    all_stages = Counter()
    all_stage_reviews = defaultdict(set)
    for lang in lang_stage:
        for stage, count in lang_stage[lang].items():
            all_stages[stage] += count
            all_stage_reviews[stage] |= lang_stage_reviews[lang][stage]
    grand_total = sum(all_stages.values())
    print(f"{'Combined':<12} {'---':<10} {'---':>10} {'---':>7} {'---':>8}")
    for stage in ["alpha", "beta", "GA", "unknown"]:
        count = all_stages.get(stage, 0)
        if count == 0:
            continue
        pct = 100 * count / max(grand_total, 1)
        review_count = len(all_stage_reviews[stage])
        print(f"{'':<12} {stage:<10} {count:>10} {pct:>6.1f}% {review_count:>8}")
    print(f"{'':<12} {'TOTAL':<10} {grand_total:>10} {'100.0%':>7}")


def main():
    parser = argparse.ArgumentParser(description="Analyse APIView revision activity.")
    parser.add_argument("--days", type=int, default=30, help="Number of days to look back (default: 30)")
    parser.add_argument("--environment", default="production", choices=["production", "staging"])
    args = parser.parse_args()

    revisions = fetch_revisions(days=args.days, environment=args.environment)

    # Determine which revisions are "active" (have non-Diagnostic comments)
    all_revision_ids = [r["id"] for r in revisions if r.get("id")]
    print("Checking which revisions are considered 'Active'...")
    active_revision_ids = fetch_revision_comment_flags(all_revision_ids, environment=args.environment)
    print(f"  {len(active_revision_ids)} of {len(all_revision_ids)} revisions are active.\n")

    (
        per_day_total,
        per_day_active,
        per_day_inactive,
        per_day_type,
        per_day_reviews,
        review_revision_count,
        review_type_count,
    ) = analyse(revisions, active_revision_ids)

    # Fetch review metadata (name + language) for all unique reviews
    all_review_ids = list(review_revision_count.keys())
    print(f"Fetching review metadata for {len(all_review_ids)} reviews ...")
    review_metadata = fetch_review_metadata(all_review_ids, environment=args.environment)
    print(f"  Retrieved metadata for {len(review_metadata)} reviews.\n")

    print_report(
        per_day_total,
        per_day_active,
        per_day_inactive,
        per_day_type,
        per_day_reviews,
        review_revision_count,
        review_type_count,
        review_metadata,
        revisions,
        days=args.days,
    )


if __name__ == "__main__":
    main()
