"""Aggregate conversation messages and query QA-record workflow states.

Aggregates per-message rows into one ``QARecord`` per thread. New threads with
a bot answer start as ``ongoing``; the Chatbot Evolution Agent owns subsequent
evaluation, except for automatic closure after 144 hours of inactivity.
"""

from __future__ import annotations

import logging
from datetime import datetime, timedelta, timezone
from typing import Sequence

from models.conversation import (
    BotAnswerVerdict,
    ConversationDocumentType,
    ConversationMessageItem,
)
from models.qa_record import FeedbackStatus, QARecord, QAStatus
from services.conversation_service import ConversationService
from utils.channel_policy import is_channel_conversation_id
from utils.azure_cosmosdb import (
    get_conversation_message_container,
    query_qa_records_by_qa_status,
    query_qa_records_by_feedback_status,
    read_qa_record,
    replace_qa_record_if_unchanged,
    upsert_qa_record,
)

logger = logging.getLogger(__name__)

# Threads without activity for at least this long are automatically
# marked as finished/correct without Agent review.
_INACTIVE_THREAD_TIMEOUT = timedelta(hours=144)


class QARecordService:
    """Aggregate conversation messages into QA records and query lifecycle states."""

    def __init__(self) -> None:
        self._conversation_service = ConversationService()

    # -- Message ingestion -------------------------------------------------

    async def get_recent_messages(
        self,
        *,
        since: datetime,
        tenant_id: str | None = None,
    ) -> list[ConversationMessageItem]:
        """Return all conversation messages created since ``since``."""
        container = await get_conversation_message_container()
        query = (
            "SELECT * FROM c "
            "WHERE c.document_type = @dtype "
            "AND c.created_at >= @since"
        )
        parameters: list[dict[str, object]] = [
            {
                "name": "@dtype",
                "value": ConversationDocumentType.message.value,
            },
            {"name": "@since", "value": since.isoformat()},
        ]
        if tenant_id:
            query += " AND c.tenant_id = @tenant_id"
            parameters.append({"name": "@tenant_id", "value": tenant_id})
        items: list[ConversationMessageItem] = []
        async for raw in container.query_items(
            query=query,
            parameters=parameters,
        ):
            items.append(ConversationMessageItem.model_validate(raw))
        return sorted(items, key=lambda message: message.created_at)

    async def get_thread_messages(
        self,
        partitions: set[str],
    ) -> list[ConversationMessageItem]:
        """Return all messages belonging to the given thread partition keys."""
        container = await get_conversation_message_container()
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
        Agent-concluded records are left untouched. Automatically closed records
        reopen on new activity; ongoing records get their thread metadata
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
            if candidate.channel_id and not is_channel_conversation_id(
                candidate.channel_id
            ):
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

            if existing.auto_closed_at is not None:
                # Reopen: new activity arrived after the auto-close window.
                existing.qa_status = QAStatus.ongoing
                existing.verdict = BotAnswerVerdict.Unknown
                existing.auto_closed_at = None
                existing.reasoning = None
                existing.confidence = None
                existing.updated_at = _now()
                await upsert_qa_record(existing.to_cosmos())
                touched.append(existing)
                logger.info(
                    "Reopened auto-closed QA record %s after new activity",
                    existing.id,
                )
                continue

            if (
                existing.qa_status == QAStatus.ongoing
                and existing.feedback is None
            ) or needs_creation_time_backfill:
                # Refresh thread metadata for ongoing records not yet picked
                # up by the Agent, or backfill creation time.
                existing.last_activity_at = candidate.last_activity_at
                existing.message_count = candidate.message_count
                if candidate.channel_id:
                    existing.channel_id = candidate.channel_id
                if candidate.message_link:
                    existing.message_link = candidate.message_link
                existing.updated_at = _now()
                await upsert_qa_record(existing.to_cosmos())
                touched.append(existing)

        return touched

    @staticmethod
    def build_record(
        items: Sequence[ConversationMessageItem],
    ) -> QARecord | None:
        """Derive a ``QARecord`` from raw conversation messages."""
        if not items:
            return None
        has_bot = any(
            m.sender_role in ("assistant", "system")
            for m in items
        )
        if not has_bot:
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
        )
        if not tenant_id:
            return None

        ordered = sorted(items, key=lambda m: m.created_at)
        now = _now()
        has_expert_reply = any(
            m.sender_role == "user"
            and getattr(m, "sender_id", None)
            and m.sender_id != ordered[0].sender_id
            for m in ordered
            if m.sender_role == "user"
        )

        return QARecord(
            id=QARecord.build_id(conversation_type, conversation_id),
            tenant_id=tenant_id,
            conversation_id=conversation_id,
            conversation_type=conversation_type,
            channel_id=QARecordService._channel_key(items, conversation_id),
            message_link=QARecordService._message_link(items),
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

    async def close_inactive_threads(
        self,
        *,
        tenant_id: str | None = None,
        excluded_channels: set[str] | None = None,
    ) -> int:
        """Auto-close inactive ongoing threads regardless of user feedback."""
        now = _now()
        cutoff = now - _INACTIVE_THREAD_TIMEOUT
        excluded_channels = excluded_channels or set()
        docs = await query_qa_records_by_qa_status(
            qa_status=QAStatus.ongoing.value, tenant_id=tenant_id
        )
        conversations = ConversationService()
        closed = 0
        for doc in docs:
            record = QARecord.from_cosmos(doc)
            if (
                record.qa_status != QAStatus.ongoing
                or record.feedback is not None
                or self.channel_key_of(record) in excluded_channels
                or (
                    record.last_activity_at is not None
                    and record.last_activity_at > cutoff
                )
            ):
                continue

            # Verify the actual transcript before relying on possibly stale metadata.
            messages = await conversations.get_messages_by_conversation_id(
                conversation_id=record.conversation_id,
                conversation_type=record.conversation_type,
            )
            if not messages:
                logger.warning(
                    "Cannot auto-close QA record %s: conversation unavailable",
                    record.id,
                )
                continue
            last_activity_at = max(message.created_at for message in messages)
            if last_activity_at > cutoff:
                continue
            record.qa_status = QAStatus.finished
            record.verdict = BotAnswerVerdict.Correct
            record.reasoning = (
                "Automatically marked correct after at least 144 hours without "
                "conversation activity, regardless of user feedback; "
                "not confirmed by the Evolution Agent."
            )
            record.confidence = None
            record.last_activity_at = last_activity_at
            record.message_count = len(messages)
            record.auto_closed_at = now
            record.updated_at = now
            if await replace_qa_record_if_unchanged(
                record.to_cosmos(), etag=doc["_etag"]
            ):
                closed += 1
                logger.info(
                    "Auto-closed inactive QA record %s without Agent review "
                    "(last activity: %s)",
                    record.id,
                    last_activity_at.isoformat(),
                )
        return closed

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
            if record.feedback
            and record.feedback.issue_url
            and record.feedback.status
            in (FeedbackStatus.pending_validation, FeedbackStatus.failed)
        ]


def _now() -> datetime:
    return datetime.now(timezone.utc)


__all__ = ["QARecordService"]
