"""Daily feedback-job scan.

Scheduled batch job that keeps the ``qa-records`` status table up to date and
drives the feedback loop:

1. **Ingest** — read conversation messages active in a time window, aggregate
   them by ``conversation_id`` into threads, and upsert one QA record per
   thread (new threads start ``ongoing``).
2. **Evaluate** — for every ``ongoing`` record, ask the LLM judge whether the
   thread has *finished* and whether the bot answered *correctly*
   (:meth:`ConversationService.evaluate_conversation`):
     * still ongoing            -> stay ``ongoing`` (re-check next run).
     * finished + correct       -> ``finished`` (archived).
     * finished + incorrect/unknown -> ``failed`` (needs feedback).
3. **Feedback** — for records that just turned ``failed``, run the hosted
   feedback agent **in-process** via :class:`FeedbackAgentService` (a
   synchronous Responses call per thread).

Usage::

    # Scan the last day (default) and drive the feedback loop
    python scripts/run_feedback_jobs.py

    # Scan a wider window
    python scripts/run_feedback_jobs.py --days 2
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

import yaml
from dotenv import load_dotenv

_PROJECT_DIR = Path(__file__).resolve().parent.parent
load_dotenv(_PROJECT_DIR / ".env", override=False)

if str(_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(_PROJECT_DIR))

import config.app_config as app_config
from models.qa_record import QAStatus
from services.conversation_service import ConversationService
from services.feedback_agent_service import FeedbackAgentService
from services.qa_record_service import QARecordService
from utils.azure_ai_foundry import close_clients as close_ai_clients
from utils.azure_cosmosdb import close_cosmos_client
from utils.azure_credential import close_credential
from utils.azure_storage import close_storage_client, download_blob

logger = logging.getLogger("run_feedback_jobs")

# Channels whose display name ends with this suffix (case-insensitive) are
# testing channels — excluded from the feedback loop so we never file issues
# for test traffic. Mirrors scripts/evaluate_channel_conversations.py.
_TESTING_CHANNEL_SUFFIX = "testing"


async def _load_excluded_channels() -> set[str]:
    """Return channel ids marked as testing channels in ``channel.yaml``.

    Returns an empty set (and logs a warning) when the blob is missing or
    malformed so the scan can still proceed without exclusion.
    """
    try:
        container = app_config.get("STORAGE_CONFIG_CONTAINER")
        blob = app_config.get("CHANNEL_CONFIG_BLOB")
        data = await download_blob(container, blob)
    except Exception:
        logger.warning("Failed to download channel.yaml", exc_info=True)
        return set()
    if not data:
        return set()
    try:
        parsed = yaml.safe_load(data.decode("utf-8")) or {}
    except yaml.YAMLError:
        logger.warning("Failed to parse channel.yaml", exc_info=True)
        return set()
    excluded: set[str] = set()
    for entry in parsed.get("channels", []) or []:
        channel_id = entry.get("id")
        name = (entry.get("name") or "").strip().lower()
        if channel_id and name.endswith(_TESTING_CHANNEL_SUFFIX):
            excluded.add(channel_id)
    return excluded


def _parse_dt(value: str) -> datetime:
    dt = datetime.fromisoformat(value)
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def _resolve_window(args: argparse.Namespace) -> tuple[datetime, datetime]:
    end = _parse_dt(args.end) if args.end else datetime.now(timezone.utc)
    if args.start:
        start = _parse_dt(args.start)
    else:
        start = end - timedelta(days=args.days)
    if start >= end:
        raise ValueError(f"start ({start}) must be before end ({end})")
    return start, end


async def _run(args: argparse.Namespace) -> None:
    conversation_service = ConversationService()
    qa_service = QARecordService(conversation_service)
    feedback_service = FeedbackAgentService()

    start, end = _resolve_window(args)
    logger.info("Scanning conversations active in [%s, %s)", start.isoformat(), end.isoformat())

    excluded_channels = await _load_excluded_channels()
    if excluded_channels:
        logger.info("Excluding %d testing channel(s)", len(excluded_channels))

    # 1. Ingest — upsert QA records for threads active in the window.
    messages = await conversation_service.get_messages_in_period(start, end)
    touched = await qa_service.upsert_threads_from_messages(
        messages, excluded_channels=excluded_channels
    )
    logger.info("Upserted %d QA thread record(s) from the window", len(touched))

    # 2. Evaluate every ongoing record (across the whole table, not just the
    #    window — a thread may have gone quiet and become judgeable).
    ongoing = await qa_service.list_ongoing(tenant_id=args.tenant)
    logger.info("Evaluating %d ongoing QA record(s)", len(ongoing))

    # The feedback step (which files GitHub issues via the hosted agent) is
    # gated so it can be disabled via config without touching the pipeline.
    feedback_enabled = (
        app_config.get("FEEDBACK_AGENT_ENABLED", "false").strip().lower() == "true"
    )
    if not feedback_enabled and not args.dry_run:
        logger.info("FEEDBACK_AGENT_ENABLED is not set; feedback analysis disabled")

    counts = {"ongoing": 0, "finished": 0, "failed": 0, "skipped": 0, "triggered": 0}

    for i, record in enumerate(ongoing):
        if args.limit is not None and i >= args.limit:
            break
        if QARecordService.channel_key_of(record) in excluded_channels:
            counts["skipped"] += 1
            continue
        items = await conversation_service.get_messages_by_conversation_id(
            record.conversation_id, record.conversation_type
        )
        evaluation = await conversation_service.evaluate_conversation(items)
        if evaluation is None:
            counts["skipped"] += 1
            continue

        record = await qa_service.apply_evaluation(record, evaluation)
        if record.qa_status == QAStatus.finished:
            counts["finished"] += 1
            continue
        if record.qa_status == QAStatus.ongoing:
            counts["ongoing"] += 1
            continue

        # 3. qa_status == failed -> run the hosted feedback agent in-process.
        counts["failed"] += 1
        if args.dry_run or not feedback_enabled:
            logger.info("Skipping feedback for %s (disabled or dry-run)", record.id)
            continue
        try:
            await feedback_service.run_job(record.id, record.tenant_id)
            counts["triggered"] += 1
        except Exception:
            logger.exception("Failed to trigger feedback for %s", record.id)

    logger.info(
        "Feedback scan complete: finished=%d failed=%d still-ongoing=%d "
        "skipped=%d feedback-triggered=%d",
        counts["finished"],
        counts["failed"],
        counts["ongoing"],
        counts["skipped"],
        counts["triggered"],
    )


async def main() -> None:
    parser = argparse.ArgumentParser(
        description="Scan QA conversations, update the qa-records status table, "
        "and drive the feedback loop.",
    )
    parser.add_argument("--days", type=int, default=1, help="Look back this many days (default: 1).")
    parser.add_argument("--start", type=str, default=None, help="Window start (UTC ISO-8601).")
    parser.add_argument("--end", type=str, default=None, help="Window end (UTC ISO-8601). Default: now.")
    parser.add_argument("--tenant", type=str, default=None, help="Restrict to a single tenant id.")
    parser.add_argument("--limit", type=int, default=None, help="Max ongoing records to evaluate.")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Evaluate and update statuses but do not trigger feedback sessions.",
    )
    parser.add_argument("--verbose", "-v", action="store_true", help="Enable debug logging.")
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stdout,
    )
    logging.getLogger("azure.core").setLevel(logging.WARNING)
    logging.getLogger("azure.identity").setLevel(logging.WARNING)

    await app_config.init()
    try:
        await _run(args)
    finally:
        await _close_clients()


async def _close_clients() -> None:
    for closer in (
        close_ai_clients,
        close_cosmos_client,
        close_storage_client,
        close_credential,
    ):
        try:
            await closer()
        except Exception:
            logger.debug("Error closing client %s", closer.__name__, exc_info=True)


if __name__ == "__main__":
    asyncio.run(main())
