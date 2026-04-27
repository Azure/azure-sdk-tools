# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Spot-check human vs AI comment timing per review/version.

For each active review/version, shows:
- Number of human-started threads
- Number of copilot-started threads
- If copilot commented: date of earliest copilot comment and earliest human thread

Usage:
    python scripts/spot_check_human_timing.py -s 2026-04-01 -e 2026-04-27 -l python
"""

from __future__ import annotations

import argparse
import sys
from collections import defaultdict
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from src._apiview import get_active_reviews, get_comments_in_date_range
from src._metrics import build_thread_start_index, get_thread_start_date, METRICS_COMMENT_FIELDS
from src._utils import get_language_pretty_name, to_iso8601


# ThreadId is not in the default comment select fields but is needed for
# accurate root-comment deduplication matching the metrics pipeline.
# Note: get_comments_in_date_range adds the "c." prefix automatically when
# select_fields is provided, so pass bare field names here.
EXTRA_SELECT_FIELDS = [
    "id", "CreatedOn", "CreatedBy", "CommentText", "IsResolved",
    "IsDeleted", "ElementId", "ReviewId", "APIRevisionId",
    "Upvotes", "Downvotes", "CommentType", "CommentSource", "ThreadId",
]


def _find_root_comments(comments: list[dict]) -> list[dict]:
    """Keep only the earliest comment per thread (ThreadId or (APIRevisionId, ElementId) fallback).

    Matches the dedup logic in ``src/_metrics._filter_to_root_comments``.
    """
    threaded: dict[str, list[dict]] = defaultdict(list)
    unthreaded: dict[tuple, list[dict]] = defaultdict(list)

    for c in comments:
        if c.get("CommentSource") == "Diagnostic":
            continue
        thread_id = c.get("ThreadId")
        if thread_id:
            threaded[thread_id].append(c)
        else:
            key = (c.get("APIRevisionId"), c.get("ElementId"))
            unthreaded[key].append(c)

    roots: list[dict] = []
    for group in threaded.values():
        group.sort(key=lambda x: x.get("CreatedOn") or "")
        roots.append(group[0])
    for group in unthreaded.values():
        group.sort(key=lambda x: x.get("CreatedOn") or "")
        roots.append(group[0])
    return roots


def run(start_date: str, end_date: str, language: str, environment: str = "production") -> None:
    pretty_lang = get_language_pretty_name(language)
    reviews, raw_comments = get_active_reviews(
        start_date,
        end_date,
        environment=environment,
        select_fields=EXTRA_SELECT_FIELDS,
    )

    window_start_iso = to_iso8601(start_date)
    window_end_iso = to_iso8601(end_date, end_of_day=True)

    # The chart pipeline (build_language_comment_bucket_reports with months=6) builds
    # thread_start_index from 6 months of comments so threads that originated before
    # the current window are correctly identified and excluded.  Mirror that here by
    # fetching a 6-month lookback of comments purely for the thread-start index.
    from dateutil.relativedelta import relativedelta

    lookback_start = (date.fromisoformat(start_date) - relativedelta(months=5)).replace(day=1)
    lookback_comments = get_comments_in_date_range(
        lookback_start.isoformat(),
        end_date,
        environment=environment,
        select_fields=list(METRICS_COMMENT_FIELDS),
        include_deleted=True,
    )
    non_diagnostic_lookback = [c for c in lookback_comments if c.get("CommentSource") != "Diagnostic"]
    thread_start_index = build_thread_start_index(non_diagnostic_lookback)

    # Build revision_id -> (review_id, package_version, review_name, approved) mapping
    rev_to_rv: dict[str, tuple[str, str, str, bool]] = {}
    review_versions: list[tuple[str, str, str, bool, bool]] = []  # (review_id, name, version, has_copilot, approved)
    approved_revision_ids: set[str] = set()

    for review in reviews:
        if review.language != pretty_lang:
            continue
        for rev_meta in review.revisions:
            approved = rev_meta.approval is not None
            for rid in rev_meta.revision_ids:
                rev_to_rv[rid] = (review.review_id, rev_meta.package_version or "?", review.name or "?", approved)
                if approved:
                    approved_revision_ids.add(rid)
            review_versions.append((
                review.review_id,
                review.name or "?",
                rev_meta.package_version or "?",
                rev_meta.has_copilot_review,
                approved,
            ))

    # Group root comments by (review_id, version), only from approved revisions
    root_comments = _find_root_comments(raw_comments)
    rv_comments: dict[tuple[str, str], list[dict]] = defaultdict(list)
    for c in root_comments:
        rev_id = c.get("APIRevisionId", "")
        if rev_id not in approved_revision_ids:
            continue
        mapping = rev_to_rv.get(rev_id)
        if mapping is None:
            continue
        review_id, version, _, _ = mapping
        rv_comments[(review_id, version)].append(c)

    # Print report
    print(f"\nHuman vs AI Thread Timing — {pretty_lang} ({start_date} to {end_date})")
    print("=" * 100)

    total_ai = 0
    total_human = 0
    total_before = 0
    total_after = 0

    for review_id, name, version, has_copilot, approved in sorted(review_versions, key=lambda x: x[1]):
        comments = rv_comments.get((review_id, version), [])

        # Apply thread-start-window filter: only count threads that originated within the window
        def _in_window(c: dict) -> bool:
            thread_start = get_thread_start_date(c, thread_start_index)
            return bool(thread_start and window_start_iso <= thread_start <= window_end_iso)

        ai_threads = [c for c in comments if c.get("CommentSource") == "AIGenerated"]
        human_threads = [c for c in comments if c.get("CommentSource") != "AIGenerated" and _in_window(c)]

        total_ai += len(ai_threads)
        total_human += len(human_threads)

        status = "APPROVED" if approved else "unapproved"
        print(f"\n{name} ({version})  [Copilot: {'YES' if has_copilot else 'no'}]  [{status}]")
        print(f"  AI threads:    {len(ai_threads)}")
        print(f"  Human threads: {len(human_threads)}")

        if ai_threads:
            earliest_ai = min(c.get("CreatedOn", "") for c in ai_threads)
            print(f"  Earliest AI:   {earliest_ai[:19]}")

            if human_threads:
                earliest_human = min(c.get("CreatedOn", "") for c in human_threads)
                print(f"  Earliest Human:{earliest_human[:19]}")
                if earliest_human < earliest_ai:
                    print("  --> Human reviewed BEFORE Copilot")
                else:
                    print("  --> Human reviewed AFTER Copilot")

                before = sum(1 for c in human_threads if (c.get("CreatedOn", "") or "") < earliest_ai)
                after = len(human_threads) - before
                total_before += before
                total_after += after
                print(f"  Breakdown: {before} before AI, {after} after AI")
        elif human_threads:
            earliest_human = min(c.get("CreatedOn", "") for c in human_threads)
            print(f"  Earliest Human:{earliest_human[:19]}")

    # Overall totals
    print(f"\n{'=' * 100}")
    print(f"TOTALS — {pretty_lang}")
    print(f"  AI threads:    {total_ai}")
    print(f"  Human threads: {total_human}")
    print(f"  Human before AI: {total_before}")
    print(f"  Human after AI:  {total_after}")
    if total_before + total_after > 0:
        pct_after = total_after / (total_before + total_after) * 100
        print(f"  % after AI:      {pct_after:.1f}%")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Spot-check human vs AI comment timing")
    parser.add_argument("-s", "--start-date", required=True, help="Start date (YYYY-MM-DD)")
    parser.add_argument("-e", "--end-date", required=True, help="End date (YYYY-MM-DD)")
    parser.add_argument("-l", "--language", required=True, help="Language to filter")
    parser.add_argument("--environment", default="production", help="Environment")
    args = parser.parse_args()
    run(args.start_date, args.end_date, args.language, args.environment)
