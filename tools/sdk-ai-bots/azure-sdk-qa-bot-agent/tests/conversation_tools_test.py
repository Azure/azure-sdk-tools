"""Tests for conversation retrieval tools."""

from __future__ import annotations

from datetime import datetime, timezone
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import pytest

from models.conversation import (
    ConversationDocumentType,
    ConversationMessageItem,
    ConversationType,
    Role,
)
from tools.conversation_tools import ConversationTools


@pytest.fixture(autouse=True)
def feedback_query():
    with patch("tools.conversation_tools.query_conversation_feedback", return_value=[]) as query:
        yield query


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
    assert result.messages[0].sender_id == "expert"


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


def _message(tenant_id):
    return ConversationMessageItem(
        id="bot-message",
        tenant_id=tenant_id,
        sender_role=Role.System,
        sender_id="bot",
        sender_name="Bot",
        content="Answer",
        created_at=datetime.now(timezone.utc),
        conversation_id="channel;messageid=123",
        conversation_type=ConversationType.teams_channel,
        conversation_partition="teams_channel:channel;messageid=123",
        trace_id="trace-123",
    )


def _feedback(index=0, reaction="bad"):
    return {
        "user_name": f"User {index}",
        "created_at": "2026-09-16T00:00:00+00:00",
        "reaction": reaction,
        "comment": "The example fails. 请检查。",
        "reasons": ["Incorrect"],
    }


async def _fetch(items, conversation_type="teams_channel"):
    return await ConversationTools(
        conversation_service=SimpleNamespace(
            get_messages_by_conversation_id=AsyncMock(return_value=items),
        )
    ).fetch_conversation(
        conversation_id="channel;messageid=123",
        conversation_type=conversation_type,
    )


@pytest.mark.asyncio
async def test_fetch_includes_all_reactions_and_exact_conversation(feedback_query):
    rows = [_feedback(i, reaction) for i, reaction in enumerate(["bad", "good", "unknown"])]
    feedback_query.return_value = rows
    result = await _fetch([_message(None), _message("typespec")])
    feedback_query.assert_awaited_once_with(
        conversation_id="channel;messageid=123",
        conversation_type="teams_channel",
    )
    assert result.model_dump(mode="json")["feedback"] == rows
    assert "feedback_truncated" not in result.model_dump()
    assert result.messages[-1].trace_id == "trace-123"


@pytest.mark.asyncio
@pytest.mark.parametrize("tenants", [[], [None], ["typespec", "azure_mcp"]])
async def test_feedback_retrieval_does_not_depend_on_transcript_tenant(feedback_query, tenants):
    feedback_query.return_value = [_feedback()]
    result = await _fetch([_message(tenant) for tenant in tenants])
    feedback_query.assert_awaited_once_with(
        conversation_id="channel;messageid=123",
        conversation_type="teams_channel",
    )
    assert result.tenant_id == next((tenant for tenant in tenants if tenant), None)
    assert result.model_dump(mode="json")["feedback"] == [_feedback()]


@pytest.mark.asyncio
async def test_invalid_conversation_type_does_not_query_feedback(feedback_query):
    result = await _fetch([], "unsupported")
    feedback_query.assert_not_awaited()
    assert result.found is False


@pytest.mark.asyncio
@pytest.mark.parametrize("count", [0, 100, 101, 150])
async def test_fetch_preserves_all_feedback(feedback_query, count):
    feedback_query.return_value = [_feedback(i) for i in range(count)]
    result = await _fetch([_message("typespec")])
    assert result.model_dump(mode="json")["feedback"] == feedback_query.return_value
    assert "feedback_truncated" not in result.model_dump()


@pytest.mark.asyncio
async def test_feedback_query_failure_preserves_transcript(feedback_query, caplog):
    feedback_query.side_effect = RuntimeError("Feedback unavailable")
    result = await _fetch([_message("typespec")])
    assert result.found is True
    assert result.tenant_id == "typespec"
    assert result.message_count == 1
    assert result.messages[0].content == "Answer"
    assert result.messages[0].trace_id == "trace-123"
    assert result.model_dump(mode="json")["feedback"] is None
    assert "Feedback lookup failed" in caplog.text


@pytest.mark.asyncio
async def test_invalid_feedback_preserves_transcript(feedback_query):
    feedback_query.return_value = [{"reaction": "invalid"}]
    result = await _fetch([_message("typespec")])
    assert result.found is True
    assert result.messages[0].content == "Answer"
    assert result.feedback is None


@pytest.mark.asyncio
async def test_transcript_failure_still_propagates(feedback_query):
    tools = ConversationTools(
        conversation_service=SimpleNamespace(
            get_messages_by_conversation_id=AsyncMock(
                side_effect=RuntimeError("Transcript unavailable")
            ),
        )
    )
    with pytest.raises(RuntimeError, match="Transcript unavailable"):
        await tools.fetch_conversation(
            conversation_id="channel;messageid=123",
            conversation_type="teams_channel",
        )
    feedback_query.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("user_name", [None, "SDK User"])
async def test_feedback_exposes_user_name_but_not_storage_identifiers(feedback_query, user_name):
    row = {**_feedback(), "user_name": user_name, "id": "stored-id", "link": "https://example.com"}
    feedback_query.return_value = [row]
    result = await _fetch([_message("typespec")])
    exposed = result.model_dump(mode="json")["feedback"][0]
    assert exposed == {**_feedback(), "user_name": user_name}
    assert "id" not in exposed
    assert "link" not in exposed


@pytest.mark.asyncio
async def test_feedback_accepts_missing_user_name(feedback_query):
    row = _feedback()
    del row["user_name"]
    feedback_query.return_value = [row]
    result = await _fetch([_message("typespec")])
    assert result.feedback[0].user_name is None