"""Unit tests for the feedback-job data model and status logic.

These are pure/offline tests — they do not touch Cosmos, Foundry, or the
network. They cover:

* the ``finished`` gate parsing in the conversation evaluator,
* the Layer-1 QA status decision and thread aggregation, and
* the Layer-2 feedback lifecycle and retry eligibility.
"""

from __future__ import annotations

import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest.mock import AsyncMock

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import services.chatbot_evolution_agent_service as evolution_service_module
import services.qa_record_service as qa_record_service_module
from models.conversation import (
    BotAnswerVerdict,
    ConversationMessageExtraInfo,
    ConversationMessageItem,
    ConversationState,
    ConversationType,
    Role,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
from services.conversation_service import ConversationService
from services.chatbot_evolution_agent_service import ChatbotEvolutionAgentService
from services.qa_record_service import QARecordService

_CONVERSATION_ID = "conv-qa-test"
_PARTITION = f"{ConversationType.teams_channel.value}:{_CONVERSATION_ID}"
_BASE_TIME = datetime(2026, 7, 1, 12, 0, 0, tzinfo=timezone.utc)


def _msg(
    role: Role,
    sender_id: str,
    content: str,
    order: int,
    *,
    msg_id: str | None = None,
    tenant_id: str | None = None,
    trace_id: str | None = None,
) -> ConversationMessageItem:
    return ConversationMessageItem(
        id=msg_id or f"msg-{order}",
        tenant_id=tenant_id,
        sender_role=role,
        sender_id=sender_id,
        sender_name=sender_id,
        content=content,
        created_at=_BASE_TIME + timedelta(minutes=order),
        conversation_id=_CONVERSATION_ID,
        conversation_type=ConversationType.teams_channel,
        conversation_partition=_PARTITION,
        trace_id=trace_id,
    )


# ---------------------------------------------------------------------------
# 1. Evaluation `finished` gate parsing
# ---------------------------------------------------------------------------


def test_parse_evaluation_finished_true():
    state, verdict, reasoning, confidence = ConversationService._parse_evaluation(
        '{"finished": true, "verdict": "correct", "reasoning": "ok", "confidence": 0.9}'
    )
    assert state == ConversationState.Finished
    assert verdict == BotAnswerVerdict.Correct
    assert confidence == 0.9


def test_parse_evaluation_finished_false_is_ongoing():
    state, verdict, _, _ = ConversationService._parse_evaluation(
        '{"finished": false, "verdict": "unknown", "reasoning": "still going", "confidence": 0.3}'
    )
    assert state == ConversationState.Ongoing
    assert verdict == BotAnswerVerdict.Unknown


def test_parse_evaluation_missing_finished_defaults_ongoing():
    state, _, _, _ = ConversationService._parse_evaluation(
        '{"verdict": "incorrect", "reasoning": "wrong", "confidence": 0.8}'
    )
    assert state == ConversationState.Ongoing


def test_parse_evaluation_invalid_json_is_ongoing_unknown():
    state, verdict, _, confidence = ConversationService._parse_evaluation("not json")
    assert state == ConversationState.Ongoing
    assert verdict == BotAnswerVerdict.Unknown
    assert confidence == 0.0


# ---------------------------------------------------------------------------
# 2. Layer-1 QA status decision
# ---------------------------------------------------------------------------


def test_decide_status_ongoing_stays_ongoing():
    assert (
        QARecordService.decide_qa_status(
            ConversationState.Ongoing, BotAnswerVerdict.Correct
        )
        == QAStatus.ongoing
    )


def test_decide_status_finished_correct_is_finished():
    assert (
        QARecordService.decide_qa_status(
            ConversationState.Finished, BotAnswerVerdict.Correct
        )
        == QAStatus.finished
    )


def test_decide_status_finished_incorrect_is_failed():
    assert (
        QARecordService.decide_qa_status(
            ConversationState.Finished, BotAnswerVerdict.Incorrect
        )
        == QAStatus.failed
    )


def test_decide_status_finished_unknown_is_failed():
    # Per design: unknown on a concluded thread is treated as failed.
    assert (
        QARecordService.decide_qa_status(
            ConversationState.Finished, BotAnswerVerdict.Unknown
        )
        == QAStatus.failed
    )


# ---------------------------------------------------------------------------
# 3. Thread aggregation -> QA record
# ---------------------------------------------------------------------------


def test_build_record_from_thread():
    messages = [
        _msg(Role.User, "poster-1", "How do I add an API version?", 0),
        _msg(
            Role.System,
            "azure-sdk-qa-bot",
            "Use @added.",
            1,
            msg_id="bot-resp_abc123",
            tenant_id="typespec",
            trace_id="trace-xyz",
        ),
        _msg(Role.User, "expert-2", "That's right.", 2),
    ]
    record = QARecordService().build_record(messages)
    assert record is not None
    assert record.id == _PARTITION
    assert record.tenant_id == "typespec"
    assert record.conversation_id == _CONVERSATION_ID
    assert record.qa_status == QAStatus.ongoing
    assert record.has_expert_reply is True
    assert record.message_count == 3
    # Feedback is thread-scoped: the record carries no per-reply response_id
    # or trace_id — the agent derives traces from the conversation.
    assert not hasattr(record, "response_id")
    assert not hasattr(record, "trace_id")


def test_build_record_without_bot_answer_is_none():
    messages = [
        _msg(Role.User, "poster-1", "Anyone there?", 0),
    ]
    assert QARecordService().build_record(messages) is None


# ---------------------------------------------------------------------------
# 3b. Testing-channel exclusion
# ---------------------------------------------------------------------------


def test_build_record_captures_channel_id_from_extra_info():
    bot = _msg(
        Role.System,
        "azure-sdk-qa-bot",
        "answer",
        1,
        msg_id="bot-resp_1",
        tenant_id="typespec",
    )
    bot.extra_info = ConversationMessageExtraInfo(channel_id="19:channel-abc@thread.tacv2")
    messages = [_msg(Role.User, "poster-1", "q?", 0), bot]
    record = QARecordService().build_record(messages)
    assert record is not None
    assert record.channel_id == "19:channel-abc@thread.tacv2"


def test_channel_key_of_prefers_stored_channel_id():
    now = datetime.now(timezone.utc)
    record = QARecord(
        id=_PARTITION,
        tenant_id="typespec",
        conversation_id="19:conv-root@thread.tacv2;messageid=123",
        conversation_type=ConversationType.teams_channel,
        channel_id="19:stored-channel@thread.tacv2",
        qa_status=QAStatus.failed,
        first_seen_at=now,
        created_at=now,
        updated_at=now,
    )
    assert QARecordService.channel_key_of(record) == "19:stored-channel@thread.tacv2"


def test_channel_key_of_falls_back_to_conversation_id_segment():
    now = datetime.now(timezone.utc)
    record = QARecord(
        id=_PARTITION,
        tenant_id="typespec",
        conversation_id="19:conv-root@thread.tacv2;messageid=123",
        conversation_type=ConversationType.teams_channel,
        qa_status=QAStatus.failed,
        first_seen_at=now,
        created_at=now,
        updated_at=now,
    )
    assert QARecordService.channel_key_of(record) == "19:conv-root@thread.tacv2"


def test_qa_record_round_trips_through_cosmos_dict():
    now = datetime.now(timezone.utc)
    record = QARecord(
        id=_PARTITION,
        tenant_id="typespec",
        conversation_id=_CONVERSATION_ID,
        conversation_type=ConversationType.teams_channel,
        qa_status=QAStatus.failed,
        feedback=FeedbackState(
            status=FeedbackStatus.running,
            created_at=now,
            updated_at=now,
        ),
        first_seen_at=now,
        created_at=now,
        updated_at=now,
    )
    doc = record.to_cosmos()
    doc["_etag"] = "system-field"  # simulate a Cosmos system field
    restored = QARecord.from_cosmos(doc)
    assert restored.qa_status == QAStatus.failed
    assert restored.cosmos_etag == "system-field"
    assert "cosmos_etag" not in restored.to_cosmos()
    assert restored.feedback is not None
    assert restored.feedback.status == FeedbackStatus.running


# ---------------------------------------------------------------------------
# 4. Layer-2 feedback lifecycle
# ---------------------------------------------------------------------------


def _record_with_feedback() -> QARecord:
    now = datetime.now(timezone.utc)
    return QARecord(
        id=_PARTITION,
        tenant_id="typespec",
        conversation_id=_CONVERSATION_ID,
        conversation_type=ConversationType.teams_channel,
        qa_status=QAStatus.failed,
        feedback=FeedbackState(status=FeedbackStatus.running, created_at=now, updated_at=now),
        first_seen_at=now,
        created_at=now,
        updated_at=now,
    )


def test_finalize_failed_sets_error():
    svc = ChatbotEvolutionAgentService()
    record = _record_with_feedback()
    svc._finalize_failed(record, error="timeout")
    assert record.feedback is not None
    assert record.feedback.status == FeedbackStatus.failed
    assert record.feedback.error == "timeout"


def test_build_input_shape():
    svc = ChatbotEvolutionAgentService()
    record = _record_with_feedback()
    payload = svc._build_input(record)
    assert payload.tenant_id == "typespec"
    assert payload.conversation_id == _CONVERSATION_ID
    assert payload.conversation_type == ConversationType.teams_channel
    # Feedback is thread-scoped: the payload carries only thread coordinates,
    # no per-reply response_id and no trigger.
    assert not hasattr(payload, "response_id")
    assert not hasattr(payload, "trigger")


def test_feedback_without_state_is_runnable():
    record = _record_with_feedback()
    record.feedback = None
    assert ChatbotEvolutionAgentService.is_runnable(record)


def test_failed_feedback_is_runnable():
    record = _record_with_feedback()
    assert record.feedback is not None
    record.feedback.status = FeedbackStatus.failed
    assert ChatbotEvolutionAgentService.is_runnable(record)


def test_done_feedback_is_not_runnable():
    record = _record_with_feedback()
    assert record.feedback is not None
    record.feedback.status = FeedbackStatus.done
    assert not ChatbotEvolutionAgentService.is_runnable(record)


def test_recent_running_feedback_is_not_runnable():
    now = datetime.now(timezone.utc)
    record = _record_with_feedback()
    assert record.feedback is not None
    record.feedback.updated_at = now - timedelta(minutes=14)
    assert not ChatbotEvolutionAgentService.is_runnable(record, now=now)


def test_stale_running_feedback_is_runnable():
    now = datetime.now(timezone.utc)
    record = _record_with_feedback()
    assert record.feedback is not None
    record.feedback.updated_at = now - timedelta(minutes=16)
    assert ChatbotEvolutionAgentService.is_runnable(record, now=now)


@pytest.mark.asyncio
async def test_run_job_persists_created_running_and_done(monkeypatch):
    record = _record_with_feedback()
    record.feedback = None
    persisted_statuses: list[str] = []

    async def capture_replace(document, *, etag):
        persisted_statuses.append(document["feedback"]["status"])
        return {**document, "_etag": f"{etag}-next"}

    service = ChatbotEvolutionAgentService()
    monkeypatch.setattr(
        service,
        "_load_job",
        AsyncMock(return_value=(record, "etag-0")),
    )
    monkeypatch.setattr(
        service,
        "_invoke_agent",
        AsyncMock(return_value='{"status":"completed"}'),
    )
    monkeypatch.setattr(
        evolution_service_module,
        "replace_qa_record_if_match",
        capture_replace,
    )

    attempted = await service.run_job(record.id, record.tenant_id)

    assert attempted is True
    assert persisted_statuses == ["created", "running", "done"]


@pytest.mark.asyncio
async def test_run_job_skips_when_atomic_claim_loses(monkeypatch):
    record = _record_with_feedback()
    record.feedback = None

    service = ChatbotEvolutionAgentService()
    invoke = AsyncMock(return_value='{"status":"completed"}')
    monkeypatch.setattr(
        service,
        "_load_job",
        AsyncMock(return_value=(record, "etag-0")),
    )
    monkeypatch.setattr(service, "_invoke_agent", invoke)
    monkeypatch.setattr(
        evolution_service_module,
        "replace_qa_record_if_match",
        AsyncMock(return_value=None),
    )

    attempted = await service.run_job(record.id, record.tenant_id)

    assert attempted is False
    invoke.assert_not_awaited()


@pytest.mark.asyncio
async def test_qa_update_reloads_record_when_etag_changed(monkeypatch):
    record = _record_with_feedback()
    record.qa_status = QAStatus.ongoing
    record.feedback = None
    record.cosmos_etag = "old-etag"

    latest = record.model_copy(deep=True)
    latest.qa_status = QAStatus.failed
    latest.feedback = FeedbackState(
        status=FeedbackStatus.running,
        created_at=_BASE_TIME,
        updated_at=_BASE_TIME,
    )
    latest_doc = {**latest.to_cosmos(), "_etag": "new-etag"}

    monkeypatch.setattr(
        qa_record_service_module,
        "replace_qa_record_if_match",
        AsyncMock(return_value=None),
    )
    monkeypatch.setattr(
        qa_record_service_module,
        "read_qa_record",
        AsyncMock(return_value=latest_doc),
    )

    result = await QARecordService()._replace_or_reload(record)

    assert result.qa_status == QAStatus.failed
    assert result.feedback is not None
    assert result.feedback.status == FeedbackStatus.running
    assert result.cosmos_etag == "new-etag"


@pytest.mark.asyncio
async def test_concurrent_record_creation_does_not_overwrite_winner(monkeypatch):
    messages = [
        _msg(Role.User, "poster-1", "question", 0),
        _msg(
            Role.System,
            "azure-sdk-qa-bot",
            "answer",
            1,
            tenant_id="typespec",
        ),
    ]
    winner = QARecordService().build_record(messages)
    assert winner is not None
    winner.qa_status = QAStatus.failed
    winner.feedback = FeedbackState(
        status=FeedbackStatus.running,
        created_at=_BASE_TIME,
        updated_at=_BASE_TIME,
    )
    winner_doc = {**winner.to_cosmos(), "_etag": "winner-etag"}

    monkeypatch.setattr(
        qa_record_service_module,
        "read_qa_record",
        AsyncMock(side_effect=[None, winner_doc]),
    )
    monkeypatch.setattr(
        qa_record_service_module,
        "create_qa_record_if_absent",
        AsyncMock(return_value=None),
    )

    touched = await QARecordService().upsert_threads_from_messages(messages)

    assert len(touched) == 1
    assert touched[0].qa_status == QAStatus.failed
    assert touched[0].feedback is not None
    assert touched[0].feedback.status == FeedbackStatus.running
