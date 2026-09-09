"""Aggregate conversation messages and query QA-record workflow states.

Aggregates per-message rows into one ``QARecord`` per thread. New threads with
a bot answer start as ``ongoing``; the Chatbot Evolution Agent owns subsequent
evaluation and lifecycle transitions.
"""

from __future__ import annotations

import logging
from datetime import datetime, timezone
from typing import Sequence

from models.conversation import (
    ConversationDocumentType,
    ConversationMessageItem,
    Role,
)
from models.qa_record import FeedbackStatus, QARecord, QAStatus
from utils.azure_cosmosdb import (
    get_conversation_message_container,
    query_qa_records_by_qa_status,
    query_qa_records_by_feedback_status,
    read_qa_record,
    upsert_qa_record,
)

logger = logging.getLogger(__name__)


class QARecordService:
    """Build QA records from messages and expose workflow queries."""

    # -- Aggregation: messages -> QA records -------------------------------

    async def get_messages_in_period(
        self,
        start: datetime,
        end: datetime,
    ) -> list[ConversationMessageItem]:
        """Return full threads with a bot message in the scan window."""
        if start.tzinfo is not None:
            start = start.astimezone(timezone.utc)
        if end.tzinfo is not None:
            end = end.astimezone(timezone.utc)
        start = start.replace(hour=0, minute=0, second=0, microsecond=0)
        container = await get_conversation_message_container()

        # Cosmos cannot aggregate this cross-partition query reliably, so first
        # collect qualifying partitions, then fetch each complete thread.
        window_query = (
            "SELECT c.conversation_partition AS partition FROM c "
            "WHERE c.document_type = @dtype "
            "AND ARRAY_CONTAINS(@bot_roles, c.sender_role) "
            "AND c.created_at >= @start AND c.created_at < @end"
        )
        window_params: list[dict[str, object]] = [
            {
                "name": "@dtype",
                "value": ConversationDocumentType.message.value,
            },
            {
                "name": "@bot_roles",
                "value": [Role.System.value, Role.Assistant.value],
            },
            {"name": "@start", "value": start.isoformat()},
            {"name": "@end", "value": end.isoformat()},
        ]
        partitions: set[str] = set()
        async for row in container.query_items(
            query=window_query,
            parameters=window_params,
        ):
            partition = row.get("partition")
            if partition:
                partitions.add(partition)
        if not partitions:
            return []

        # Return every message from each qualifying partition, including
        # messages outside the activity window, so the Agent sees full context.
        messages_query = (
            "SELECT * FROM c "
            "WHERE c.document_type = @dtype "
            "AND ARRAY_CONTAINS(@partitions, c.conversation_partition)"
        )
        messages_params: list[dict[str, object]] = [
            {
                "name": "@dtype",
                "value": ConversationDocumentType.message.value,
            },
            {"name": "@partitions", "value": sorted(partitions)},
        ]
        items: list[ConversationMessageItem] = []
        async for raw in container.query_items(
            query=messages_query,
            parameters=messages_params,
        ):
            items.append(ConversationMessageItem.model_validate(raw))
        return sorted(items, key=lambda message: message.created_at)

    async def upsert_threads_from_messages(
        self,
        messages: Sequence[ConversationMessageItem],
        *,
        excluded_channels: set[str] | None = None,
    ) -> list[QARecord]:
        """Insert/refresh QA records for every thread present in ``messages``.

        Threads without a bot answer are skipped (nothing to judge), as are
        threads in ``excluded_channels`` (e.g. testing channels). Existing
        records that already concluded (``finished`` / ``failed``) are left
        untouched; ``ongoing`` records get their latest-bot-turn metadata
        refreshed. Returns the records touched.
        """
        excluded_channels = excluded_channels or set()
        groups: dict[str, list[ConversationMessageItem]] = {}
        for message in messages:
            groups.setdefault(message.conversation_partition, []).append(message)
        touched: list[QARecord] = []
        for _partition, items in groups.items():
            candidate = self.build_record(items)
            if candidate is None:
                continue
            if candidate.channel_id and candidate.channel_id in excluded_channels:
                continue

            existing_doc = await read_qa_record(
                record_id=candidate.id, tenant_id=candidate.tenant_id
            )
            if existing_doc is None:
                await upsert_qa_record(candidate.to_cosmos())
                touched.append(candidate)
                continue

            existing = QARecord.from_cosmos(existing_doc)
            needs_creation_time_backfill = (
                existing.conversation_created_at is None
                and candidate.conversation_created_at is not None
            )
            if needs_creation_time_backfill:
                existing.conversation_created_at = candidate.conversation_created_at
            if existing.qa_status in (QAStatus.finished, QAStatus.failed):
                # Concluded threads are immutable to the scanner.
                if needs_creation_time_backfill:
                    await upsert_qa_record(existing.to_cosmos())
                touched.append(existing)
                continue

            # Refresh the ongoing thread's latest-turn metadata.
            existing.message_link = candidate.message_link or existing.message_link
            existing.message_count = candidate.message_count
            existing.has_expert_reply = candidate.has_expert_reply
            existing.last_activity_at = candidate.last_activity_at
            existing.updated_at = _now()
            await upsert_qa_record(existing.to_cosmos())
            touched.append(existing)
        return touched

    def build_record(
        self, items: Sequence[ConversationMessageItem]
    ) -> QARecord | None:
        """Assemble a candidate ``ongoing`` QA record from a thread's messages.

        Requires the thread to have at least one bot answer (otherwise it is
        not a QA thread); returns ``None`` when there is no bot answer or the
        coordinates needed to key the record are missing.
        """
        has_bot_answer = any(
            m.sender_role.value in ("system", "assistant") for m in items
        )
        if not has_bot_answer:
            return None

        conversation_id = next(
            (m.conversation_id for m in items if m.conversation_id), None
        )
        conversation_type = next(
            (m.conversation_type for m in items if m.conversation_type), None
        )
        if not conversation_id or conversation_type is None:
            return None

        tenant_id = next(
            (m.tenant_id for m in items if m.tenant_id), None
        ) or "unknown"

        ordered = sorted(items, key=lambda m: m.created_at)
        poster_id = next(
            (m.sender_id for m in ordered if m.sender_role.value == "user"), None
        )
        has_expert_reply = any(
            m.sender_role.value == "user"
            and poster_id is not None
            and m.sender_id != poster_id
            for m in ordered
        )

        now = _now()
        return QARecord(
            id=QARecord.build_id(conversation_type, conversation_id),
            tenant_id=tenant_id,
            conversation_id=conversation_id,
            conversation_type=conversation_type,
            channel_id=self._channel_key(items, conversation_id),
            message_link=self._message_link(items),
            qa_status=QAStatus.ongoing,
            has_expert_reply=has_expert_reply,
            message_count=len(ordered),
            conversation_created_at=ordered[0].created_at if ordered else None,
            last_activity_at=ordered[-1].created_at if ordered else None,
            first_seen_at=now,
            created_at=now,
            updated_at=now,
        )

    @staticmethod
    def _channel_key(
        items: Sequence[ConversationMessageItem],
        conversation_id: str | None = None,
    ) -> str | None:
        """Derive the Teams channel id a thread belongs to.

        Prefers a message's ``extra_info.channel_id`` and falls back to the
        channel segment of ``conversation_id`` (everything before the
        ``;messageid=`` root-message suffix).
        """
        for m in items:
            channel_id = m.extra_info.channel_id if m.extra_info else None
            if channel_id:
                return channel_id
        for m in items:
            if m.conversation_id:
                return m.conversation_id.split(";messageid=", 1)[0]
        if conversation_id:
            return conversation_id.split(";messageid=", 1)[0]
        return None

    @staticmethod
    def channel_key_of(record: QARecord) -> str | None:
        """Return the channel id of an existing QA record for exclusion checks."""
        if record.channel_id:
            return record.channel_id
        if record.conversation_id:
            return record.conversation_id.split(";messageid=", 1)[0]
        return None

    @staticmethod
    def _message_link(items: Sequence[ConversationMessageItem]) -> str | None:
        """Prefer a stored Teams permalink from any message's extra_info."""
        for m in sorted(items, key=lambda x: x.created_at):
            link = getattr(m.extra_info, "message_link", None) if m.extra_info else None
            if link:
                return link
        return None

    # -- Queries -----------------------------------------------------------

    async def list_analyzable(
        self,
        *,
        tenant_id: str | None = None,
    ) -> list[QARecord]:
        """Return new and failed records eligible for Agent analysis."""
        docs = await query_qa_records_by_qa_status(
            qa_status=QAStatus.ongoing.value,
            tenant_id=tenant_id,
        )
        failed_docs = await query_qa_records_by_feedback_status(
            feedback_status=FeedbackStatus.failed.value,
            tenant_id=tenant_id,
        )
        records = {
            (record.tenant_id, record.id): record
            for record in (
                QARecord.from_cosmos(doc) for doc in [*docs, *failed_docs]
            )
        }.values()
        return [
            record
            for record in records
            if record.feedback is None
            or (
                record.feedback.status == FeedbackStatus.failed
                and not record.feedback.issue_url
            )
        ]

    async def list_pending_validation(
        self,
        *,
        tenant_id: str | None = None,
    ) -> list[QARecord]:
        """Return pending and failed remediation-issue validations."""
        docs = await query_qa_records_by_feedback_status(
            feedback_status=FeedbackStatus.pending_validation.value,
            tenant_id=tenant_id,
        )
        failed_docs = await query_qa_records_by_feedback_status(
            feedback_status=FeedbackStatus.failed.value,
            tenant_id=tenant_id,
        )
        records = {
            (record.tenant_id, record.id): record
            for record in (
                QARecord.from_cosmos(doc) for doc in [*docs, *failed_docs]
            )
        }.values()
        return [
            record
            for record in records
            if record.feedback and record.feedback.issue_url
        ]


def _now() -> datetime:
    return datetime.now(timezone.utc)


__all__ = ["QARecordService"]
