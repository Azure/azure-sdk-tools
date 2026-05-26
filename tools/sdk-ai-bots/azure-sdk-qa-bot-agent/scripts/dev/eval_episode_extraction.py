"""Evaluate episode extraction on historical Teams channel threads.

Parses a markdown file into threads, runs each through the episode
extraction LLM call, and produces a human-readable report showing:
- The original post (truncated)
- Whether an episode was extracted or skipped
- The full episode details if extracted

Usage::

    # Dry-run: parse threads and show which qualify (no LLM calls)
    python scripts/eval_episode_extraction.py --dry-run

    # Process all qualifying threads
    python scripts/eval_episode_extraction.py

    # Process a different MD file
    python scripts/eval_episode_extraction.py --file general.md

    # Process only specific thread indices
    python scripts/eval_episode_extraction.py --indices 2 3 5

    # Limit to N threads
    python scripts/eval_episode_extraction.py --limit 5

    # Save report to file
    python scripts/eval_episode_extraction.py --output report.md
"""

from __future__ import annotations

import asyncio
import argparse
import logging
import sys
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
_DEFAULT_FILE = "TypeSpecDiscussion.md"


# ---------------------------------------------------------------------------
# Report formatting
# ---------------------------------------------------------------------------

def _format_report_entry(
    thread,
    episode,
    status: str,
) -> str:
    """Format a single thread evaluation entry for the report."""
    lines = []
    lines.append(f"## [{thread.index}] {thread.title}")
    lines.append(f"**Poster:** {thread.poster}  ")
    lines.append(f"**Messages:** {len(thread.messages)}  ")
    lines.append(f"**Status:** {status}")
    lines.append("")

    # Original post (truncated)
    lines.append("### Original Post")
    lines.append("```")
    lines.append(thread.original_post[:400] + ("..." if len(thread.original_post) > 400 else ""))
    lines.append("```")
    lines.append("")

    if episode is not None:
        lines.append("### Extracted Episode")
        lines.append(f"- **Trigger:** {episode.trigger}")
        if episode.symptoms:
            lines.append(f"- **Symptoms:**")
            for s in episode.symptoms:
                lines.append(f"  - {s}")
        lines.append(f"- **Reasoning chain:**")
        for i, step in enumerate(episode.reasoning_chain, 1):
            lines.append(f"  {i}. {step}")
        lines.append(f"- **Resolution:** {episode.resolution}")
        lines.append(f"- **Key insight:** {episode.key_insight}")
        lines.append(f"- **Confidence:** {episode.confidence:.2f}")

    lines.append("")
    lines.append("---")
    lines.append("")
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

async def main() -> None:
    parser = argparse.ArgumentParser(
        description="Evaluate episode extraction on historical Teams channel threads.",
    )
    parser.add_argument(
        "--file", "-f", type=str, default=_DEFAULT_FILE,
        help=f"MD file name in historical_messages/ (default: {_DEFAULT_FILE})",
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Parse threads and show which qualify without calling the LLM",
    )
    parser.add_argument(
        "--indices", type=int, nargs="+", default=None,
        help="Process only these thread indices",
    )
    parser.add_argument(
        "--limit", type=int, default=None,
        help="Maximum number of threads to process",
    )
    parser.add_argument(
        "--output", "-o", type=str, default=None,
        help="Write report to this file (default: stdout)",
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

    # Parse threads from markdown using shared parser
    md_path = _HISTORICAL_DIR / args.file
    if not md_path.exists():
        print(f"ERROR: File not found: {md_path}", file=sys.stderr)
        sys.exit(1)

    threads, tenant_id = parse_md_file(md_path)
    print(f"Parsed {len(threads)} threads from {md_path.name} (tenant: {tenant_id})", file=sys.stderr)

    svc = ThreadMemoryService()

    # Filter threads
    if args.indices:
        threads = [t for t in threads if t.index in args.indices]

    report_entries: list[str] = []
    report_entries.append(f"# Episode Extraction Evaluation — {md_path.name}\n")
    report_entries.append(f"Tenant: `{tenant_id}`  ")
    report_entries.append(f"Total threads parsed: {len(threads)}\n")

    stats = {"qualified": 0, "extracted": 0, "null": 0, "skipped": 0}
    processed = 0

    for thread in threads:
        if args.limit and processed >= args.limit:
            break

        # Use raw_messages for the quality gate (expects list[dict])
        msgs = thread.raw_messages
        expert_msg = find_last_expert_message(thread)
        if expert_msg is None:
            if args.dry_run:
                report_entries.append(
                    _format_report_entry(thread, None, "⏭️ Skipped (no expert reply)")
                )
            stats["skipped"] += 1
            continue

        qualifies = svc._qualifies_for_episode(expert_msg, msgs)

        if not qualifies:
            if args.dry_run:
                report_entries.append(
                    _format_report_entry(thread, None, "⏭️ Skipped (does not qualify)")
                )
            stats["skipped"] += 1
            continue

        stats["qualified"] += 1

        if args.dry_run:
            report_entries.append(
                _format_report_entry(thread, None, "✅ Qualifies (dry-run, no LLM call)")
            )
            continue

        # Call LLM
        print(
            f"  Processing [{thread.index}] {thread.title[:50]}...",
            file=sys.stderr,
        )
        formatted = svc._format_thread(msgs)
        episode = await svc._call_llm(formatted)

        if episode is None:
            report_entries.append(
                _format_report_entry(thread, None, "❌ LLM returned null (low-value or unresolved)")
            )
            stats["null"] += 1
        else:
            report_entries.append(
                _format_report_entry(thread, episode, f"✅ Episode extracted (confidence: {episode.confidence:.2f})")
            )
            stats["extracted"] += 1

        processed += 1

    # Summary
    report_entries.append("# Summary\n")
    report_entries.append(f"- Qualified: {stats['qualified']}")
    report_entries.append(f"- Episodes extracted: {stats['extracted']}")
    report_entries.append(f"- LLM returned null: {stats['null']}")
    report_entries.append(f"- Skipped (didn't qualify): {stats['skipped']}")

    report = "\n".join(report_entries)

    if args.output:
        Path(args.output).write_text(report, encoding="utf-8")
        print(f"Report written to {args.output}", file=sys.stderr)
    else:
        sys.stdout.buffer.write(report.encode("utf-8"))
        sys.stdout.buffer.write(b"\n")


if __name__ == "__main__":
    asyncio.run(main())
