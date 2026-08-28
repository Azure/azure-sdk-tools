"""Offline tests for QA-record aggregation and evolution transitions."""

from __future__ import annotations

import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest
from pydantic import ValidationError

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.conversation import (
    BotAnswerVerdict,
    ConversationMessageExtraInfo,
    ConversationMessageItem,
    ConversationType,
    Role,
)
from models.feedback import (
    ChatbotEvolutionAgentInput,
    ChatbotEvolutionAgentMode,
    ChatbotEvolutionAgentOutcome,
    ChatbotEvolutionAgentResult,
    RootCauseClassification,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
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
    tenant_id: str | None = None,
) -> ConversationMessageItem:
    return ConversationMessageItem(
        id=f"msg-{order}",
        tenant_id=tenant_id,
        sender_role=role,
        sender_id=sender_id,
        sender_name=sender_id,
        content=content,
        created_at=_BASE_TIME + timedelta(minutes=order),
        conversation_id=_CONVERSATION_ID,
        conversation_type=ConversationType.teams_channel,
        conversation_partition=_PARTITION,
    )


def _record(
    *,
    qa_status: QAStatus = QAStatus.ongoing,
    feedback_status: FeedbackStatus | None = FeedbackStatus.running,
) -> QARecord:
    feedback = (
        FeedbackState(
            status=feedback_status,
            created_at=_BASE_TIME,
            updated_at=_BASE_TIME,
        )
        if feedback_status
        else None
    )
    return QARecord(
        id=_PARTITION,
        tenant_id="typespec",
        conversation_id=_CONVERSATION_ID,
        conversation_type=ConversationType.teams_channel,
        qa_status=qa_status,
        feedback=feedback,
        first_seen_at=_BASE_TIME,
        created_at=_BASE_TIME,
        updated_at=_BASE_TIME,
    )


def _result(
    outcome: ChatbotEvolutionAgentOutcome,
) -> ChatbotEvolutionAgentResult:
    if outcome == ChatbotEvolutionAgentOutcome.issue_created:
        return ChatbotEvolutionAgentResult(
            outcome=outcome,
            classification=RootCauseClassification.retrieval_mismatch,
            issue_url="https://github.com/Azure/azure-sdk-pr/issues/123",
            reasoning="Grounded result.",
            confidence=0.9,
        )
    return ChatbotEvolutionAgentResult(
        outcome=outcome,
        reasoning="Grounded result.",
        confidence=0.9,
    )


# ---------------------------------------------------------------------------
# Thread aggregation and channel metadata
# ---------------------------------------------------------------------------


def test_build_record_from_thread() -> None:
    messages = [
        _msg(Role.User, "poster-1", "How do I add an API version?", 0),
        _msg(Role.System, "azure-sdk-qa-bot", "Use @added.", 1, tenant_id="typespec"),
        _msg(Role.User, "expert-2", "That's right.", 2),
    ]
    record = QARecordService().build_record(messages)
    assert record is not None
    assert record.id == _PARTITION
    assert record.tenant_id == "typespec"
    assert record.qa_status == QAStatus.ongoing
    assert record.has_expert_reply is True
    assert record.message_count == 3
    assert record.conversation_created_at == _BASE_TIME
    # Records stay thread-scoped; the Agent selects the relevant bot turn and
    # trace after fetching the complete conversation.
    assert not hasattr(record, "response_id")
    assert not hasattr(record, "trace_id")


def test_build_record_without_bot_answer_is_none() -> None:
    assert QARecordService().build_record(
        [_msg(Role.User, "poster-1", "Anyone there?", 0)]
    ) is None


def test_build_record_captures_channel_id_from_extra_info() -> None:
    bot = _msg(Role.System, "azure-sdk-qa-bot", "answer", 1, tenant_id="typespec")
    bot.extra_info = ConversationMessageExtraInfo(channel_id="19:channel@thread.tacv2")
    record = QARecordService().build_record(
        [_msg(Role.User, "poster-1", "q?", 0), bot]
    )
    assert record is not None
    assert record.channel_id == "19:channel@thread.tacv2"


def test_channel_key_falls_back_to_conversation_id() -> None:
    record = _record(feedback_status=None)
    record.conversation_id = "19:channel@thread.tacv2;messageid=123"
    assert QARecordService.channel_key_of(record) == "19:channel@thread.tacv2"


def test_qa_record_round_trip_preserves_validation_state() -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.pending_validation,
    )
    assert record.feedback is not None
    record.feedback.issue_url = "https://github.com/Azure/azure-sdk-pr/issues/123"
    record.feedback.classification = RootCauseClassification.reasoning_gap
    doc = record.to_cosmos()
    assert "mode" not in doc["feedback"]
    doc["_etag"] = "system-field"
    restored = QARecord.from_cosmos(doc)
    assert restored.feedback is not None
    assert restored.feedback.status == FeedbackStatus.pending_validation
    assert restored.feedback.classification == RootCauseClassification.reasoning_gap


# ---------------------------------------------------------------------------
# Agent contract and lifecycle transitions
# ---------------------------------------------------------------------------


def test_analysis_input_omits_issue() -> None:
    payload = ChatbotEvolutionAgentService()._build_input(
        _record(),
        ChatbotEvolutionAgentMode.analysis,
    )
    assert payload.mode == ChatbotEvolutionAgentMode.analysis
    assert payload.issue_url is None
    assert "tenant_id" not in payload.model_dump()
    assert payload.evaluation_time.tzinfo is not None
    assert payload.evaluation_time.utcoffset() == timedelta(0)


def test_validation_input_requires_issue() -> None:
    with pytest.raises(ValidationError):
        ChatbotEvolutionAgentInput(
            conversation_id=_CONVERSATION_ID,
            conversation_type=ConversationType.teams_channel,
            evaluation_time=_BASE_TIME,
            mode=ChatbotEvolutionAgentMode.validation,
        )


def test_evolution_input_requires_timezone_aware_evaluation_time() -> None:
    with pytest.raises(ValidationError, match="UTC offset"):
        ChatbotEvolutionAgentInput(
            conversation_id=_CONVERSATION_ID,
            conversation_type=ConversationType.teams_channel,
            evaluation_time=datetime(2026, 7, 1, 12, 0, 0),
        )


def test_ongoing_result_keeps_record_reanalyzable() -> None:
    record = _record()
    ChatbotEvolutionAgentService()._apply_result(
        record,
        _result(ChatbotEvolutionAgentOutcome.conversation_ongoing),
    )
    assert record.qa_status == QAStatus.ongoing
    assert record.feedback is None
    assert record.verdict == BotAnswerVerdict.Unknown


def test_no_issue_result_finishes_record() -> None:
    record = _record()
    ChatbotEvolutionAgentService()._apply_result(
        record,
        _result(ChatbotEvolutionAgentOutcome.no_issue),
    )
    assert record.qa_status == QAStatus.finished
    assert record.feedback is not None
    assert record.feedback.status == FeedbackStatus.done


def test_issue_result_waits_for_validation() -> None:
    record = _record()
    ChatbotEvolutionAgentService()._apply_result(
        record,
        _result(ChatbotEvolutionAgentOutcome.issue_created),
    )
    assert record.qa_status == QAStatus.failed
    assert record.feedback is not None
    assert record.feedback.status == FeedbackStatus.pending_validation
    assert record.feedback.issue_url == (
        "https://github.com/Azure/azure-sdk-pr/issues/123"
    )


def test_processing_failure_marks_assessment_failed() -> None:
    record = _record()
    ChatbotEvolutionAgentService()._apply_result(
        record,
        _result(ChatbotEvolutionAgentOutcome.processing_failed),
    )
    assert record.qa_status == QAStatus.failed
    assert record.verdict == BotAnswerVerdict.Unknown
    assert record.reasoning == "Grounded result."
    assert record.evaluated_at is not None
    assert record.feedback is not None
    assert record.feedback.status == FeedbackStatus.failed
    assert record.feedback.error == "agent_processing_failed"


def test_remediation_failure_preserves_incorrect_answer() -> None:
    record = _record()
    result = ChatbotEvolutionAgentResult(
        outcome=ChatbotEvolutionAgentOutcome.remediation_failed,
        reasoning="Grounded result.",
        confidence=0.9,
        classification=RootCauseClassification.outdated_content,
    )
    ChatbotEvolutionAgentService()._apply_result(
        record,
        result,
    )
    assert record.qa_status == QAStatus.failed
    assert record.verdict == BotAnswerVerdict.Incorrect
    assert record.reasoning == "Grounded result."
    assert record.evaluated_at is not None
    assert record.feedback is not None
    assert record.feedback.status == FeedbackStatus.failed
    assert record.feedback.error == "agent_remediation_failed"
    assert (
        record.feedback.classification
        == RootCauseClassification.outdated_content
    )


def test_remediation_failure_rejects_issue_url() -> None:
    with pytest.raises(ValidationError):
        ChatbotEvolutionAgentResult(
            outcome=ChatbotEvolutionAgentOutcome.remediation_failed,
            reasoning="Issue creation failed.",
            confidence=0.9,
            classification=RootCauseClassification.outdated_content,
            issue_url="https://github.com/Azure/azure-sdk-pr/issues/123",
        )


@pytest.mark.parametrize(
    ("outcome", "expected"),
    [
        (ChatbotEvolutionAgentOutcome.validation_passed, FeedbackStatus.done),
        (ChatbotEvolutionAgentOutcome.validation_failed, FeedbackStatus.failed),
    ],
)
def test_validation_result_is_terminal(
    outcome: ChatbotEvolutionAgentOutcome,
    expected: FeedbackStatus,
) -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.pending_validation,
    )
    record.verdict = BotAnswerVerdict.Incorrect
    ChatbotEvolutionAgentService()._apply_result(
        record,
        _result(outcome),
        mode=ChatbotEvolutionAgentMode.validation,
    )
    assert record.qa_status == QAStatus.failed
    assert record.verdict == BotAnswerVerdict.Incorrect
    assert record.feedback is not None
    assert record.feedback.status == expected
    assert record.feedback.validated_at is not None


def test_failed_analysis_is_eligible_for_retry() -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.failed,
    )
    assert ChatbotEvolutionAgentService._can_run(
        record,
        ChatbotEvolutionAgentMode.analysis,
    )
    assert not ChatbotEvolutionAgentService._can_run(
        record,
        ChatbotEvolutionAgentMode.validation,
    )


def test_failed_validation_is_eligible_for_retry() -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.failed,
    )
    assert record.feedback is not None
    record.feedback.issue_url = "https://github.com/Azure/azure-sdk-pr/issues/123"
    assert not ChatbotEvolutionAgentService._can_run(
        record,
        ChatbotEvolutionAgentMode.analysis,
    )
    assert ChatbotEvolutionAgentService._can_run(
        record,
        ChatbotEvolutionAgentMode.validation,
    )


@pytest.mark.asyncio
async def test_failed_analysis_is_listed_as_analyzable() -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.failed,
    )
    with (
        patch(
            "services.qa_record_service.query_qa_records_by_qa_status",
            new=AsyncMock(return_value=[]),
        ),
        patch(
            "services.qa_record_service.query_qa_records_by_feedback_status",
            new=AsyncMock(return_value=[record.to_cosmos()]),
        ),
    ):
        records = await QARecordService().list_analyzable()

    assert [item.id for item in records] == [record.id]


@pytest.mark.asyncio
async def test_failed_validation_is_listed_as_pending() -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.failed,
    )
    assert record.feedback is not None
    record.feedback.issue_url = "https://github.com/Azure/azure-sdk-pr/issues/123"
    with patch(
        "services.qa_record_service.query_qa_records_by_feedback_status",
        new=AsyncMock(side_effect=[[], [record.to_cosmos()]]),
    ):
        records = await QARecordService().list_pending_validation()

    assert [item.id for item in records] == [record.id]


@pytest.mark.parametrize(
    "feedback_status",
    [FeedbackStatus.created, FeedbackStatus.running],
)
def test_interrupted_synchronous_run_is_not_retried(
    feedback_status: FeedbackStatus,
) -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=feedback_status,
    )
    assert not ChatbotEvolutionAgentService._can_run(
        record,
        ChatbotEvolutionAgentMode.analysis,
    )
    assert not ChatbotEvolutionAgentService._can_run(
        record,
        ChatbotEvolutionAgentMode.validation,
    )


def test_pending_validation_is_eligible_only_for_validation() -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.pending_validation,
    )
    assert record.feedback is not None
    record.feedback.issue_url = "https://github.com/Azure/azure-sdk-pr/issues/123"
    assert not ChatbotEvolutionAgentService._can_run(
        record, ChatbotEvolutionAgentMode.analysis
    )
    assert ChatbotEvolutionAgentService._can_run(
        record, ChatbotEvolutionAgentMode.validation
    )


def test_issue_created_requires_issue_metadata() -> None:
    with pytest.raises(ValidationError):
        ChatbotEvolutionAgentResult(
            outcome=ChatbotEvolutionAgentOutcome.issue_created,
            reasoning="Missing issue metadata.",
            confidence=0.9,
        )


def test_agent_result_rejects_removed_fields() -> None:
    with pytest.raises(ValidationError):
        ChatbotEvolutionAgentResult.model_validate(
            {
                "outcome": ChatbotEvolutionAgentOutcome.no_issue,
                "reasoning": "Grounded.",
                "confidence": 0.9,
                "verdict": BotAnswerVerdict.Correct,
            }
        )


@pytest.mark.asyncio
async def test_run_job_persists_issue_result() -> None:
    record = _record(qa_status=QAStatus.ongoing, feedback_status=None)
    result = _result(ChatbotEvolutionAgentOutcome.issue_created)

    service = ChatbotEvolutionAgentService()
    service._load_job = AsyncMock(return_value=record)
    service._invoke_agent = AsyncMock(return_value=result.model_dump_json())
    upsert = AsyncMock()
    with patch(
        "services.chatbot_evolution_agent_service.upsert_qa_record",
        new=upsert,
    ):
        actual = await service.run_job(record.id, record.tenant_id)

    assert actual == result
    assert upsert.await_count == 3
    final_call = upsert.await_args
    assert final_call is not None
    persisted = QARecord.from_cosmos(final_call.args[0])
    assert persisted.verdict == BotAnswerVerdict.Incorrect
    assert persisted.feedback is not None
    assert persisted.feedback.status == FeedbackStatus.pending_validation


@pytest.mark.asyncio
async def test_run_job_persists_remediation_failure() -> None:
    record = _record(qa_status=QAStatus.ongoing, feedback_status=None)
    result = _result(ChatbotEvolutionAgentOutcome.remediation_failed)

    service = ChatbotEvolutionAgentService()
    service._load_job = AsyncMock(return_value=record)
    service._invoke_agent = AsyncMock(return_value=result.model_dump_json())
    upsert = AsyncMock()
    with patch(
        "services.chatbot_evolution_agent_service.upsert_qa_record",
        new=upsert,
    ):
        actual = await service.run_job(record.id, record.tenant_id)

    assert actual == result
    final_call = upsert.await_args
    assert final_call is not None
    persisted = QARecord.from_cosmos(final_call.args[0])
    assert persisted.qa_status == QAStatus.failed
    assert persisted.verdict == BotAnswerVerdict.Incorrect
    assert persisted.reasoning == "Grounded result."
    assert persisted.feedback is not None
    assert persisted.feedback.status == FeedbackStatus.failed
    assert persisted.feedback.error == "agent_remediation_failed"


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("mode", "outcome"),
    [
        (
            ChatbotEvolutionAgentMode.analysis,
            ChatbotEvolutionAgentOutcome.validation_passed,
        ),
        (
            ChatbotEvolutionAgentMode.validation,
            ChatbotEvolutionAgentOutcome.no_issue,
        ),
        (
            ChatbotEvolutionAgentMode.validation,
            ChatbotEvolutionAgentOutcome.remediation_failed,
        ),
    ],
)
async def test_run_job_rejects_outcome_for_wrong_mode(
    mode: ChatbotEvolutionAgentMode,
    outcome: ChatbotEvolutionAgentOutcome,
) -> None:
    if mode == ChatbotEvolutionAgentMode.analysis:
        record = _record(qa_status=QAStatus.ongoing, feedback_status=None)
    else:
        record = _record(
            qa_status=QAStatus.failed,
            feedback_status=FeedbackStatus.pending_validation,
        )
        assert record.feedback is not None
        record.feedback.issue_url = (
            "https://github.com/Azure/azure-sdk-pr/issues/123"
        )
    result = _result(outcome)

    service = ChatbotEvolutionAgentService()
    service._load_job = AsyncMock(return_value=record)
    service._invoke_agent = AsyncMock(return_value=result.model_dump_json())
    upsert = AsyncMock()
    with patch(
        "services.chatbot_evolution_agent_service.upsert_qa_record",
        new=upsert,
    ):
        actual = await service.run_job(record.id, record.tenant_id, mode=mode)

    assert actual is None
    final_call = upsert.await_args
    assert final_call is not None
    persisted = QARecord.from_cosmos(final_call.args[0])
    assert persisted.feedback is not None
    assert persisted.feedback.status == FeedbackStatus.failed
    if mode == ChatbotEvolutionAgentMode.analysis:
        assert persisted.qa_status == QAStatus.failed
        assert persisted.verdict == BotAnswerVerdict.Unknown
