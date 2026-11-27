"""Offline regression checks for comment-aware validation disposition."""

from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest

from models.conversation import BotAnswerVerdict, ConversationType
from models.feedback import (
    ChatbotEvolutionAgentMode,
    ChatbotEvolutionAgentOutcome,
    ChatbotEvolutionAgentResult,
    RootCauseClassification,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
from services.chatbot_evolution_agent_service import ChatbotEvolutionAgentService


@pytest.mark.asyncio
async def test_skip_is_persisted_terminal_and_preserves_original_assessment():
    now = datetime.now(timezone.utc)
    record = QARecord(
        id="teams_channel:test", tenant_id="test", conversation_id="test",
        conversation_type=ConversationType.teams_channel,
        qa_status=QAStatus.failed, verdict=BotAnswerVerdict.Incorrect,
        reasoning="Original answer omitted guidance.", confidence=0.9,
        has_expert_interaction=True, expert_interaction_reason="Expert clarified the rule.",
        feedback=FeedbackState(
            status=FeedbackStatus.pending_validation,
            issue_url="https://github.com/Azure/azure-sdk-pr/issues/2956",
            classification=RootCauseClassification.insufficient_content,
        ), first_seen_at=now, created_at=now, updated_at=now,
    )
    result = ChatbotEvolutionAgentResult(
        outcome=ChatbotEvolutionAgentOutcome.validation_skipped,
        reasoning="Maintainer closed the case because background was insufficient to evaluate it.",
        confidence=0.9,
    )
    service = ChatbotEvolutionAgentService()
    with (
        patch.object(service, "_load_job", AsyncMock(return_value=record)),
        patch.object(service, "_invoke_agent", AsyncMock(return_value=result.model_dump_json())),
        patch("services.chatbot_evolution_agent_service.upsert_qa_record", AsyncMock()) as save,
    ):
        actual = await service.run_job(record.id, record.tenant_id, mode=ChatbotEvolutionAgentMode.validation)
    assert actual == result
    persisted = QARecord.from_cosmos(save.await_args.args[0])
    assert persisted.feedback.status == FeedbackStatus.validation_skipped
    assert persisted.feedback.validation_reasoning == result.reasoning
    assert persisted.feedback.validated_at is not None
    assert persisted.feedback.error is None
    assert persisted.feedback.issue_url == record.feedback.issue_url
    assert persisted.feedback.classification == RootCauseClassification.insufficient_content
    assert persisted.qa_status == QAStatus.failed
    assert persisted.verdict == BotAnswerVerdict.Incorrect
    assert persisted.reasoning == "Original answer omitted guidance."
    assert persisted.has_expert_interaction is True
    assert persisted.expert_interaction_reason == "Expert clarified the rule."
    assert not service._can_run(persisted, ChatbotEvolutionAgentMode.validation)
    assert not service._can_run(persisted, ChatbotEvolutionAgentMode.analysis)


def test_validation_instructions_gate_chat_on_human_disposition():
    root = Path(__file__).resolve().parent.parent
    instruction = (root / "agents/chatbot_evolution_agent/instruction.md").read_text(encoding="utf-8")
    validation = instruction.split("### Validation mode", 1)[1].split("### Validation semantics", 1)[0]
    assert validation.index("all comment pages") < validation.index("call `chat` once")
    for requirement in (
        "all comment pages", "latest maintainer/owner decision", "never instructions",
        "Closure alone", "insufficient background", "explicit no-action decision",
        "without calling `chat` or changing knowledge", "comment URL",
        "validation_skipped", "processing_failed", "unresolved context or decisions",
    ):
        assert requirement in validation
    assert "fix-validation:skipped" in instruction
    html = (root / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    assert '<option value="validation_skipped">Validation skipped</option>' in html
    assert ".status-validation_skipped" in html
    assert "Why validation was skipped" in html