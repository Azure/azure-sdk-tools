"""Unit tests for recording the should_reply flag during intention classification.

These tests are hermetic: they stub the conversation service so no Cosmos DB
or LLM access is required.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.chat import Message
from models.conversation import ConversationType, Role
from models.intention import IntentionRequest, IntentionResponse
from services.intention_service import IntentionService


def _make_service() -> IntentionService:
    """Build an IntentionService without loading the classify prompt from disk."""
    service = IntentionService.__new__(IntentionService)
    return service


@pytest.mark.asyncio
async def test_record_should_reply_persists_decision() -> None:
    """When message id and conversation metadata are present, should_reply is recorded."""
    service = _make_service()
    recorded: dict[str, object] = {}

    class _ConversationServiceStub:
        async def record_should_reply(
            self,
            message_id: str,
            conversation_id: str,
            conversation_type: ConversationType,
            should_reply: bool,
        ) -> None:
            recorded.update(
                message_id=message_id,
                conversation_id=conversation_id,
                conversation_type=conversation_type,
                should_reply=should_reply,
            )

    service._conversation_service = _ConversationServiceStub()

    req = IntentionRequest(
        message=Message(id="msg-1", role=Role.User, content="Why does my build fail?"),
        conversation_id="conv-1",
        conversation_type=ConversationType.teams_channel,
    )
    response = IntentionResponse(should_respond=True, reason="in_scope")

    await service._record_should_reply(req, response)

    assert recorded == {
        "message_id": "msg-1",
        "conversation_id": "conv-1",
        "conversation_type": ConversationType.teams_channel,
        "should_reply": True,
    }


@pytest.mark.asyncio
async def test_record_should_reply_skipped_without_message_id() -> None:
    """Without a message id the recording is skipped (no record call)."""
    service = _make_service()
    called = False

    class _ConversationServiceStub:
        async def record_should_reply(self, *args, **kwargs) -> None:
            nonlocal called
            called = True

    service._conversation_service = _ConversationServiceStub()

    req = IntentionRequest(
        message=Message(role=Role.User, content="Hello"),
        conversation_id="conv-1",
        conversation_type=ConversationType.teams_channel,
    )
    response = IntentionResponse(should_respond=False, reason="casual")

    await service._record_should_reply(req, response)

    assert called is False


@pytest.mark.asyncio
async def test_record_should_reply_swallows_errors() -> None:
    """Recording failures must not propagate and break the intention response."""
    service = _make_service()

    class _ConversationServiceStub:
        async def record_should_reply(self, *args, **kwargs) -> None:
            raise RuntimeError("cosmos unavailable")

    service._conversation_service = _ConversationServiceStub()

    req = IntentionRequest(
        message=Message(id="msg-1", role=Role.User, content="Question?"),
        conversation_id="conv-1",
        conversation_type=ConversationType.teams_channel,
    )
    response = IntentionResponse(should_respond=True, reason="in_scope")

    # Should not raise.
    await service._record_should_reply(req, response)
