"""QA record service — Layer-1 lifecycle for QA threads.

Aggregates the per-message rows in ``conversation-messages`` into one
:class:`~models.qa_record.QARecord` per thread (keyed by
``conversation_id``) and owns the **Layer-1** ``qa_status`` transitions:

* New threads with a bot answer are inserted as ``ongoing``.
* Applying an evaluation result advances a thread to ``finished`` (bot
  answered correctly) or ``failed`` (bot answer wrong/unconfirmed — a case
  worth a feedback analysis). Threads the evaluator marks *ongoing* stay
  ``ongoing`` for re-evaluation on a later run.

The **Layer-2** feedback lifecycle is owned separately by
:class:`services.feedback_agent_service.FeedbackAgentService`.
"""

from __future__ import annotations

import logging
from datetime import datetime, timezone
from typing import Sequence

from models.conversation import (
    BotAnswerVerdict,
    ConversationEvaluationItem,
    ConversationMessageItem,
    ConversationState,
)
from models.qa_record import QARecord, QAStatus
from services.conversation_service import ConversationService
from utils.azure_cosmosdb import (
    query_qa_records_by_qa_status,
    read_qa_record,
    upsert_qa_record,
)

logger = logging.getLogger(__name__)


class QARecordService:
    """Builds and transitions the Layer-1 status of QA threads."""

    def __init__(
        self, conversation_service: ConversationService | None = None
    ) -> None:
        self._conversation = conversation_service or ConversationService()

    # -- Aggregation: messages -> QA records -------------------------------

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
        groups = ConversationService.group_by_conversation(messages)
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
            if existing.qa_status in (QAStatus.finished, QAStatus.failed):
                # Concluded threads are immutable to the scanner.
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

    # -- Transition: apply an evaluation result ----------------------------

    @staticmethod
    def decide_qa_status(
        state: ConversationState, verdict: BotAnswerVerdict
    ) -> QAStatus:
        """Pure Layer-1 status decision (no I/O).

        * ``ongoing`` thread                     -> ``ongoing``.
        * ``finished`` + ``correct``             -> ``finished``.
        * ``finished`` + ``incorrect``/``unknown`` -> ``failed``.
        """
        if state == ConversationState.Ongoing:
            return QAStatus.ongoing
        if verdict == BotAnswerVerdict.Correct:
            return QAStatus.finished
        return QAStatus.failed

    async def apply_evaluation(
        self,
        record: QARecord,
        evaluation: ConversationEvaluationItem,
    ) -> QARecord:
        """Advance ``record``'s Layer-1 status from an evaluation result.

        Mapping (per design):
        * ``ongoing`` thread  -> stay ``ongoing`` (re-evaluate later).
        * ``finished`` + ``correct``            -> ``finished`` (archive).
        * ``finished`` + ``incorrect``/``unknown`` -> ``failed`` (feedback).
        """
        record.verdict = evaluation.verdict
        record.reasoning = evaluation.reasoning
        record.confidence = evaluation.confidence
        record.has_expert_reply = evaluation.has_expert_reply
        record.message_count = evaluation.message_count
        record.evaluated_at = evaluation.evaluated_at
        record.message_link = record.message_link or evaluation.message_link

        record.qa_status = self.decide_qa_status(evaluation.state, evaluation.verdict)

        record.updated_at = _now()
        await upsert_qa_record(record.to_cosmos())
        return record

    # -- Queries -----------------------------------------------------------

    async def list_ongoing(self, *, tenant_id: str | None = None) -> list[QARecord]:
        """Return every QA record still in the ``ongoing`` state."""
        docs = await query_qa_records_by_qa_status(
            qa_status=QAStatus.ongoing.value, tenant_id=tenant_id
        )
        return [QARecord.from_cosmos(d) for d in docs]


def _now() -> datetime:
    return datetime.now(timezone.utc)


__all__ = ["QARecordService"]
