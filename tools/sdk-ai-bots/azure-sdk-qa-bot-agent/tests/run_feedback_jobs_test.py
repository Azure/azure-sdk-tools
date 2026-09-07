"""Tests for daily evolution-loop orchestration."""

from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timezone
from unittest.mock import AsyncMock, MagicMock, patch

import httpx
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
async def test_candidate_restore_queues_sync_only_pipeline() -> None:
    queued_body: dict = {}

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal queued_body
        if request.url.path.endswith("/_apis/build/definitions"):
            return httpx.Response(
                200,
                json={
                    "value": [
                        {
                            "id": 42,
                            "name": (
                                "tools - sdk-ai-bots-knowledge-sync - "
                                "provision-and-sync"
                            ),
                        }
                    ]
                },
            )
        if request.method == "POST" and request.url.path.endswith("/_apis/build/builds"):
            queued_body = json.loads(request.content)
            return httpx.Response(200, json={"id": 123})
        if request.url.path.endswith("/_apis/build/builds/123"):
            return httpx.Response(
                200,
                json={"id": 123, "status": "completed", "result": "succeeded"},
            )
        return httpx.Response(404)

    env = {
        "SYSTEM_ACCESSTOKEN": "test-token",
        "ADO_COLLECTION_URI": "https://dev.azure.com/azure-sdk/",
        "ADO_PROJECT": "internal",
        "ADO_SOURCE_BRANCH": "refs/heads/test-branch",
        "KNOWLEDGE_SYNC_ENVIRONMENT": "dev",
    }
    with patch.dict(os.environ, env, clear=False):
        build_id = await run_feedback_jobs._restore_candidate_knowledge(
            transport=httpx.MockTransport(handler),
        )

    assert build_id == 123
    assert queued_body == {
        "definition": {"id": 42},
        "sourceBranch": "refs/heads/test-branch",
        "templateParameters": {
            "environment": "dev",
            "provisionInfrastructure": "false",
        },
    }


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
        patch.object(
            run_feedback_jobs,
            "_restore_candidate_knowledge",
            new=AsyncMock(return_value=123),
        ) as restore,
    ):
        await run_feedback_jobs._run(_args())

    qa_service.get_messages_in_period.assert_awaited_once()
    evolution.run_job.assert_awaited_once()
    assert restore.await_count == 2
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


@pytest.mark.asyncio
async def test_run_restores_candidate_before_production_validation() -> None:
    events: list[str] = []
    qa_service = MagicMock()
    qa_service.get_messages_in_period = AsyncMock(return_value=[])
    qa_service.upsert_threads_from_messages = AsyncMock(return_value=[])
    qa_service.list_analyzable = AsyncMock(
        return_value=[_record(qa_status=QAStatus.ongoing)]
    )
    qa_service.list_pending_validation = AsyncMock(
        return_value=[
            _record(
                qa_status=QAStatus.failed,
                feedback_status=FeedbackStatus.pending_validation,
            )
        ]
    )
    evolution = MagicMock()

    async def run_job(*_args, mode: ChatbotEvolutionAgentMode, **_kwargs):
        events.append(mode.value)
        outcome = (
            ChatbotEvolutionAgentOutcome.no_issue
            if mode == ChatbotEvolutionAgentMode.analysis
            else ChatbotEvolutionAgentOutcome.validation_passed
        )
        return _result(outcome)

    evolution.run_job = AsyncMock(side_effect=run_job)

    async def restore() -> int:
        events.append("restore")
        return 123

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
            "_restore_candidate_knowledge",
            new=AsyncMock(side_effect=restore),
        ),
        patch.object(
            run_feedback_jobs,
            "get_github_issue_state",
            new=AsyncMock(return_value="closed"),
        ),
        patch.object(run_feedback_jobs.app_config, "get", return_value="true"),
    ):
        await run_feedback_jobs._run(_args())

    assert events == ["restore", "analysis", "restore", "validation"]
