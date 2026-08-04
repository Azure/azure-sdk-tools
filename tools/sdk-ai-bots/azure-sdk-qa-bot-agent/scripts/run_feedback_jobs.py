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
3. **Feedback** — for every runnable ``failed`` record, including work left
   by a disabled or interrupted earlier scan, run the hosted chatbot evolution
   agent **in-process** via
   :class:`ChatbotEvolutionAgentService` (a synchronous Responses call per
   thread).

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
from services.chatbot_evolution_agent_service import ChatbotEvolutionAgentService
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


async def _load_excluded_channels() -> set[str] | None:
    """Return channel ids marked as testing channels in ``channel.yaml``.

    Returns ``None`` when the configuration cannot be loaded or validated.
    Evaluation can still proceed in that case, but feedback invocation must
    fail closed because testing channels cannot be excluded safely.
    """
    try:
        container = app_config.get("STORAGE_CONFIG_CONTAINER")
        blob = app_config.get("CHANNEL_CONFIG_BLOB")
        if not container or not blob:
            raise RuntimeError("Channel configuration storage is not configured")
        data = await download_blob(container, blob)
    except Exception:
        logger.warning("Failed to download channel.yaml", exc_info=True)
        return None
    if not data:
        logger.warning("channel.yaml is empty")
        return None
    try:
        parsed = yaml.safe_load(data.decode("utf-8")) or {}
    except (UnicodeDecodeError, yaml.YAMLError):
        logger.warning("Failed to parse channel.yaml", exc_info=True)
        return None
    if not isinstance(parsed, dict):
        logger.warning("channel.yaml root must be a mapping")
        return None
    if "channels" not in parsed:
        logger.warning("channel.yaml is missing the channels list")
        return None
    channels = parsed["channels"]
    if not isinstance(channels, list):
        logger.warning("channel.yaml channels must be a list")
        return None

    excluded: set[str] = set()
    for entry in channels:
        if not isinstance(entry, dict):
            logger.warning("channel.yaml contains a non-mapping channel entry")
            return None
        channel_id = entry.get("id")
        raw_name = entry.get("name")
        if (
            not isinstance(channel_id, str)
            or not channel_id.strip()
            or not isinstance(raw_name, str)
            or not raw_name.strip()
        ):
            logger.warning("channel.yaml channel entries require string id and name")
            return None
        name = raw_name.strip().lower()
        if name.endswith(_TESTING_CHANNEL_SUFFIX):
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
    evolution_service = ChatbotEvolutionAgentService()

    start, end = _resolve_window(args)
    logger.info("Scanning conversations active in [%s, %s)", start.isoformat(), end.isoformat())

    loaded_excluded_channels = await _load_excluded_channels()
    channel_filter_available = loaded_excluded_channels is not None
    excluded_channels = loaded_excluded_channels or set()
    if excluded_channels:
        logger.info("Excluding %d testing channel(s)", len(excluded_channels))
    if not channel_filter_available:
        logger.error(
            "Testing-channel configuration is unavailable; feedback invocation "
            "will be disabled for this scan"
        )

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
        app_config.get("CHATBOT_EVOLUTION_AGENT_ENABLED", "false").strip().lower()
        == "true"
    )
    if not feedback_enabled and not args.dry_run:
        logger.info(
            "CHATBOT_EVOLUTION_AGENT_ENABLED is not set; feedback analysis disabled"
        )

    counts = {
        "ongoing": 0,
        "finished": 0,
        "failed": 0,
        "skipped": 0,
        "feedback_attempted": 0,
    }

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

        counts["failed"] += 1

    # 3. Run every eligible failed record, not only records that failed during
    # this scan. This catches up work created while feedback was disabled and
    # retries failed or stale-running synchronous invocations.
    failed_records = await qa_service.list_failed(tenant_id=args.tenant)
    runnable = [
        record
        for record in failed_records
        if evolution_service.is_runnable(record)
        and QARecordService.channel_key_of(record) not in excluded_channels
    ]
    logger.info("Found %d runnable feedback record(s)", len(runnable))

    if args.dry_run or not feedback_enabled or not channel_filter_available:
        if runnable:
            logger.info(
                "Skipping %d runnable feedback record(s) "
                "(disabled, dry-run, or channel filter unavailable)",
                len(runnable),
            )
    else:
        for record in runnable:
            try:
                attempted = await evolution_service.run_job(
                    record.id, record.tenant_id
                )
                if attempted:
                    counts["feedback_attempted"] += 1
            except Exception:
                logger.exception("Failed to run feedback for %s", record.id)

    logger.info(
        "Feedback scan complete: finished=%d failed=%d still-ongoing=%d "
        "skipped=%d feedback-attempted=%d",
        counts["finished"],
        counts["failed"],
        counts["ongoing"],
        counts["skipped"],
        counts["feedback_attempted"],
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
