"""Unit tests for the intention classification service."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.chat import Message
from models.intention import IntentionRequest, IntentionResponse
from services.intention_service import IntentionService


@pytest.fixture
def service() -> IntentionService:
    return IntentionService()


@pytest.mark.asyncio
async def test_expert_reply_skips(service: IntentionService) -> None:
    req = IntentionRequest(
        message=Message(
            role="user", content="How do I fix this CI error?", user_id="user-1"
        ),
        conversation_id="conv-123",
        conversation_type="teams_channel",
    )

    mock_conversation_service = AsyncMock()
    mock_conversation_service.has_expert_reply = AsyncMock(return_value=True)
    service._conversation_service = mock_conversation_service

    resp = await service.classify(req)
    assert resp.should_respond is False
    assert resp.reason == "expert_already_replied"


@pytest.mark.asyncio
async def test_llm_classifies_technical_question(service: IntentionService) -> None:
    mock_choice = MagicMock()
    mock_choice.message.content = (
        '{"should_respond": true, "reason": "The user is asking about SDK generation."}'
    )
    mock_response = MagicMock()
    mock_response.choices = [mock_choice]

    mock_completions = AsyncMock()
    mock_completions.create = AsyncMock(return_value=mock_response)

    mock_openai_client = MagicMock()
    mock_openai_client.chat.completions = mock_completions

    mock_project_client = MagicMock()
    mock_project_client.get_openai_client.return_value = mock_openai_client

    req = IntentionRequest(
        message=Message(
            role="user", content="How do I generate an SDK from my TypeSpec?"
        ),
    )

    with patch(
        "services.intention_service.get_project_client",
        return_value=mock_project_client,
    ):
        resp = await service.classify(req)

    assert resp.should_respond is True
    assert resp.reason == "The user is asking about SDK generation."


@pytest.mark.asyncio
async def test_llm_classifies_casual_message(service: IntentionService) -> None:
    mock_choice = MagicMock()
    mock_choice.message.content = (
        '{"should_respond": false, "reason": "The message is a casual remark."}'
    )
    mock_response = MagicMock()
    mock_response.choices = [mock_choice]

    mock_completions = AsyncMock()
    mock_completions.create = AsyncMock(return_value=mock_response)

    mock_openai_client = MagicMock()
    mock_openai_client.chat.completions = mock_completions

    mock_project_client = MagicMock()
    mock_project_client.get_openai_client.return_value = mock_openai_client

    req = IntentionRequest(
        message=Message(role="user", content="Thanks everyone, happy Friday!"),
    )

    with patch(
        "services.intention_service.get_project_client",
        return_value=mock_project_client,
    ):
        resp = await service.classify(req)

    assert resp.should_respond is False
    assert resp.reason == "The message is a casual remark."


@pytest.mark.asyncio
async def test_llm_error_defaults_to_respond(service: IntentionService) -> None:
    mock_project_client = MagicMock()
    mock_openai_client = MagicMock()
    mock_openai_client.chat.completions = AsyncMock(
        side_effect=RuntimeError("connection failed")
    )
    mock_project_client.get_openai_client.return_value = mock_openai_client

    req = IntentionRequest(
        message=Message(role="user", content="How do I fix this?"),
    )

    with patch(
        "services.intention_service.get_project_client",
        return_value=mock_project_client,
    ):
        resp = await service.classify(req)

    assert resp.should_respond is True
    assert resp.reason == "llm_error_default_respond"
