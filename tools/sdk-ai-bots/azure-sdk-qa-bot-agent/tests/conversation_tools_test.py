"""Tests for conversation retrieval tools."""

from __future__ import annotations

from datetime import datetime, timezone
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from models.conversation import (
    ConversationDocumentType,
    ConversationMessageItem,
    ConversationType,
    Role,
)
from tools.conversation_tools import ConversationTools


@pytest.mark.asyncio
async def test_fetch_conversation_preserves_complete_message_content():
    expert_correction = "Complete expert evidence. " * 100
    message = ConversationMessageItem(
        id="expert-message",
        tenant_id="azure_typespec_authoring",
        sender_role=Role.User,
        sender_id="expert",
        sender_name="SDK Expert",
        content=expert_correction,
        created_at=datetime.now(timezone.utc),
        conversation_id="conversation-1",
        conversation_type=ConversationType.teams_channel,
        conversation_partition="teams_channel:conversation-1",
        document_type=ConversationDocumentType.message,
    )
    conversation_service = SimpleNamespace(
        get_messages_by_conversation_id=AsyncMock(return_value=[message]),
    )

    result = await ConversationTools(
        conversation_service=conversation_service
    ).fetch_conversation(
        conversation_id="conversation-1",
        conversation_type=ConversationType.teams_channel.value,
    )

    assert result.truncated is False
    assert result.tenant_id == "azure_typespec_authoring"
    assert result.messages[0].content == expert_correction


@pytest.mark.asyncio
async def test_fetch_conversation_preserves_all_messages():
    messages = [
        ConversationMessageItem(
            id=f"message-{index}",
            sender_role=Role.User,
            sender_id="user",
            sender_name="User",
            content=f"content-{index}",
            created_at=datetime.now(timezone.utc),
            conversation_id="conversation-1",
            conversation_type=ConversationType.teams_channel,
            conversation_partition="teams_channel:conversation-1",
            document_type=ConversationDocumentType.message,
        )
        for index in range(101)
    ]
    conversation_service = SimpleNamespace(
        get_messages_by_conversation_id=AsyncMock(return_value=messages),
    )

    result = await ConversationTools(
        conversation_service=conversation_service
    ).fetch_conversation(
        conversation_id="conversation-1",
        conversation_type=ConversationType.teams_channel.value,
    )

    assert result.truncated is False
    assert len(result.messages) == 101
    assert result.messages[0].id == "message-0"
    assert result.messages[-1].id == "message-100"