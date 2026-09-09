"""Daily Chatbot Evolution Agent orchestration.

The pipeline ingests active Teams threads, invokes the Agent to evaluate every
eligible conversation, checks remediation issues, and invokes Agent validation
after an issue closes. Conversation completion and correctness are judged only
inside the Agent.

Usage::

    # Scan the last day (default) and drive the evolution loop
    python scripts/run_feedback_jobs.py

    # Scan a wider window
    python scripts/run_feedback_jobs.py --days 2
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import re
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
from models.feedback import (
    ChatbotEvolutionAgentMode,
    ChatbotEvolutionAgentOutcome,
)
from services.chatbot_evolution_agent_service import ChatbotEvolutionAgentService
from services.qa_record_service import QARecordService
from tools.github_mcp_tools import get_github_issue_state
from utils.azure_ai_foundry import close_clients as close_ai_clients
from utils.azure_cosmosdb import close_cosmos_client
from utils.azure_credential import close_credential
from utils.azure_storage import close_storage_client, download_blob

logger = logging.getLogger("run_feedback_jobs")

_TESTING_CHANNEL_PATTERN = re.compile(r"\btesting\b", re.IGNORECASE)
_TESTING_CHANNEL_NAMES = {
    "azure sdk qa bot - auto reply - test",
    "smoke-tests",
}


def _is_testing_channel(name: str) -> bool:
    normalized = name.strip().casefold()
    return (
        _TESTING_CHANNEL_PATTERN.search(normalized) is not None
        or normalized in _TESTING_CHANNEL_NAMES
    )


async def _load_excluded_channels() -> set[str]:
    """Return channel ids whose configured display name marks test traffic."""
    container = app_config.get("STORAGE_CONFIG_CONTAINER")
    blob = app_config.get("CHANNEL_CONFIG_BLOB")
    if not container or not blob:
        raise RuntimeError("Storage channel configuration is not configured")
    data = await download_blob(container, blob)
    if not data:
        raise RuntimeError("Storage channel configuration is empty")
    try:
        parsed = yaml.safe_load(data.decode("utf-8")) or {}
    except (UnicodeDecodeError, yaml.YAMLError) as exc:
        raise RuntimeError("Storage channel configuration is invalid") from exc
    excluded: set[str] = set()
    for entry in parsed.get("channels", []) or []:
        channel_id = entry.get("id")
        name = entry.get("name") or ""
        if channel_id and _is_testing_channel(name):
            excluded.add(channel_id)
    return excluded


def _parse_dt(value: str) -> datetime:
    dt = datetime.fromisoformat(value)
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def _resolve_window(args: argparse.Namespace) -> tuple[datetime, datetime]:
    end = _parse_dt(args.end) if args.end else datetime.now(timezone.utc)
    start = _parse_dt(args.start) if args.start else end - timedelta(days=args.days)
    if start >= end:
        raise ValueError(f"start ({start}) must be before end ({end})")
    return start, end


async def _run(args: argparse.Namespace) -> None:
    qa_service = QARecordService()
    evolution_service = ChatbotEvolutionAgentService()

    start, end = _resolve_window(args)
    logger.info(
        "Scanning conversations active in [%s, %s)",
        start.isoformat(),
        end.isoformat(),
    )
    excluded_channels = await _load_excluded_channels()

    # 1. Ingest threads active in the window into the durable QA status table.
    messages = await qa_service.get_messages_in_period(start, end)
    touched = await qa_service.upsert_threads_from_messages(
        messages,
        excluded_channels=excluded_channels,
    )
    logger.info("Upserted %d QA thread record(s)", len(touched))

    # Keep the complete Agent workflow behind one configuration gate so it can
    # be disabled without changing the scheduled pipeline.
    enabled = (
        app_config.get("CHATBOT_EVOLUTION_AGENT_ENABLED", "false").strip().lower()
        == "true"
    )
    if args.dry_run or not enabled:
        logger.info("Agent processing disabled; ingestion completed only")
        return

    counts = {
        "ongoing": 0,
        "finished": 0,
        "issues": 0,
        "waiting_validation": 0,
        "validated": 0,
        "validation_failed": 0,
        "evolution_failed": 0,
        "skipped": 0,
    }

    # 2. Validate fixes first. Newly created issues from this run wait until
    # the next daily scan before their closure is checked.
    pending = await qa_service.list_pending_validation(tenant_id=args.tenant)
    for record in pending:
        if QARecordService.channel_key_of(record) in excluded_channels:
            counts["skipped"] += 1
            continue
        issue_url = record.feedback.issue_url if record.feedback else None
        if not issue_url:
            logger.error("Pending-validation record %s has no issue URL", record.id)
            counts["skipped"] += 1
            continue
        try:
            issue_state = await get_github_issue_state(issue_url)
        except Exception:
            logger.exception("Failed to read issue state for %s", record.id)
            counts["skipped"] += 1
            continue
        if issue_state != "closed":
            counts["waiting_validation"] += 1
            continue

        try:
            result = await evolution_service.run_job(
                record.id,
                record.tenant_id,
                mode=ChatbotEvolutionAgentMode.validation,
            )
        except Exception:
            logger.exception("Validation persistence failed for %s", record.id)
            counts["validation_failed"] += 1
            continue
        if result is None:
            counts["validation_failed"] += 1
        elif result.outcome == ChatbotEvolutionAgentOutcome.validation_passed:
            counts["validated"] += 1
        else:
            counts["validation_failed"] += 1

    # 3. Ask the Evolution Agent to evaluate and, when necessary, diagnose
    # each conversation. The pipeline does not make either decision itself.
    analyzable = await qa_service.list_analyzable(tenant_id=args.tenant)
    if args.limit is not None:
        analyzable = analyzable[: args.limit]
    for record in analyzable:
        if QARecordService.channel_key_of(record) in excluded_channels:
            counts["skipped"] += 1
            continue
        try:
            result = await evolution_service.run_job(
                record.id,
                record.tenant_id,
                mode=ChatbotEvolutionAgentMode.analysis,
            )
        except Exception:
            logger.exception("Analysis persistence failed for %s", record.id)
            counts["skipped"] += 1
            continue
        if result is None:
            counts["skipped"] += 1
        elif result.outcome in (
            ChatbotEvolutionAgentOutcome.processing_failed,
            ChatbotEvolutionAgentOutcome.remediation_failed,
        ):
            counts["evolution_failed"] += 1
        elif result.outcome == ChatbotEvolutionAgentOutcome.conversation_ongoing:
            counts["ongoing"] += 1
        elif result.outcome == ChatbotEvolutionAgentOutcome.no_issue:
            counts["finished"] += 1
        else:
            counts["issues"] += 1

    logger.info(
        "Evolution scan complete: ongoing=%d finished=%d issues=%d "
        "waiting-validation=%d validated=%d validation-failed=%d "
        "evolution-failed=%d skipped=%d",
        counts["ongoing"],
        counts["finished"],
        counts["issues"],
        counts["waiting_validation"],
        counts["validated"],
        counts["validation_failed"],
        counts["evolution_failed"],
        counts["skipped"],
    )


async def main() -> None:
    parser = argparse.ArgumentParser(
        description="Ingest QA conversations and run the evolution loop.",
    )
    parser.add_argument(
        "--days",
        type=int,
        default=1,
        help="Look back this many days (default: 1).",
    )
    parser.add_argument("--start", type=str, default=None, help="Window start.")
    parser.add_argument("--end", type=str, default=None, help="Window end.")
    parser.add_argument("--tenant", type=str, default=None)
    parser.add_argument("--limit", type=int, default=None)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Ingest records without invoking agents or checking issues.",
    )
    parser.add_argument("--verbose", "-v", action="store_true")
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
