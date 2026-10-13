"""Offline tests for QA-record aggregation and evolution transitions."""

from __future__ import annotations

import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock, patch

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
    FoundryAgentReference,
    RootCauseClassification,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
from services.chatbot_evolution_agent_service import ChatbotEvolutionAgentService
from services.qa_record_service import QARecordService
from utils.azure_ai_foundry_agent import HostedAgentClient
from utils.knowledge_config import KbIssueTarget, KbTarget

_CONVERSATION_ID = "conv-qa-test"
_PARTITION = f"{ConversationType.teams_channel.value}:{_CONVERSATION_ID}"
_BASE_TIME = datetime(2026, 7, 1, 12, 0, 0, tzinfo=timezone.utc)
_WIKI_SOURCE_URL = "https://github.com/Azure/azure-sdk-for-java.wiki.git"
_WIKI_SOURCE_TARGET = KbTarget(
    source_url=_WIKI_SOURCE_URL,
    branch="master",
    path="",
    scope="java_wiki",
    issue_target=KbIssueTarget(
        provider="github",
        owner="Azure",
        repo="azure-sdk-for-java",
    ),
)
_ADO_SOURCE_URL = (
    "https://azure-sdk@dev.azure.com/azure-sdk/internal/_git/internal.wiki"
)
_ADO_SOURCE_TARGET = KbTarget(
    source_url=_ADO_SOURCE_URL,
    branch="main",
    path="",
    scope="internal_wiki",
    issue_target=KbIssueTarget(
        provider="azure-devops",
        organization="azure-sdk",
        project="internal",
    ),
)


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
    if outcome in (
        ChatbotEvolutionAgentOutcome.issue_created,
        ChatbotEvolutionAgentOutcome.issue_reused,
    ):
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


def _kb_issue_result(
    *,
    issue_url: str,
    source_id: str,
    source_url: str,
) -> ChatbotEvolutionAgentResult:
    return ChatbotEvolutionAgentResult(
        outcome=ChatbotEvolutionAgentOutcome.issue_created,
        classification=RootCauseClassification.missing_content,
        issue_url=issue_url,
        source_id=source_id,
        source_url=source_url,
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


@pytest.mark.asyncio
async def test_scan_fetches_full_thread_after_human_only_activity() -> None:
    human = _msg(Role.User, "poster-1", "Follow-up?", 2)
    human.created_at = _BASE_TIME + timedelta(days=1)
    items = [
        _msg(Role.User, "poster-1", "Question?", 0),
        _msg(Role.System, "bot", "Answer.", 1, tenant_id="typespec"),
        human,
    ]

    async def rows(values):
        for value in values:
            yield value

    container = MagicMock()
    container.query_items.side_effect = [
        rows([{"partition": _PARTITION}]),
        rows([item.model_dump(mode="json") for item in items]),
    ]
    with patch(
        "services.qa_record_service.get_conversation_message_container",
        new=AsyncMock(return_value=container),
    ):
        actual = await QARecordService().get_messages_in_period(
            human.created_at, human.created_at + timedelta(days=1)
        )

    assert actual == items
    window = container.query_items.call_args_list[0].kwargs
    assert "sender_role" not in window["query"]
    assert not any(param["name"] == "@bot_roles" for param in window["parameters"])
    record = QARecordService().build_record(actual)
    assert record is not None
    assert record.last_activity_at == human.created_at


@pytest.mark.asyncio
@pytest.mark.parametrize("qa_status", [QAStatus.ongoing, QAStatus.finished, QAStatus.failed])
async def test_refresh_updates_only_ongoing_thread_metadata(qa_status: QAStatus) -> None:
    record = _record(qa_status=qa_status, feedback_status=None)
    record.conversation_created_at = _BASE_TIME
    record.last_activity_at = _BASE_TIME + timedelta(minutes=1)
    record.message_count = 2
    doc = {**record.to_cosmos(), "_etag": "old-etag"}
    items = [
        _msg(Role.User, "poster", "Question?", 0),
        _msg(Role.System, "bot", "Answer.", 1, tenant_id="typespec"),
        _msg(Role.User, "poster", "New follow-up?", 2),
    ]
    replace = AsyncMock(return_value=True)
    upsert = AsyncMock()
    with (
        patch("services.qa_record_service.read_qa_record", new=AsyncMock(return_value=doc)),
        patch("services.qa_record_service.replace_qa_record_if_unchanged", new=replace),
        patch("services.qa_record_service.upsert_qa_record", new=upsert),
    ):
        touched = await QARecordService().upsert_threads_from_messages(items)

    assert len(touched) == 1
    upsert.assert_not_awaited()
    if qa_status == QAStatus.ongoing:
        replace.assert_awaited_once()
        write = replace.await_args
        assert write is not None
        assert write.kwargs["etag"] == "old-etag"
        updated = QARecord.from_cosmos(write.args[0])
        assert updated.last_activity_at == items[-1].created_at
        assert updated.message_count == 3
        assert updated.feedback is None
    else:
        replace.assert_not_awaited()
        assert touched[0].last_activity_at == record.last_activity_at


@pytest.mark.asyncio
async def test_metadata_refresh_does_not_overwrite_concurrent_feedback() -> None:
    record = _record(feedback_status=None)
    record.conversation_created_at = _BASE_TIME
    doc = {**record.to_cosmos(), "_etag": "old-etag"}
    replace = AsyncMock(return_value=False)
    with (
        patch("services.qa_record_service.read_qa_record", new=AsyncMock(return_value=doc)),
        patch("services.qa_record_service.replace_qa_record_if_unchanged", new=replace),
    ):
        assert await QARecordService().upsert_threads_from_messages(
            [_msg(Role.System, "bot", "Answer.", 1, tenant_id="typespec")]
        ) == []
    replace.assert_awaited_once()


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("hours", "reaction", "write_succeeds", "expected_closed"),
    [
        (143, "good", True, 0),
        (144, "good", True, 1),
        (145, "good", True, 1),
        (144, "bad", True, 0),
        (144, "good", False, 0),
    ],
)
async def test_inactive_threads_close_only_after_cutoff_without_negative_feedback(
    hours: int, reaction: str, write_succeeds: bool, expected_closed: int,
) -> None:
    record = _record(feedback_status=None)
    record.last_activity_at = _BASE_TIME - timedelta(hours=hours)
    record.first_seen_at = _BASE_TIME - timedelta(days=30)
    record.evaluated_at = _BASE_TIME - timedelta(hours=1)
    record.confidence = 0.9
    message = _msg(Role.System, "bot", "Answer.", 0, tenant_id="typespec")
    message.created_at = record.last_activity_at
    doc = {**record.to_cosmos(), "_etag": "old-etag"}
    query = AsyncMock(return_value=[doc])
    feedback = AsyncMock(return_value=[{"reaction": reaction}])
    replace = AsyncMock(return_value=write_succeeds)
    with (
        patch("services.qa_record_service._now", return_value=_BASE_TIME),
        patch("services.qa_record_service.query_qa_records_by_qa_status", new=query),
        patch(
            "services.qa_record_service.ConversationService.get_messages_by_conversation_id",
            new=AsyncMock(return_value=[message]),
        ),
        patch("services.qa_record_service.query_conversation_feedback", new=feedback),
        patch("services.qa_record_service.replace_qa_record_if_unchanged", new=replace),
    ):
        assert await QARecordService().close_inactive_threads(
            tenant_id="typespec"
        ) == expected_closed

    query.assert_awaited_once_with(qa_status="ongoing", tenant_id="typespec")
    if hours < 144 or reaction == "bad":
        replace.assert_not_awaited()
    else:
        write = replace.await_args
        assert write is not None
        assert write.kwargs["etag"] == "old-etag"
        closed = QARecord.from_cosmos(write.args[0])
        assert closed.qa_status == QAStatus.finished
        assert closed.verdict == BotAnswerVerdict.Correct
        assert closed.feedback is None
        assert closed.confidence is None
        assert closed.reasoning is not None
        assert "not confirmed by the Evolution Agent" in closed.reasoning
        assert closed.evaluated_at == record.evaluated_at
        assert closed.updated_at == _BASE_TIME
        assert closed.first_seen_at == record.first_seen_at
        assert closed.last_activity_at == message.created_at


@pytest.mark.asyncio
async def test_inactivity_closure_checks_latest_transcript_not_stale_metadata() -> None:
    record = _record(feedback_status=None)
    record.last_activity_at = _BASE_TIME - timedelta(days=30)
    message = _msg(Role.User, "poster", "A new follow-up?", 0)
    message.created_at = _BASE_TIME - timedelta(hours=143, minutes=59, seconds=59)
    feedback = AsyncMock()
    replace = AsyncMock()
    with (
        patch("services.qa_record_service._now", return_value=_BASE_TIME),
        patch(
            "services.qa_record_service.query_qa_records_by_qa_status",
            new=AsyncMock(return_value=[{**record.to_cosmos(), "_etag": "old-etag"}]),
        ),
        patch(
            "services.qa_record_service.ConversationService.get_messages_by_conversation_id",
            new=AsyncMock(return_value=[message]),
        ),
        patch("services.qa_record_service.query_conversation_feedback", new=feedback),
        patch("services.qa_record_service.replace_qa_record_if_unchanged", new=replace),
    ):
        assert await QARecordService().close_inactive_threads() == 0
    feedback.assert_not_awaited()
    replace.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("feedback_status", [None, *FeedbackStatus])
async def test_inactivity_closure_preserves_feedback_workflows_and_excluded_channels(
    feedback_status: FeedbackStatus | None,
) -> None:
    record = _record(feedback_status=feedback_status)
    record.last_activity_at = _BASE_TIME - timedelta(days=30)
    messages = AsyncMock()
    replace = AsyncMock()
    with (
        patch("services.qa_record_service._now", return_value=_BASE_TIME),
        patch(
            "services.qa_record_service.query_qa_records_by_qa_status",
            new=AsyncMock(return_value=[{**record.to_cosmos(), "_etag": "old-etag"}]),
        ),
        patch(
            "services.qa_record_service.ConversationService.get_messages_by_conversation_id",
            new=messages,
        ),
        patch("services.qa_record_service.replace_qa_record_if_unchanged", new=replace),
    ):
        assert await QARecordService().close_inactive_threads(
            excluded_channels={_CONVERSATION_ID} if feedback_status is None else set()
        ) == 0
    messages.assert_not_awaited()
    replace.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("lookup", ["messages", "feedback"])
async def test_inactivity_closure_propagates_lookup_failures(lookup: str) -> None:
    record = _record(feedback_status=None)
    record.last_activity_at = _BASE_TIME - timedelta(days=30)
    message = _msg(Role.System, "bot", "Answer.", 0)
    message.created_at = record.last_activity_at
    messages = AsyncMock(return_value=[message])
    feedback = AsyncMock(return_value=[])
    if lookup == "messages":
        messages.side_effect = RuntimeError("lookup unavailable")
    else:
        feedback.side_effect = RuntimeError("lookup unavailable")
    replace = AsyncMock()
    with (
        patch("services.qa_record_service._now", return_value=_BASE_TIME),
        patch(
            "services.qa_record_service.query_qa_records_by_qa_status",
            new=AsyncMock(return_value=[{**record.to_cosmos(), "_etag": "old-etag"}]),
        ),
        patch(
            "services.qa_record_service.ConversationService.get_messages_by_conversation_id",
            new=messages,
        ),
        patch("services.qa_record_service.query_conversation_feedback", new=feedback),
        patch("services.qa_record_service.replace_qa_record_if_unchanged", new=replace),
        pytest.raises(RuntimeError, match="lookup unavailable"),
    ):
        await QARecordService().close_inactive_threads()
    replace.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "mode", [ChatbotEvolutionAgentMode.analysis, ChatbotEvolutionAgentMode.validation]
)
@pytest.mark.parametrize("status", [FeedbackStatus.created, FeedbackStatus.running])
async def test_recovery_makes_abandoned_jobs_retryable(
    mode: ChatbotEvolutionAgentMode, status: FeedbackStatus,
) -> None:
    record = _record(feedback_status=status)
    assert record.feedback is not None
    record.feedback.updated_at = _BASE_TIME - timedelta(minutes=20)
    record.feedback.created_at = record.feedback.updated_at
    if mode == ChatbotEvolutionAgentMode.validation:
        record.qa_status = QAStatus.failed
        record.verdict = BotAnswerVerdict.Incorrect
        record.feedback.issue_url = "https://github.com/Azure/azure-sdk-pr/issues/123"
    doc = {**record.to_cosmos(), "_etag": "old-etag"}
    query = AsyncMock(side_effect=[[doc], []] if status == FeedbackStatus.created else [[], [doc]])
    replace = AsyncMock(return_value=True)
    service = ChatbotEvolutionAgentService()
    service._invoke_agent = AsyncMock()
    with (
        patch("services.chatbot_evolution_agent_service._now", return_value=_BASE_TIME),
        patch("services.chatbot_evolution_agent_service.query_qa_records_by_feedback_status", new=query),
        patch("services.chatbot_evolution_agent_service.replace_qa_record_if_unchanged", new=replace),
    ):
        assert await service.recover_stale_jobs(tenant_id="typespec") == 1

    assert all(call.kwargs["tenant_id"] == "typespec" for call in query.await_args_list)
    write = replace.await_args
    assert write is not None
    assert write.kwargs["etag"] == "old-etag"
    recovered = QARecord.from_cosmos(write.args[0])
    assert recovered.feedback is not None
    assert recovered.feedback.status == FeedbackStatus.failed
    assert recovered.feedback.error == f"agent_{mode.value}_abandoned: inactive for at least 20 minutes"
    assert recovered.feedback.issue_url == record.feedback.issue_url
    assert ChatbotEvolutionAgentService._can_run(recovered, mode)
    if mode == ChatbotEvolutionAgentMode.analysis:
        assert recovered.verdict == BotAnswerVerdict.Unknown
    else:
        assert recovered.verdict == BotAnswerVerdict.Incorrect
    service._invoke_agent.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("age_minutes", "excluded", "write_succeeds", "writes"),
    [(19, False, True, 0), (20, True, True, 0), (20, False, False, 1)],
)
async def test_recovery_preserves_active_excluded_or_changed_jobs(
    age_minutes: int, excluded: bool, write_succeeds: bool, writes: int,
) -> None:
    record = _record()
    assert record.feedback is not None
    record.feedback.updated_at = _BASE_TIME - timedelta(minutes=age_minutes)
    doc = {**record.to_cosmos(), "_etag": "old-etag"}
    replace = AsyncMock(return_value=write_succeeds)
    with (
        patch("services.chatbot_evolution_agent_service._now", return_value=_BASE_TIME),
        patch(
            "services.chatbot_evolution_agent_service.query_qa_records_by_feedback_status",
            new=AsyncMock(side_effect=[[], [doc]]),
        ),
        patch("services.chatbot_evolution_agent_service.replace_qa_record_if_unchanged", new=replace),
    ):
        assert await ChatbotEvolutionAgentService().recover_stale_jobs(
            excluded_channels={_CONVERSATION_ID} if excluded else set()
        ) == 0
    assert replace.await_count == writes


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


def test_validation_input_accepts_ado_work_item() -> None:
    payload = ChatbotEvolutionAgentInput(
        conversation_id=_CONVERSATION_ID,
        conversation_type=ConversationType.teams_channel,
        evaluation_time=_BASE_TIME,
        mode=ChatbotEvolutionAgentMode.validation,
        issue_url="https://dev.azure.com/azure-sdk/internal/_workitems/edit/456",
    )

    assert payload.issue_url.endswith("/456")


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
    assert record.verdict == BotAnswerVerdict.Correct
    assert record.feedback is None
    assert record.reasoning == "Grounded result."
    assert record.confidence == 0.9
    assert record.evaluated_at is not None
    assert QARecord.from_cosmos(record.to_cosmos()).feedback is None
    for mode in ChatbotEvolutionAgentMode:
        assert not ChatbotEvolutionAgentService._can_run(record, mode)


def test_legacy_done_without_issue_becomes_no_feedback() -> None:
    record = _record(qa_status=QAStatus.finished)
    record.verdict = BotAnswerVerdict.Correct
    doc = record.to_cosmos()
    doc["feedback"]["status"] = "done"

    restored = QARecord.from_cosmos(doc)

    assert restored.qa_status == QAStatus.finished
    assert restored.verdict == BotAnswerVerdict.Correct
    assert restored.feedback is None
    assert restored.to_cosmos()["feedback"] is None
    assert doc["feedback"]["status"] == "done"


def test_legacy_done_with_issue_preserves_validation_history() -> None:
    record = _record(qa_status=QAStatus.failed)
    record.verdict = BotAnswerVerdict.Incorrect
    assert record.feedback is not None
    record.feedback.issue_url = "https://github.com/Azure/azure-sdk-pr/issues/123"
    record.feedback.classification = RootCauseClassification.reasoning_gap
    record.feedback.validation_reasoning = "Fix confirmed."
    record.feedback.validated_at = _BASE_TIME
    doc = record.to_cosmos()
    doc["feedback"]["status"] = "done"

    restored = QARecord.from_cosmos(doc)

    assert restored.qa_status == QAStatus.failed
    assert restored.verdict == BotAnswerVerdict.Incorrect
    assert restored.feedback is not None
    assert restored.feedback.status == FeedbackStatus.validation_passed
    assert restored.feedback.issue_url == record.feedback.issue_url
    assert restored.feedback.classification == record.feedback.classification
    assert restored.feedback.validation_reasoning == "Fix confirmed."
    assert restored.feedback.validated_at == _BASE_TIME
    assert restored.to_cosmos()["feedback"]["status"] == "validation_passed"
    assert doc["feedback"]["status"] == "done"


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


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "issue_url",
    [
        "https://github.com/Azure/azure-sdk-for-java/issues/456",
        "https://github.com/Azure/azure-sdk-pr/issues/456",
    ],
)
async def test_wiki_issue_route_accepts_configured_target_or_fallback(
    issue_url: str,
) -> None:
    result = _kb_issue_result(
        issue_url=issue_url,
        source_id="java_wiki",
        source_url=_WIKI_SOURCE_URL,
    )

    with patch(
        "services.chatbot_evolution_agent_service.get_kb_targets",
        new=AsyncMock(return_value=(_WIKI_SOURCE_TARGET,)),
    ):
        await ChatbotEvolutionAgentService()._validate_issue_route(result)


@pytest.mark.asyncio
async def test_run_job_rejects_wiki_source_with_unrelated_issue_target() -> None:
    result = _kb_issue_result(
        issue_url="https://github.com/Azure/azure-sdk-tools/issues/456",
        source_id="java_wiki",
        source_url=_WIKI_SOURCE_URL,
    )

    record = _record(qa_status=QAStatus.ongoing, feedback_status=None)
    service = ChatbotEvolutionAgentService()
    service._load_job = AsyncMock(return_value=record)
    service._invoke_agent = AsyncMock(return_value=result.model_dump_json())
    upsert = AsyncMock()
    with (
        patch(
            "services.chatbot_evolution_agent_service.get_kb_targets",
            new=AsyncMock(return_value=(_WIKI_SOURCE_TARGET,)),
        ),
        patch(
            "services.chatbot_evolution_agent_service.upsert_qa_record",
            new=upsert,
        ),
    ):
        actual = await service.run_job(record.id, record.tenant_id)

    assert actual is None
    final_call = upsert.await_args
    assert final_call is not None
    persisted = QARecord.from_cosmos(final_call.args[0])
    assert persisted.feedback is not None
    assert persisted.feedback.status == FeedbackStatus.failed
    assert persisted.feedback.issue_url is None


@pytest.mark.asyncio
async def test_wiki_issue_route_rejects_unconfigured_source() -> None:
    result = _kb_issue_result(
        issue_url="https://github.com/Azure/unconfigured/issues/456",
        source_id="unconfigured",
        source_url="https://github.com/Azure/unconfigured.wiki.git",
    )

    with (
        patch(
            "services.chatbot_evolution_agent_service.get_kb_targets",
            new=AsyncMock(return_value=()),
        ),
        pytest.raises(ValueError, match="authoritative configuration"),
    ):
        await ChatbotEvolutionAgentService()._validate_issue_route(result)


@pytest.mark.asyncio
async def test_ado_issue_route_accepts_configured_target() -> None:
    result = _kb_issue_result(
        issue_url="https://dev.azure.com/azure-sdk/internal/_workitems/edit/456",
        source_id="internal_wiki",
        source_url=_ADO_SOURCE_URL,
    )

    with patch(
        "services.chatbot_evolution_agent_service.get_kb_targets",
        new=AsyncMock(return_value=(_ADO_SOURCE_TARGET,)),
    ):
        await ChatbotEvolutionAgentService()._validate_issue_route(result)


@pytest.mark.asyncio
async def test_static_source_route_accepts_fallback_without_source_url() -> None:
    result = ChatbotEvolutionAgentResult(
        outcome=ChatbotEvolutionAgentOutcome.issue_created,
        classification=RootCauseClassification.missing_content,
        issue_url="https://github.com/Azure/azure-sdk-pr/issues/456",
        source_id="static_typespec_qa",
        reasoning="Grounded result.",
        confidence=0.9,
    )

    with (
        patch(
            "services.chatbot_evolution_agent_service.get_kb_targets",
            new=AsyncMock(return_value=()),
        ),
        patch(
            "services.chatbot_evolution_agent_service.get_knowledge_source",
            return_value=object(),
        ),
    ):
        await ChatbotEvolutionAgentService()._validate_issue_route(result)


@pytest.mark.asyncio
async def test_static_source_route_requires_fallback_repository() -> None:
    result = ChatbotEvolutionAgentResult(
        outcome=ChatbotEvolutionAgentOutcome.issue_created,
        classification=RootCauseClassification.missing_content,
        issue_url="https://github.com/Azure/azure-sdk-tools/issues/456",
        source_id="static_typespec_qa",
        reasoning="Grounded result.",
        confidence=0.9,
    )

    with (
        patch(
            "services.chatbot_evolution_agent_service.get_kb_targets",
            new=AsyncMock(return_value=()),
        ),
        patch(
            "services.chatbot_evolution_agent_service.get_knowledge_source",
            return_value=object(),
        ),
        pytest.raises(ValueError, match="must use Azure/azure-sdk-pr"),
    ):
        await ChatbotEvolutionAgentService()._validate_issue_route(result)


def test_kb_issue_result_requires_source_id() -> None:
    with pytest.raises(ValidationError, match="require source_id"):
        ChatbotEvolutionAgentResult(
            outcome=ChatbotEvolutionAgentOutcome.issue_created,
            classification=RootCauseClassification.missing_content,
            issue_url="https://github.com/Azure/azure-sdk-pr/issues/456",
            source_url=_WIKI_SOURCE_URL,
            reasoning="Missing source identity.",
            confidence=0.9,
        )


@pytest.mark.parametrize(
    "issue_url",
    [
        "https://dev.azure.com/azure-sdk/internal/_workitems/edit/456",
        "https://github.com/Azure/azure-sdk-tools/issues/456",
    ],
)
def test_system_issue_result_requires_fallback_repository(
    issue_url: str,
) -> None:
    with pytest.raises(ValidationError):
        ChatbotEvolutionAgentResult(
            outcome=ChatbotEvolutionAgentOutcome.issue_created,
            classification=RootCauseClassification.retrieval_mismatch,
            issue_url=issue_url,
            reasoning="Invalid issue target.",
            confidence=0.9,
        )


def test_reused_issue_waits_for_validation() -> None:
    record = _record()

    ChatbotEvolutionAgentService()._apply_result(
        record,
        _result(ChatbotEvolutionAgentOutcome.issue_reused),
    )

    assert record.feedback is not None
    assert record.feedback.status == FeedbackStatus.pending_validation


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
        (
            ChatbotEvolutionAgentOutcome.validation_passed,
            FeedbackStatus.validation_passed,
        ),
        (
            ChatbotEvolutionAgentOutcome.validation_failed,
            FeedbackStatus.validation_failed,
        ),
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
    assert not ChatbotEvolutionAgentService._can_run(
        record,
        ChatbotEvolutionAgentMode.validation,
    )


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


def test_failed_validation_processing_is_eligible_for_retry() -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.failed,
    )
    assert record.feedback is not None
    record.feedback.issue_url = "https://github.com/Azure/azure-sdk-pr/issues/123"
    record.feedback.error = "agent_processing_failed"
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
async def test_failed_validation_processing_is_listed_as_pending() -> None:
    record = _record(
        qa_status=QAStatus.failed,
        feedback_status=FeedbackStatus.failed,
    )
    assert record.feedback is not None
    record.feedback.issue_url = "https://github.com/Azure/azure-sdk-pr/issues/123"
    record.feedback.error = "agent_processing_failed"
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
@pytest.mark.parametrize(
    "mode",
    [ChatbotEvolutionAgentMode.analysis, ChatbotEvolutionAgentMode.validation],
)
@pytest.mark.parametrize("poll_for_text", [False, True])
async def test_evolution_consumes_shared_response_stream(
    mode: ChatbotEvolutionAgentMode,
    poll_for_text: bool,
    caplog: pytest.LogCaptureFixture,
) -> None:
    outcome = (
        ChatbotEvolutionAgentOutcome.issue_created
        if mode == ChatbotEvolutionAgentMode.analysis
        else ChatbotEvolutionAgentOutcome.validation_passed
    )
    result_text = _result(outcome).model_dump_json()
    payload = ChatbotEvolutionAgentInput(
        conversation_id=_CONVERSATION_ID,
        conversation_type=ConversationType.teams_channel,
        evaluation_time=_BASE_TIME,
        mode=mode,
        issue_url=(
            "https://github.com/Azure/azure-sdk-pr/issues/123"
            if mode == ChatbotEvolutionAgentMode.validation
            else None
        ),
    )
    response = SimpleNamespace(
        id="resp-evolution",
        status="completed",
        output_text=result_text,
        error=None,
    )
    completed = (
        SimpleNamespace(id=response.id, status="completed", output_text="")
        if poll_for_text
        else response
    )
    stream = MagicMock()
    stream.__aiter__.return_value = [
        SimpleNamespace(type="response.created"),
        SimpleNamespace(type="response.output_text.delta", delta="partial"),
        SimpleNamespace(type="response.completed", response=completed),
    ]
    stream.close = AsyncMock()
    stream.response.headers = {"x-request-id": "trace-evolution"}
    project = MagicMock()
    client = project.get_openai_client.return_value
    client.responses.create = AsyncMock(return_value=stream)
    client.responses.retrieve = AsyncMock(return_value=response)
    service = ChatbotEvolutionAgentService(project_client=project)
    reference = FoundryAgentReference(name="evolution", version="14")
    service._resolve_agent_reference = AsyncMock(return_value=reference)
    caplog.set_level("INFO", logger="services.chatbot_evolution_agent_service")

    with (
        patch(
            "services.chatbot_evolution_agent_service.HostedAgentClient",
            wraps=HostedAgentClient,
        ) as shared_client,
        patch("utils.azure_ai_foundry_agent.asyncio.sleep", new=AsyncMock()),
    ):
        actual = await service._invoke_agent(payload)

    assert actual == result_text
    shared_client.assert_called_once_with(
        client, max_retries=1, stream_timeout=600
    )
    project.get_openai_client.assert_called_once_with(
        agent_name="azure-sdk-chatbot-evolution-agent"
    )
    client.responses.create.assert_awaited_once_with(
        input=[
            {
                "type": "message",
                "role": "user",
                "content": [{"type": "input_text", "text": payload.to_json()}],
            }
        ],
        store=True,
        stream=True,
        extra_body={"agent_reference": reference.to_extra_body()},
    )
    if poll_for_text:
        client.responses.retrieve.assert_awaited_once_with(response.id)
    else:
        client.responses.retrieve.assert_not_awaited()
    stream.close.assert_awaited_once()
    assert "trace_id=trace-evolution, response_id=resp-evolution" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "event_type", ["response.failed", "response.incomplete", "response.in_progress"]
)
async def test_evolution_does_not_retry_unfinished_stream(event_type: str) -> None:
    stream = MagicMock()
    stream.__aiter__.return_value = [SimpleNamespace(type=event_type)]
    stream.close = AsyncMock()
    project = MagicMock()
    client = project.get_openai_client.return_value
    client.responses.create = AsyncMock(return_value=stream)
    service = ChatbotEvolutionAgentService(project_client=project)
    service._resolve_agent_reference = AsyncMock(
        return_value=FoundryAgentReference(name="evolution", version="14")
    )
    payload = service._build_input(_record(), ChatbotEvolutionAgentMode.analysis)

    with pytest.raises(RuntimeError, match="after 1 attempts") as exc:
        await service._invoke_agent(payload)

    assert "without a response.completed event" in str(exc.value.__cause__)
    client.responses.create.assert_awaited_once()
    stream.close.assert_awaited_once()


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
