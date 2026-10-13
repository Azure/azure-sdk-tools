"""Tests for daily evolution-loop orchestration."""

from __future__ import annotations

import argparse
from datetime import datetime, timedelta, timezone
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from models.conversation import (
    BotAnswerVerdict,
    ConversationMessageItem,
    ConversationType,
    Role,
)
from models.feedback import (
    ChatbotEvolutionAgentMode,
    ChatbotEvolutionAgentOutcome,
    ChatbotEvolutionAgentResult,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
from scripts import run_feedback_jobs
from services.qa_record_service import QARecordService
from utils.channel_policy import is_testing_channel


def _record(
    *,
    qa_status: QAStatus,
    feedback_status: FeedbackStatus | None = None,
) -> QARecord:
    now = datetime(2026, 7, 1, tzinfo=timezone.utc)
    feedback = (
        FeedbackState(
            status=feedback_status,
            issue_url=(
                "https://github.com/Azure/azure-sdk-pr/issues/123"
                if feedback_status == FeedbackStatus.pending_validation
                else None
            ),
            created_at=now,
            updated_at=now,
        )
        if feedback_status
        else None
    )
    return QARecord(
        id="teams_channel:conversation-1",
        tenant_id="typespec_channel_qa_bot",
        conversation_id="conversation-1",
        conversation_type=ConversationType.teams_channel,
        qa_status=qa_status,
        feedback=feedback,
        first_seen_at=now,
        created_at=now,
        updated_at=now,
    )


def _result(
    outcome: ChatbotEvolutionAgentOutcome,
) -> ChatbotEvolutionAgentResult:
    return ChatbotEvolutionAgentResult(
        outcome=outcome,
        reasoning="Grounded.",
        confidence=0.9,
    )


def _args() -> argparse.Namespace:
    return argparse.Namespace(
        start="2026-07-01T00:00:00+00:00",
        end="2026-07-02T00:00:00+00:00",
        days=1,
        tenant=None,
        limit=None,
        dry_run=False,
    )


@pytest.mark.parametrize(
    "name",
    [
        "Stress (testing)",
        "Azure SDK QA bot for Python Testing  🐍",
        "Azure SDK QA Bot - Auto Reply - Test",
        "Smoke-Tests",
    ],
)
def test_testing_channel_names_are_excluded(name: str) -> None:
    assert is_testing_channel(name)


@pytest.mark.parametrize(
    "name",
    [
        "Python Test V-Team",
        "Test-Proxy - Questions, Help, and Discussion",
        "TypeSpec Discussion",
    ],
)
def test_product_channels_with_test_in_name_are_not_excluded(name: str) -> None:
    assert not is_testing_channel(name)


@pytest.mark.asyncio
async def test_excluded_channels_use_shared_policy() -> None:
    assert run_feedback_jobs.is_testing_channel is is_testing_channel
    data = run_feedback_jobs.yaml.safe_dump({"channels": [
        {"id": "testing", "name": "Stress (testing)"},
        {"id": "smoke", "name": "Smoke-Tests"},
        {"id": "product", "name": "Python Test V-Team"},
        {"id": "unnamed"},
        {"name": "Stress (testing)"},
    ]}).encode("utf-8")
    with (
        patch.object(run_feedback_jobs.app_config, "get", side_effect=["config", "channel.yaml"]),
        patch.object(run_feedback_jobs, "download_blob", new=AsyncMock(return_value=data)),
    ):
        assert await run_feedback_jobs._load_excluded_channels() == {"testing", "smoke"}


@pytest.mark.asyncio
async def test_excluded_channel_load_failure_stops_the_job() -> None:
    with (
        patch.object(
            run_feedback_jobs.app_config,
            "get",
            side_effect=["config", "channel.yaml"],
        ),
        patch.object(
            run_feedback_jobs,
            "download_blob",
            new=AsyncMock(side_effect=RuntimeError("storage unavailable")),
        ),
        pytest.raises(RuntimeError, match="storage unavailable"),
    ):
        await run_feedback_jobs._load_excluded_channels()


@pytest.mark.asyncio
@pytest.mark.parametrize(("dry_run", "enabled"), [(True, "true"), (False, "false")])
async def test_disabled_agent_processing_does_not_recover_or_close_jobs(
    dry_run: bool, enabled: str,
) -> None:
    qa_service = MagicMock()
    qa_service.get_messages_in_period = AsyncMock(return_value=[])
    qa_service.upsert_threads_from_messages = AsyncMock(return_value=[])
    qa_service.close_inactive_threads = AsyncMock()
    evolution = MagicMock()
    evolution.recover_stale_jobs = AsyncMock()
    args = _args()
    args.dry_run = dry_run
    with (
        patch.object(run_feedback_jobs, "QARecordService", return_value=qa_service),
        patch.object(run_feedback_jobs, "ChatbotEvolutionAgentService", return_value=evolution),
        patch.object(run_feedback_jobs, "_load_excluded_channels", new=AsyncMock(return_value=set())),
        patch.object(run_feedback_jobs.app_config, "get", return_value=enabled),
    ):
        await run_feedback_jobs._run(args)
    evolution.recover_stale_jobs.assert_not_awaited()
    qa_service.close_inactive_threads.assert_not_awaited()
    qa_service.list_analyzable.assert_not_called()


@pytest.mark.asyncio
async def test_run_invokes_agent_for_analysis_without_external_evaluator() -> None:
    qa_service = MagicMock()
    qa_service.get_messages_in_period = AsyncMock(return_value=[])
    qa_service.upsert_threads_from_messages = AsyncMock(return_value=[])
    qa_service.close_inactive_threads = AsyncMock(return_value=0)
    qa_service.list_pending_validation = AsyncMock(return_value=[])
    qa_service.list_analyzable = AsyncMock(
        return_value=[_record(qa_status=QAStatus.ongoing)]
    )
    evolution = MagicMock()
    evolution.recover_stale_jobs = AsyncMock(return_value=0)
    evolution.run_job = AsyncMock(
        return_value=_result(
            ChatbotEvolutionAgentOutcome.no_issue,
        )
    )

    with (
        patch.object(run_feedback_jobs, "QARecordService", return_value=qa_service),
        patch.object(
            run_feedback_jobs,
            "ChatbotEvolutionAgentService",
            return_value=evolution,
        ),
        patch.object(
            run_feedback_jobs,
            "_load_excluded_channels",
            new=AsyncMock(return_value=set()),
        ),
        patch.object(
            run_feedback_jobs.app_config,
            "get",
            return_value="true",
        ),
    ):
        await run_feedback_jobs._run(_args())

    qa_service.get_messages_in_period.assert_awaited_once()
    evolution.recover_stale_jobs.assert_awaited_once_with(
        tenant_id=None, excluded_channels=set()
    )
    qa_service.close_inactive_threads.assert_awaited_once_with(
        tenant_id=None, excluded_channels=set()
    )
    evolution.run_job.assert_awaited_once()
    assert (
        evolution.run_job.await_args.kwargs["mode"]
        == ChatbotEvolutionAgentMode.analysis
    )


@pytest.mark.asyncio
async def test_scan_auto_closes_inactive_record_without_agent_invocation(caplog) -> None:
    caplog.set_level("INFO", logger="run_feedback_jobs")
    record = _record(qa_status=QAStatus.ongoing)
    record.last_activity_at = record.created_at
    now = record.last_activity_at + timedelta(hours=144)
    stored = {**record.to_cosmos(), "_etag": "old-etag"}
    message = ConversationMessageItem(
        id="bot-answer",
        tenant_id=record.tenant_id,
        sender_role=Role.System,
        sender_id="bot",
        sender_name="bot",
        content="Answer.",
        created_at=record.last_activity_at,
        conversation_id=record.conversation_id,
        conversation_type=record.conversation_type,
        conversation_partition=record.id,
    )

    async def query(**kwargs):
        return [stored] if stored["qa_status"] == kwargs["qa_status"] else []

    async def replace(document, *, etag):
        assert etag == stored["_etag"]
        stored.update(document)
        return True

    evolution = MagicMock()
    evolution.recover_stale_jobs = AsyncMock(return_value=0)
    evolution.run_job = AsyncMock()
    args = _args()
    args.tenant = record.tenant_id
    with (
        patch("services.qa_record_service._now", return_value=now),
        patch.object(QARecordService, "get_messages_in_period", new=AsyncMock(return_value=[])),
        patch.object(QARecordService, "upsert_threads_from_messages", new=AsyncMock(return_value=[])),
        patch("services.qa_record_service.query_qa_records_by_qa_status", new=AsyncMock(side_effect=query)),
        patch("services.qa_record_service.query_qa_records_by_feedback_status", new=AsyncMock(return_value=[])),
        patch(
            "services.qa_record_service.ConversationService.get_messages_by_conversation_id",
            new=AsyncMock(return_value=[message]),
        ),
        patch("services.qa_record_service.query_conversation_feedback", new=AsyncMock(return_value=[])),
        patch("services.qa_record_service.replace_qa_record_if_unchanged", new=AsyncMock(side_effect=replace)),
        patch.object(run_feedback_jobs, "ChatbotEvolutionAgentService", return_value=evolution),
        patch.object(run_feedback_jobs, "_load_excluded_channels", new=AsyncMock(return_value=set())),
        patch.object(run_feedback_jobs.app_config, "get", return_value="true"),
    ):
        await run_feedback_jobs._run(args)

    assert stored["qa_status"] == QAStatus.finished.value
    assert stored["verdict"] == BotAnswerVerdict.Correct.value
    evolution.run_job.assert_not_awaited()
    assert "ongoing=0 finished=1" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("outcome", "validated", "failed", "skipped"),
    [
        (ChatbotEvolutionAgentOutcome.validation_passed, 1, 0, 0),
        (ChatbotEvolutionAgentOutcome.validation_failed, 0, 1, 0),
        (ChatbotEvolutionAgentOutcome.validation_skipped, 0, 0, 1),
        (ChatbotEvolutionAgentOutcome.processing_failed, 0, 1, 0),
        (None, 0, 1, 0),
    ],
)
async def test_run_validates_only_after_issue_closes(
    outcome, validated, failed, skipped, caplog
) -> None:
    caplog.set_level("INFO", logger="run_feedback_jobs")
    qa_service = MagicMock()
    qa_service.get_messages_in_period = AsyncMock(return_value=[])
    qa_service.upsert_threads_from_messages = AsyncMock(return_value=[])
    qa_service.close_inactive_threads = AsyncMock(return_value=0)
    qa_service.list_pending_validation = AsyncMock(
        return_value=[
            _record(
                qa_status=QAStatus.failed,
                feedback_status=FeedbackStatus.pending_validation,
            )
        ]
    )
    qa_service.list_analyzable = AsyncMock(return_value=[])
    evolution = MagicMock()
    evolution.recover_stale_jobs = AsyncMock(return_value=0)
    evolution.run_job = AsyncMock(
        return_value=_result(outcome) if outcome is not None else None
    )

    with (
        patch.object(run_feedback_jobs, "QARecordService", return_value=qa_service),
        patch.object(
            run_feedback_jobs,
            "ChatbotEvolutionAgentService",
            return_value=evolution,
        ),
        patch.object(
            run_feedback_jobs,
            "_load_excluded_channels",
            new=AsyncMock(return_value=set()),
        ),
        patch.object(
            run_feedback_jobs,
            "get_issue_state",
            new=AsyncMock(return_value="closed"),
        ),
        patch.object(
            run_feedback_jobs.app_config,
            "get",
            return_value="true",
        ),
    ):
        await run_feedback_jobs._run(_args())

    assert (
        evolution.run_job.await_args.kwargs["mode"]
        == ChatbotEvolutionAgentMode.validation
    )
    assert (
        f"waiting-validation=0 validated={validated} validation-failed={failed} "
        f"validation-skipped={skipped} evolution-failed=0 skipped=0"
    ) in caplog.text
    if skipped:
        assert "Validation skipped for teams_channel:conversation-1" in caplog.text
