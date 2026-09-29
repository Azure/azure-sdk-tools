"""Periodic Q&A summarization of Teams threads held in the conversation store.

Threads are discovered from ``conversation-messages`` and summaries land in a
separate ``teams-qa-summaries`` container, so summarization never rewrites the
messages the bot owns. Only threads that yield reusable Q&A are stored, which
makes every document in the container an answer a reader can use directly.

Freshness is decided in two steps. A cheap projection of every message in the
channel gives a per-thread message count and the highest Cosmos ``_ts`` which
moves on any insert or edit; only threads whose gate drifted are read in full.
The content digest of that full read then decides whether the model actually
has to run again.

That gate lives in the stored summary, so it only covers threads that produced
one. A rejected thread leaves no record and is offered to the model again on
every run, which keeps the container clean at the cost of re-judging threads
whose verdict rarely changes.
"""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone

from azure.cosmos import exceptions

from models.conversation import ConversationDocumentType
from services.conversation_service import (
    channel_partition_prefix,
    root_id_from_partition,
    select_channels,
    split_subject,
    validate_channels,
)

SUMMARY_DOCUMENT_TYPE = "teams_thread_qa_summary"


def thread_digest(messages: list[dict]) -> str:
    return hashlib.sha256(json.dumps(
        [[message.get("id"), message.get("sender_role"), message.get("content")]
         for message in messages],
        ensure_ascii=False, separators=(",", ":"),
    ).encode("utf-8")).hexdigest()


def _as_datetime(value: object) -> datetime | None:
    if isinstance(value, datetime):
        return value if value.tzinfo else value.replace(tzinfo=timezone.utc)
    if not isinstance(value, str) or not value:
        return None
    text = value[:-1] + "+00:00" if value.endswith("Z") else value
    try:
        parsed = datetime.fromisoformat(text)
    except ValueError:
        return None
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)


def _iso_utc(value: object) -> str | None:
    """Normalize a timestamp to one sortable UTC spelling.

    ``conversation-messages`` keeps whatever the bot serialized, so a message whose
    microseconds land on zero is stored as ``...:05Z`` while its neighbours are stored
    as ``...:05.325000Z``. Cosmos compares those as plain strings and ``Z`` sorts after
    ``.``, which drops the short form out of a range filter. Summaries own their
    container, so every timestamp written here gets the six-digit form and a range
    query over them can use a single boundary spelling.
    """
    parsed = _as_datetime(value)
    if parsed is None:
        return value if isinstance(value, str) else None
    return parsed.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f") + "Z"


def _ordered(messages: list[dict]) -> list[dict]:
    """Oldest first, comparing instants rather than their spelling."""
    floor = datetime.min.replace(tzinfo=timezone.utc)
    return sorted(messages, key=lambda message: (
        _as_datetime(message.get("created_at")) or floor, message.get("id") or ""
    ))


def _prompt_message(message: dict, *, subject: str | None = None,
                    content: str | None = None) -> dict:
    prompt = {
        "id": message.get("id"),
        "sender_role": message.get("sender_role"),
        "sender_name": message.get("sender_name"),
        "created_at": message.get("created_at"),
        "content": message.get("content") if content is None else content,
    }
    if subject is not None:
        prompt["subject"] = subject
    return prompt


class TeamsQASummaryService:
    """Turn stored Teams threads into reusable Q&A, once per meaningful change."""

    def __init__(self, messages_container, summaries_container, processor):
        if processor is None:
            raise RuntimeError("A Teams thread processor is required for summarization.")
        self._messages = messages_container
        self._summaries = summaries_container
        self._processor = processor

    async def _thread_gates(self, channel_id: str) -> dict[str, dict]:
        """Return per-thread message count and latest write timestamp."""
        gates: dict[str, dict] = {}
        async for row in self._messages.query_items(
            query=("SELECT c.conversation_partition AS partition, c._ts AS ts FROM c "
                   "WHERE c.document_type = @dtype "
                   "AND STARTSWITH(c.conversation_partition, @prefix)"),
            parameters=[
                {"name": "@dtype", "value": ConversationDocumentType.message.value},
                {"name": "@prefix", "value": channel_partition_prefix(channel_id)},
            ],
        ):
            partition = row.get("partition")
            if not partition:
                continue
            gate = gates.setdefault(partition, {"message_count": 0, "last_write_ts": 0})
            gate["message_count"] += 1
            gate["last_write_ts"] = max(gate["last_write_ts"], row.get("ts") or 0)
        return gates

    async def _existing_summaries(self, channel_id: str) -> dict[str, dict]:
        summaries: dict[str, dict] = {}
        async for raw in self._summaries.query_items(
            query="SELECT * FROM c WHERE c.document_type = @dtype",
            parameters=[{"name": "@dtype", "value": SUMMARY_DOCUMENT_TYPE}],
            partition_key=channel_id,
        ):
            summaries[raw["id"]] = raw
        return summaries

    async def _thread_messages(self, partition: str) -> list[dict]:
        messages = [raw async for raw in self._messages.query_items(
            query="SELECT * FROM c WHERE c.document_type = @dtype",
            parameters=[
                {"name": "@dtype", "value": ConversationDocumentType.message.value},
            ],
            partition_key=partition,
        )]
        return _ordered(messages)

    def _is_fresh(self, summary: dict | None, gate: dict) -> bool:
        """True when neither the thread nor the processor moved since last time.

        Passing the summary's own digest back into ``is_current`` reduces that
        call to a processor name and version check.
        """
        return bool(
            summary
            and summary.get("message_count") == gate["message_count"]
            and summary.get("last_write_ts") == gate["last_write_ts"]
            and self._processor.is_current(summary, summary.get("source_content_hash"))
        )

    async def _discard(self, channel_id: str, root_id: str, previous: dict | None) -> None:
        """Forget a thread the processor rejected.

        A rejected thread leaves nothing behind, so one that used to produce a Q&A
        and no longer does has its stale answer removed rather than left to be read
        as current.
        """
        if previous is None:
            return
        try:
            await self._summaries.delete_item(item=root_id, partition_key=channel_id)
        except exceptions.CosmosResourceNotFoundError:
            pass

    def _document(self, channel: dict, root: dict, partition: str,
                  messages: list[dict], gate: dict, decision: dict) -> dict:
        extra_info = root.get("extra_info")
        # Only accepted threads reach the container, so the fields that routed the
        # decision would be a constant and a perpetual null once stored.
        accepted = {key: value for key, value in decision.items()
                    if key not in ("status", "exclusion_reason")}
        document = {
            "id": root["id"],
            "channel_id": channel["channelId"],
            "team_id": channel["teamId"],
            "tenant_id": channel["tenantKey"],
            "document_type": SUMMARY_DOCUMENT_TYPE,
            "post_id": root["id"],
            "conversation_partition": partition,
            "message_link": (extra_info or {}).get("message_link"),
            "message_count": gate["message_count"],
            "last_write_ts": gate["last_write_ts"],
            "thread_started_at": _iso_utc(root.get("created_at")),
            "last_message_at": _iso_utc(messages[-1].get("created_at")),
            **accepted,
        }
        # The processor stamps its own run time; restate it in the container's
        # spelling so every timestamp here answers to the same range boundary.
        document["processed_at"] = _iso_utc(document.get("processed_at"))
        return document

    async def summarize(self, channels: list[dict]) -> dict:
        validate_channels(channels)
        summary = {"channelsCompleted": 0, "threadsScanned": 0, "threadsSummarized": 0,
                   "threadsUnchanged": 0, "threadsRefreshed": 0, "threadsExcluded": 0,
                   "threadsSkipped": 0}
        for channel in channels:
            channel_id = channel["channelId"]
            gates = await self._thread_gates(channel_id)
            existing = await self._existing_summaries(channel_id)
            for partition, gate in sorted(gates.items()):
                summary["threadsScanned"] += 1
                root_id = root_id_from_partition(partition)
                if root_id is None:
                    summary["threadsSkipped"] += 1
                    continue
                previous = existing.get(root_id)
                if self._is_fresh(previous, gate):
                    summary["threadsUnchanged"] += 1
                    continue
                messages = await self._thread_messages(partition)
                root = next((item for item in messages if item.get("id") == root_id), None)
                if root is None:
                    # Without the root post the thread cannot be framed as a
                    # question; leave it for the next backfill.
                    summary["threadsSkipped"] += 1
                    continue
                replies = [item for item in messages if item is not root]
                messages = [root, *replies]
                digest = thread_digest(messages)
                if previous is not None and self._processor.is_current(previous, digest):
                    refreshed = {**previous, "message_count": gate["message_count"],
                                 "last_write_ts": gate["last_write_ts"]}
                    await self._summaries.upsert_item(body=refreshed)
                    summary["threadsRefreshed"] += 1
                    continue
                subject, body = split_subject(root.get("content") or "")
                decision = await self._processor.process(
                    channel,
                    _prompt_message(root, subject=subject, content=body),
                    [_prompt_message(reply) for reply in replies],
                    digest,
                )
                if decision.get("qa") is None:
                    await self._discard(channel_id, root_id, previous)
                    summary["threadsExcluded"] += 1
                    continue
                await self._summaries.upsert_item(
                    body=self._document(channel, root, partition, messages, gate, decision)
                )
                summary["threadsSummarized"] += 1
            summary["channelsCompleted"] += 1
        return summary


async def summarize_configured_channels(config, processor, channel_id=None):
    from utils.azure_cosmosdb import (
        get_conversation_message_container,
        get_teams_qa_summaries_container,
    )

    channels = select_channels(config["channels"], channel_id)
    service = TeamsQASummaryService(
        await get_conversation_message_container(),
        await get_teams_qa_summaries_container(),
        processor,
    )
    return await service.summarize(channels)
