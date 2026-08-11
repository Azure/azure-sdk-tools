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
        get_feedback_by_conversation_id=AsyncMock(return_value=[]),
    )

    result = await ConversationTools(
        conversation_service=conversation_service
    ).fetch_conversation(
        conversation_id="conversation-1",
        conversation_type=ConversationType.teams_channel.value,
    )

    assert result.truncated is False
    assert result.messages[0].content == expert_correction