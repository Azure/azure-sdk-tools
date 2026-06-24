"""Batch-extract episodes from all historical Teams channel markdown files.

Parses each MD file, constructs proper ``ConversationMessageItem`` objects,
and calls ``ThreadMemoryService._extract_episode()`` to extract and store
episodes in Cosmos DB.

The ``message`` argument passed to ``_extract_episode`` is the **last expert
reply** (non-bot, non-poster) so the sender check naturally passes.

Usage::

    # Dry-run: parse all files, show which threads qualify (no LLM/Cosmos)
    python scripts/batch_extract_episodes.py --dry-run

    # Process a single file
    python scripts/batch_extract_episodes.py --file TypeSpecDiscussion.md

    # Limit to N threads per file
    python scripts/batch_extract_episodes.py --limit 5

    # Save a summary report
    python scripts/batch_extract_episodes.py --output batch_report.md
"""

from __future__ import annotations

import asyncio
import argparse
import logging
import sys
import time
from pathlib import Path

from dotenv import load_dotenv

_PROJECT_DIR = Path(__file__).resolve().parent.parent.parent
load_dotenv(_PROJECT_DIR / ".env", override=False)

if str(_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(_PROJECT_DIR))

import config.app_config as app_config
from services.thread_memory_service import ThreadMemoryService
from scripts.dev.md_thread_parser import parse_md_file, find_last_expert_message

_HISTORICAL_DIR = _PROJECT_DIR / "historical_messages"
_MD_FILES = [
    "TypeSpecDiscussion.md",
    "APISpecReview.md",
    "general.md",
    "AzureSDKOnboarding.md",
    "JS.md",
    "Python.md",
]

logger = logging.getLogger(__name__)


async def _process_file(
    md_path: Path,
    svc: ThreadMemoryService,
    *,
    dry_run: bool = False,
    limit: int | None = None,
    delay: float = 1.0,
) -> dict:
    """Process a single MD file and return stats."""
    threads, tenant_id = parse_md_file(md_path)
    stats = {
        "file": md_path.name,
        "tenant_id": tenant_id,
        "total": len(threads),
        "qualified": 0,
        "extracted": 0,
        "null": 0,
        "skipped": 0,
        "errors": 0,
        "entries": [],
    }

    print(
        f"\n{'='*60}\n"
        f"File: {md_path.name}  |  Tenant: {tenant_id}  |  Threads: {len(threads)}\n"
        f"{'='*60}",
        file=sys.stderr,
    )

    processed = 0

    for thread in threads:
        if limit and processed >= limit:
            break

        # Find the last expert reply to use as the message trigger
        expert_msg = find_last_expert_message(thread)
        if expert_msg is None:
            stats["skipped"] += 1
            if dry_run:
                stats["entries"].append(
                    f"  [{thread.index}] {thread.title[:60]} — ⏭️ skipped (no expert reply)"
                )
            continue

        # Quality gate
        qualifies = svc._qualifies_for_episode(expert_msg, thread.raw_messages)
        if not qualifies:
            stats["skipped"] += 1
            if dry_run:
                stats["entries"].append(
                    f"  [{thread.index}] {thread.title[:60]} — ⏭️ skipped (does not qualify)"
                )
            continue

        stats["qualified"] += 1

        if dry_run:
            stats["entries"].append(
                f"  [{thread.index}] {thread.title[:60]} — ✅ qualifies "
                f"({len(thread.messages)} msgs, expert: {expert_msg.sender_name})"
            )
            continue

        # Call _extract_episode (LLM + embedding + Cosmos DB upsert)
        print(
            f"  [{thread.index}] {thread.title[:50]}...",
            file=sys.stderr,
            end="",
        )
        try:
            await svc._extract_episode(expert_msg, thread.messages, tenant_id)
            # We can't directly know if LLM returned null or an episode here
            # because _extract_episode doesn't return the result. Count as
            # "processed" — the service logs the outcome.
            stats["extracted"] += 1
            print(" ✅", file=sys.stderr)
        except Exception as e:
            stats["errors"] += 1
            print(f" ❌ {e}", file=sys.stderr)
            logger.exception("Error extracting episode for thread %s", thread.conversation_id)

        processed += 1

        # Rate limiting
        if delay > 0:
            await asyncio.sleep(delay)

    return stats


def _format_stats_report(all_stats: list[dict]) -> str:
    """Format a summary report from all file stats."""
    lines = ["# Batch Episode Extraction Report\n"]

    totals = {"total": 0, "qualified": 0, "extracted": 0, "skipped": 0, "errors": 0}

    for stats in all_stats:
        lines.append(f"## {stats['file']}")
        lines.append(f"- Tenant: `{stats['tenant_id']}`")
        lines.append(f"- Total threads: {stats['total']}")
        lines.append(f"- Qualified: {stats['qualified']}")
        lines.append(f"- Processed: {stats['extracted']}")
        lines.append(f"- Skipped: {stats['skipped']}")
        if stats["errors"]:
            lines.append(f"- **Errors: {stats['errors']}**")
        lines.append("")

        for entry in stats.get("entries", []):
            lines.append(entry)
        if stats.get("entries"):
            lines.append("")

        for k in totals:
            totals[k] += stats.get(k, 0)

    lines.append("## Totals\n")
    for k, v in totals.items():
        lines.append(f"- {k}: {v}")

    return "\n".join(lines)


async def main() -> None:
    parser = argparse.ArgumentParser(
        description="Batch-extract episodes from historical Teams channel markdown files.",
    )
    parser.add_argument(
        "--file", "-f", type=str, default=None,
        help="Process only this MD file (e.g. TypeSpecDiscussion.md)",
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Parse and check qualification without calling LLM / Cosmos DB",
    )
    parser.add_argument(
        "--limit", type=int, default=None,
        help="Max qualifying threads to process per file",
    )
    parser.add_argument(
        "--delay", type=float, default=1.0,
        help="Seconds to wait between LLM calls (default: 1.0)",
    )
    parser.add_argument(
        "--output", "-o", type=str, default=None,
        help="Write summary report to this file",
    )
    parser.add_argument(
        "--verbose", "-v", action="store_true",
        help="Enable debug logging",
    )
    args = parser.parse_args()

    log_level = logging.DEBUG if args.verbose else logging.WARNING
    logging.basicConfig(
        level=log_level,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stderr,
    )
    logging.getLogger("azure.core").setLevel(logging.WARNING)
    logging.getLogger("azure.identity").setLevel(logging.WARNING)

    await app_config.init()

    # Determine which files to process
    if args.file:
        files = [args.file]
    else:
        files = _MD_FILES

    svc = ThreadMemoryService()
    all_stats: list[dict] = []
    start = time.time()

    for filename in files:
        md_path = _HISTORICAL_DIR / filename
        if not md_path.exists():
            print(f"WARNING: {md_path} not found, skipping", file=sys.stderr)
            continue

        stats = await _process_file(
            md_path,
            svc,
            dry_run=args.dry_run,
            limit=args.limit,
            delay=args.delay,
        )
        all_stats.append(stats)

    elapsed = time.time() - start
    print(f"\nDone in {elapsed:.1f}s", file=sys.stderr)

    # Report
    report = _format_stats_report(all_stats)

    if args.output:
        Path(args.output).write_text(report, encoding="utf-8")
        print(f"Report written to {args.output}", file=sys.stderr)
    else:
        sys.stdout.buffer.write(report.encode("utf-8"))
        sys.stdout.buffer.write(b"\n")


if __name__ == "__main__":
    asyncio.run(main())
