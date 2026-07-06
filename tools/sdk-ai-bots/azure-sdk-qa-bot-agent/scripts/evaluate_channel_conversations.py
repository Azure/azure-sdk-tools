"""Scheduled channel-conversation quality evaluation.

Reads channel conversation threads over a time window from Cosmos DB and asks
an LLM to judge each bot answer as ``correct``, ``incorrect``, or ``unknown`` —
using the user's question and any subsequent human expert reply as context.

Results are logged and written as a JSON artifact suitable for a pipeline to
publish.

Usage::

    # Evaluate the last 7 days (default) and write the artifact
    python scripts/evaluate_channel_conversations.py

    # Evaluate the last 1 day
    python scripts/evaluate_channel_conversations.py --days 1

    # Evaluate an explicit window (UTC, ISO-8601)
    python scripts/evaluate_channel_conversations.py \
        --start 2026-07-01T00:00:00 --end 2026-07-02T00:00:00

    # Cap the number of threads and choose the artifact path
    python scripts/evaluate_channel_conversations.py --limit 20 --output out/eval.json
"""

from __future__ import annotations

import argparse
import asyncio
import json
import logging
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Sequence
from urllib.parse import quote

import yaml
from dotenv import load_dotenv

_PROJECT_DIR = Path(__file__).resolve().parent.parent
load_dotenv(_PROJECT_DIR / ".env", override=False)

if str(_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(_PROJECT_DIR))

import config.app_config as app_config
from models.conversation import (
    BotAnswerVerdict,
    ConversationEvaluationItem,
    ConversationMessageItem,
)
from services.conversation_service import ConversationService
from utils.azure_ai_foundry import close_clients as close_ai_clients
from utils.azure_cosmosdb import close_cosmos_client
from utils.azure_credential import close_credential
from utils.azure_storage import close_storage_client, download_blob

logger = logging.getLogger("evaluate_channel_conversations")

_DEFAULT_OUTPUT = _PROJECT_DIR / "channel_conversation_evaluation.json"

# Fixed Microsoft tenant used when constructing Teams message deep links,
# matching the Logic App's MessageLink formula.
_TEAMS_TENANT_ID = "72f988bf-86f1-41af-91ab-2d7cd011db47"

# Blob holding the channel_id -> friendly name mapping.
_CHANNEL_CONFIG_CONTAINER = "bot-configs"
_CHANNEL_CONFIG_BLOB = "channel.yaml"


async def _load_channel_names() -> dict[str, str]:
    """Load the ``channel_id`` -> display ``name`` map from ``channel.yaml``.

    Returns an empty map (and logs a warning) when the blob is missing or
    malformed so the summary can still fall back to raw channel ids.
    """
    try:
        data = await download_blob(_CHANNEL_CONFIG_CONTAINER, _CHANNEL_CONFIG_BLOB)
    except Exception:
        logger.warning("Failed to download channel.yaml", exc_info=True)
        return {}
    if not data:
        return {}
    try:
        parsed = yaml.safe_load(data.decode("utf-8")) or {}
    except yaml.YAMLError:
        logger.warning("Failed to parse channel.yaml", exc_info=True)
        return {}
    names: dict[str, str] = {}
    for entry in parsed.get("channels", []) or []:
        channel_id = entry.get("id")
        name = entry.get("name")
        if channel_id and name:
            names[channel_id] = name
    return names


def _parse_dt(value: str) -> datetime:
    """Parse an ISO-8601 string into a timezone-aware UTC datetime."""
    dt = datetime.fromisoformat(value)
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def _resolve_window(args: argparse.Namespace) -> tuple[datetime, datetime]:
    """Determine the [start, end) window from CLI args."""
    end = _parse_dt(args.end) if args.end else datetime.now(timezone.utc)
    if args.start:
        start = _parse_dt(args.start)
    else:
        start = end - timedelta(days=args.days)
    if start >= end:
        raise ValueError(f"start ({start}) must be before end ({end})")
    return start, end


def _resolve_teams_link(
    messages: Sequence[ConversationMessageItem],
) -> str | None:
    """Resolve a Teams link pointing at the root of the conversation.

    Preference order:
    1. The stored ``message_link`` of any message (the permalink the Logic App
       captured from Teams).
    2. A constructed Teams channel deep link, mirroring the Logic App
       ``MessageLink`` formula::

           https://teams.microsoft.com/l/message/{escaped channel_id}/{parent_message_id}
               ?tenantId={tenant}&parentMessageId={parent_message_id}

       Only ``channel_id`` is URL-escaped; ``parent_message_id`` (the root
       message id, i.e. the conversation's ``conversation_id``) is left raw.

    Returns ``None`` when neither a stored link nor enough identifiers exist.
    """
    ordered = sorted(messages, key=lambda m: m.created_at)

    for m in ordered:
        link = getattr(m.extra_info, "message_link", None) if m.extra_info else None
        if link:
            return link

    channel_id = next(
        (
            m.extra_info.channel_id
            for m in ordered
            if m.extra_info and m.extra_info.channel_id
        ),
        None,
    )
    parent_message_id = next(
        (m.conversation_id for m in ordered if m.conversation_id), None
    )
    if not channel_id or not parent_message_id:
        return None

    channel_segment = quote(channel_id, safe="")
    return (
        "https://teams.microsoft.com/l/message/"
        f"{channel_segment}/{parent_message_id}"
        f"?tenantId={_TEAMS_TENANT_ID}"
        f"&parentMessageId={parent_message_id}"
    )


async def _run_evaluation(
    service: ConversationService,
    start: datetime,
    end: datetime,
    *,
    limit: int | None,
) -> list[ConversationEvaluationItem]:
    """Evaluate every conversation that started in the window.

    The channel-agnostic verdict comes from
    :meth:`ConversationService.evaluate_conversation`; this function adds the
    Teams deep link and returns the evaluated threads.
    """
    messages = await service.get_messages_in_period(start, end)
    conversations = ConversationService.group_by_conversation(messages)
    total_threads = len(conversations)

    threads: list[ConversationEvaluationItem] = []
    skipped = 0
    for partition, items in conversations.items():
        if limit is not None and len(threads) >= limit:
            break

        try:
            conv_eval = await service.evaluate_conversation(items)
        except Exception:
            logger.warning(
                "Evaluation failed for conversation=%s", partition, exc_info=True
            )
            skipped += 1
            continue

        if conv_eval is None:
            skipped += 1
            continue

        conv_eval.message_link = _resolve_teams_link(items)
        threads.append(conv_eval)

    correct = sum(1 for t in threads if t.verdict == BotAnswerVerdict.Correct)
    incorrect = sum(1 for t in threads if t.verdict == BotAnswerVerdict.Incorrect)
    unknown = len(threads) - correct - incorrect
    logger.info(
        "Evaluation run complete: conversations=%d evaluated=%d skipped=%d "
        "correct=%d incorrect=%d unknown=%d",
        total_threads,
        len(threads),
        skipped,
        correct,
        incorrect,
        unknown,
    )
    return threads

def _channel_key(item: ConversationEvaluationItem) -> str:
    """Extract the Teams channel identity a conversation belongs to.

    Conversation ids look like ``19:<thread>@thread.tacv2;messageid=<root>``;
    the channel is everything before the ``;messageid=`` root-message suffix.
    Falls back to the partition when the id is not in that shape.
    """
    base = item.conversation_id or item.conversation_partition
    return base.split(";messageid=", 1)[0]


def _log_summary(
    threads: Sequence[ConversationEvaluationItem],
    start: datetime,
    end: datetime,
    channel_names: dict[str, str] | None = None,
) -> None:
    """Print a human-readable summary of the run, grouped by channel."""
    channel_names = channel_names or {}
    correct = sum(
        1 for t in threads if t.verdict == BotAnswerVerdict.Correct
    )
    incorrect = sum(
        1 for t in threads if t.verdict == BotAnswerVerdict.Incorrect
    )
    unknown = len(threads) - correct - incorrect

    channels: dict[str, list[ConversationEvaluationItem]] = {}
    for t in threads:
        channels.setdefault(_channel_key(t), []).append(t)

    logger.info("=" * 60)
    logger.info(
        "Channel conversation evaluation — window %s .. %s",
        start.isoformat(),
        end.isoformat(),
    )
    logger.info(
        "Channels: %d | Evaluated threads: %d", len(channels), len(threads)
    )
    logger.info(
        "Verdicts — correct: %d | incorrect: %d | unknown: %d",
        correct,
        incorrect,
        unknown,
    )
    if threads:
        rate = 100.0 * correct / len(threads)
        logger.info("Correct rate (of evaluated): %.1f%%", rate)
    logger.info("=" * 60)

    for c_index, (channel, items) in enumerate(channels.items(), 1):
        c_correct = sum(1 for t in items if t.verdict == BotAnswerVerdict.Correct)
        c_incorrect = sum(
            1 for t in items if t.verdict == BotAnswerVerdict.Incorrect
        )
        c_unknown = len(items) - c_correct - c_incorrect
        c_rate = 100.0 * c_correct / len(items) if items else 0.0
        channel_label = channel_names.get(channel, channel)
        logger.info("=" * 60)
        logger.info(
            "Channel %d/%d — %s (%s)",
            c_index,
            len(channels),
            channel_label,
            channel,
        )
        logger.info(
            "  threads: %d | correct: %d | incorrect: %d | unknown: %d | correct rate: %.1f%%",
            len(items),
            c_correct,
            c_incorrect,
            c_unknown,
            c_rate,
        )
        for i, t in enumerate(items, 1):
            logger.info("-" * 60)
            logger.info(
                "  Thread %d/%d — [%s] confidence=%.2f",
                i,
                len(items),
                t.verdict.value.upper(),
                t.confidence,
            )
            logger.info("    conversation_id : %s", t.conversation_id)
            logger.info("    messages        : %d", t.message_count)
            logger.info(
                "    expert involved : %s", "yes" if t.has_expert_reply else "no"
            )
            logger.info(
                "    teams link      : %s", t.message_link or "(unavailable)"
            )
            logger.info("    reasoning       : %s", t.reasoning)


def _write_artifact(
    threads: Sequence[ConversationEvaluationItem],
    output: Path,
    channel_names: dict[str, str] | None = None,
) -> None:
    """Write the evaluation results as a JSON array grouped by channel."""
    channel_names = channel_names or {}
    output.parent.mkdir(parents=True, exist_ok=True)

    channels: dict[str, list[ConversationEvaluationItem]] = {}
    for t in threads:
        channels.setdefault(_channel_key(t), []).append(t)

    groups = []
    for channel, items in channels.items():
        correct = sum(1 for t in items if t.verdict == BotAnswerVerdict.Correct)
        incorrect = sum(
            1 for t in items if t.verdict == BotAnswerVerdict.Incorrect
        )
        unknown = len(items) - correct - incorrect
        groups.append(
            {
                "channel_id": channel,
                "channel_name": channel_names.get(channel, channel),
                "summary": {
                    "threads": len(items),
                    "correct": correct,
                    "incorrect": incorrect,
                    "unknown": unknown,
                    "correct_rate": round(100.0 * correct / len(items), 1)
                    if items
                    else 0.0,
                },
                "conversations": [t.model_dump(mode="json") for t in items],
            }
        )

    output.write_text(
        json.dumps(groups, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    logger.info("Wrote evaluation artifact: %s", output)



async def main() -> None:
    parser = argparse.ArgumentParser(
        description="Evaluate channel conversation quality over a time window.",
    )
    parser.add_argument(
        "--days",
        type=int,
        default=7,
        help="Look back this many days from now (ignored if --start is set). Default: 7",
    )
    parser.add_argument(
        "--start",
        type=str,
        default=None,
        help="Inclusive window start (UTC, ISO-8601, e.g. 2026-07-01T00:00:00).",
    )
    parser.add_argument(
        "--end",
        type=str,
        default=None,
        help="Exclusive window end (UTC, ISO-8601). Default: now.",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=None,
        help="Maximum number of threads to evaluate.",
    )
    parser.add_argument(
        "--output",
        "-o",
        type=str,
        default=str(_DEFAULT_OUTPUT),
        help=f"Path to write the JSON artifact. Default: {_DEFAULT_OUTPUT}",
    )
    parser.add_argument(
        "--verbose",
        "-v",
        action="store_true",
        help="Enable debug logging.",
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stdout,
    )
    logging.getLogger("azure.core").setLevel(logging.WARNING)
    logging.getLogger("azure.identity").setLevel(logging.WARNING)

    start, end = _resolve_window(args)

    await app_config.init()

    try:
        service = ConversationService()
        threads = await _run_evaluation(service, start, end, limit=args.limit)

        channel_names = await _load_channel_names()
        _log_summary(threads, start, end, channel_names)
        _write_artifact(threads, Path(args.output), channel_names)
    finally:
        await _close_clients()


async def _close_clients() -> None:
    """Close the shared async Azure clients to avoid unclosed-session errors."""
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
