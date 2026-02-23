# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Analyzes APIView revision data over a 30-day lookback period.

Reports:
  - Revisions created per day
  - Deleted vs. not-deleted revisions per day
  - Revisions by type (Manual / Automatic / PullRequest) per day
  - Unique reviews represented per day and top reviews by revision count

Usage:
    python scripts/revision_analysis.py [--days 30] [--environment production]
"""

import argparse
import sys
from collections import Counter, defaultdict
from datetime import datetime, timedelta, timezone
from pathlib import Path

# Ensure project root is on sys.path so `src` is importable
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from src._apiview import get_apiview_cosmos_client

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

    print(f"Querying APIRevisions created since {cutoff[:10]} …")
    items = list(container.query_items(query=query, parameters=params, enable_cross_partition_query=True))
    print(f"  Retrieved {len(items)} revisions.\n")
    return items


def analyse(revisions: list[dict]):
    """Build per-day aggregations and a top-reviews summary."""
    per_day_total = Counter()
    per_day_deleted = Counter()
    per_day_not_deleted = Counter()
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

        is_deleted = rev.get("IsDeleted", False)
        if is_deleted:
            per_day_deleted[day] += 1
        else:
            per_day_not_deleted[day] += 1

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
        per_day_deleted,
        per_day_not_deleted,
        per_day_type,
        per_day_reviews,
        review_revision_count,
        review_type_count,
    )


def fetch_review_names(review_ids: list[str], environment: str) -> dict[str, str]:
    """Query the Reviews container to get PackageName for a list of review IDs."""
    if not review_ids:
        return {}
    container = get_apiview_cosmos_client(container_name="Reviews", environment=environment)
    params = []
    clauses = []
    for i, rid in enumerate(review_ids):
        pname = f"@id_{i}"
        clauses.append(f"c.id = {pname}")
        params.append({"name": pname, "value": rid})
    query = f"SELECT c.id, c.PackageName FROM c WHERE ({' OR '.join(clauses)})"
    results = list(container.query_items(query=query, parameters=params, enable_cross_partition_query=True))
    return {r["id"]: r.get("PackageName", "(unknown)") for r in results}


def print_report(
    per_day_total,
    per_day_deleted,
    per_day_not_deleted,
    per_day_type,
    per_day_reviews,
    review_revision_count,
    review_type_count,
    review_names: dict[str, str],
    days: int,
):
    all_days = sorted(per_day_total.keys())
    if not all_days:
        print("No revisions found in the lookback period.")
        return

    # ── Daily breakdown ──────────────────────────────────────────────
    header = (
        f"{'Date':<12} {'Total':>6} {'Deleted':>8} {'Active':>7} {'Manual':>7} {'Auto':>6} {'PR':>5} {'Reviews':>8}"
    )
    print("=" * len(header))
    print(f" Revision Activity  —  last {days} days")
    print("=" * len(header))
    print(header)
    print("-" * len(header))

    sum_total = sum_del = sum_act = 0
    sum_type = Counter()

    for day in all_days:
        total = per_day_total[day]
        deleted = per_day_deleted[day]
        active = per_day_not_deleted[day]
        manual = per_day_type[day].get("Manual", 0)
        auto = per_day_type[day].get("Automatic", 0)
        pr = per_day_type[day].get("PullRequest", 0)
        reviews = len(per_day_reviews[day])

        print(f"{day:<12} {total:>6} {deleted:>8} {active:>7} {manual:>7} {auto:>6} {pr:>5} {reviews:>8}")

        sum_total += total
        sum_del += deleted
        sum_act += active
        sum_type["Manual"] += manual
        sum_type["Automatic"] += auto
        sum_type["PullRequest"] += pr

    print("-" * len(header))
    total_reviews = len(review_revision_count)
    print(
        f"{'TOTAL':<12} {sum_total:>6} {sum_del:>8} {sum_act:>7} "
        f"{sum_type['Manual']:>7} {sum_type['Automatic']:>6} {sum_type['PullRequest']:>5} {total_reviews:>8}"
    )
    print()

    # ── Top reviews by revision count ────────────────────────────────
    top_n = 25
    print(f"Top {top_n} reviews by revision count:")
    print(f"  {'ReviewId':<40} {'Name':<45} {'Total':>6} {'Auto':>6} {'Manual':>7} {'PR':>5}")
    print(f"  {'-'*40} {'-'*45} {'-'*6} {'-'*6} {'-'*7} {'-'*5}")
    for review_id, count in review_revision_count.most_common(top_n):
        name = review_names.get(review_id, "(unknown)")
        if len(name) > 44:
            name = name[:41] + "..."
        tc = review_type_count[review_id]
        print(
            f"  {review_id:<40} {name:<45} {count:>6} "
            f"{tc.get('Automatic', 0):>6} {tc.get('Manual', 0):>7} {tc.get('PullRequest', 0):>5}"
        )
    print()

    # ── Summary stats ────────────────────────────────────────────────
    avg_per_day = sum_total / max(len(all_days), 1)
    print("Summary:")
    print(f"  Days with activity:    {len(all_days)}")
    print(f"  Total revisions:       {sum_total}")
    print(f"  Avg revisions/day:     {avg_per_day:.1f}")
    print(f"  Deleted revisions:     {sum_del}  ({100*sum_del/max(sum_total,1):.1f}%)")
    print(f"  Active revisions:      {sum_act}  ({100*sum_act/max(sum_total,1):.1f}%)")
    print(f"  Manual:                {sum_type['Manual']}  ({100*sum_type['Manual']/max(sum_total,1):.1f}%)")
    print(f"  Automatic:             {sum_type['Automatic']}  ({100*sum_type['Automatic']/max(sum_total,1):.1f}%)")
    print(f"  PullRequest:           {sum_type['PullRequest']}  ({100*sum_type['PullRequest']/max(sum_total,1):.1f}%)")
    print(f"  Unique reviews:        {total_reviews}")


def main():
    parser = argparse.ArgumentParser(description="Analyse APIView revision activity.")
    parser.add_argument("--days", type=int, default=30, help="Number of days to look back (default: 30)")
    parser.add_argument("--environment", default="production", choices=["production", "staging"])
    args = parser.parse_args()

    revisions = fetch_revisions(days=args.days, environment=args.environment)
    (
        per_day_total,
        per_day_deleted,
        per_day_not_deleted,
        per_day_type,
        per_day_reviews,
        review_revision_count,
        review_type_count,
    ) = analyse(revisions)

    # Fetch review names for the top 25
    top_ids = [rid for rid, _ in review_revision_count.most_common(25)]
    print("Fetching review names …")
    review_names = fetch_review_names(top_ids, environment=args.environment)

    print_report(
        per_day_total,
        per_day_deleted,
        per_day_not_deleted,
        per_day_type,
        per_day_reviews,
        review_revision_count,
        review_type_count,
        review_names,
        days=args.days,
    )


if __name__ == "__main__":
    main()
