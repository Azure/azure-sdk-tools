"""Tests for ChatService memory scope resolution."""

from __future__ import annotations

import sys
from pathlib import Path

from models.chat import ChatRequest, Message as ChatMessage

# Ensure the project root is on sys.path so ``services`` resolves.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from services.chat_service import ChatService


class _FakeEvent:
    def __init__(self, type_: str, response=None) -> None:
        self.type = type_
        self.response = response


async def _fake_stream(events):
    for ev in events:
        yield ev


def test_chat_service_resolves_memory_scope() -> None:
    service = ChatService()

    # user_id present → user_{user_id}
    with_user_id = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(
            role="user", content="hello", user_id="29:orgid:abc-def-123"
        ),
    )
    assert service._resolve_memory_scope(with_user_id) == "user_29orgidabc-def-123"

    # user_id present even when extra fields are set
    with_both = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(
            role="user", content="hello", user_id="29:orgid:abc-def-123"
        ),
    )
    assert service._resolve_memory_scope(with_both) == "user_29orgidabc-def-123"

    # No user_id → None
    no_user_id = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(role="user", content="hello"),
    )
    assert service._resolve_memory_scope(no_user_id) is None

    # No user_id → None
    scope_but_no_user = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(role="user", content="hello"),
    )
    assert service._resolve_memory_scope(scope_but_no_user) is None

    assert (
        service._build_memory_scope_message("my-scope")
        == "[memory_scope] value=my-scope"
    )


def test_chat_service_returns_none_when_user_id_empty() -> None:
    """Empty/whitespace user_id returns None."""
    service = ChatService()

    empty_id = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(role="user", content="hello", user_id=""),
    )
    assert service._resolve_memory_scope(empty_id) is None

    whitespace_id = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(role="user", content="hello", user_id="  "),
    )
    assert service._resolve_memory_scope(whitespace_id) is None


def test_collect_completed_response_returns_response() -> None:
    """A stream containing a response.completed event yields its response."""
    import asyncio

    sentinel = object()
    events = [
        _FakeEvent("response.created"),
        _FakeEvent("response.completed", response=sentinel),
    ]
    response, last = asyncio.run(
        ChatService._collect_completed_response(_fake_stream(events))
    )
    assert response is sentinel
    assert last == "response.completed"


def test_collect_completed_response_none_when_no_completed_event() -> None:
    """A stream that ends without a completed event yields (None, last_event)."""
    import asyncio

    events = [_FakeEvent("response.created"), _FakeEvent("response.in_progress")]
    response, last = asyncio.run(
        ChatService._collect_completed_response(_fake_stream(events))
    )
    assert response is None
    assert last == "response.in_progress"
