"""Tests for daily evolution-loop orchestration."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from models.conversation import ConversationType
from models.feedback import (
    ChatbotEvolutionAgentMode,
    ChatbotEvolutionAgentOutcome,
    ChatbotEvolutionAgentResult,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
from scripts import run_feedback_jobs


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
    assert run_feedback_jobs._is_testing_channel(name)


@pytest.mark.parametrize(
    "name",
    [
        "Python Test V-Team",
        "Test-Proxy - Questions, Help, and Discussion",
        "TypeSpec Discussion",
    ],
)
def test_product_channels_with_test_in_name_are_not_excluded(name: str) -> None:
    assert not run_feedback_jobs._is_testing_channel(name)


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
async def test_run_invokes_agent_for_analysis_without_external_evaluator() -> None:
    qa_service = MagicMock()
    qa_service.get_messages_in_period = AsyncMock(return_value=[])
    qa_service.upsert_threads_from_messages = AsyncMock(return_value=[])
    qa_service.list_pending_validation = AsyncMock(return_value=[])
    qa_service.list_analyzable = AsyncMock(
        return_value=[_record(qa_status=QAStatus.ongoing)]
    )
    evolution = MagicMock()
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
    evolution.run_job.assert_awaited_once()
    assert (
        evolution.run_job.await_args.kwargs["mode"]
        == ChatbotEvolutionAgentMode.analysis
    )


@pytest.mark.asyncio
async def test_run_validates_only_after_issue_closes() -> None:
    qa_service = MagicMock()
    qa_service.get_messages_in_period = AsyncMock(return_value=[])
    qa_service.upsert_threads_from_messages = AsyncMock(return_value=[])
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
    evolution.run_job = AsyncMock(
        return_value=_result(
            ChatbotEvolutionAgentOutcome.validation_passed,
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
            run_feedback_jobs,
            "get_github_issue_state",
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
