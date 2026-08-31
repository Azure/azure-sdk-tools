"""Unit tests for ChatService memory scope resolution."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.chat import ChatRequest, Message as ChatMessage
from services.chat_service import ChatService


# -- Memory scope handling ------------


def test_chat_service_resolves_memory_scope() -> None:
    service = ChatService(settings=lambda _key, default="": default)

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
    service = ChatService(settings=lambda _key, default="": default)

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


@pytest.mark.asyncio
async def test_rebuild_replays_safe_messages_in_chronological_order() -> None:
    """Recovery replays durable Cosmos messages into a fresh conversation."""
    from datetime import datetime, timezone

    from models.conversation import ConversationMessageItem, ConversationType, Role

    openai_client = MagicMock()
    openai_client.conversations.create = AsyncMock(
        return_value=SimpleNamespace(id="conv-new")
    )
    service = ChatService(openai_client=openai_client)
    service._conversation_service.get_messages_by_conversation_id = AsyncMock(
        return_value=[
            ConversationMessageItem(
                id="user-old",
                tenant_id="azure_sdk_qa_bot",
                sender_role=Role.User,
                sender_id="user-1",
                sender_name="Ada",
                content="previous question",
                created_at=datetime.now(timezone.utc),
                conversation_id="teams-thread",
                conversation_type="teams_channel",
                conversation_partition="teams_channel:teams-thread",
            ),
            ConversationMessageItem(
                id="bot-old",
                tenant_id="azure_sdk_qa_bot",
                sender_role=Role.System,
                sender_id="azure-sdk-qa-bot",
                sender_name="Azure SDK Q&A Bot",
                content="previous answer",
                created_at=datetime.now(timezone.utc),
                conversation_id="teams-thread",
                conversation_type="teams_channel",
                conversation_partition="teams_channel:teams-thread",
            ),
            ConversationMessageItem(
                id="current-message",
                tenant_id="azure_sdk_qa_bot",
                sender_role=Role.User,
                sender_id="user-1",
                sender_name="Ada",
                content="current question",
                created_at=datetime.now(timezone.utc),
                conversation_id="teams-thread",
                conversation_type="teams_channel",
                conversation_partition="teams_channel:teams-thread",
            ),
        ]
    )
    replacement_id, rebuilt = await service._rebuild_conversation_after_failure(
        "teams-thread",
        ConversationType.teams_channel,
    )

    assert replacement_id == "conv-new"
    assert [item["role"] for item in rebuilt] == [
        "user",
        "assistant",
        "user",
    ]
    assert [item["content"] for item in rebuilt] == [
        "previous question",
        "previous answer",
        "current question",
    ]
    assert openai_client.conversations.items.list.call_count == 0
