"""Unit tests for ChatService memory scope resolution."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest
from openai.types.responses import ResponseOutputMessage

from models.chat import ChatRequest, Message as ChatMessage
from models.knowledge import Reference
from services.chat_service import ChatService

# -- Memory scope handling ------------


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


@pytest.mark.asyncio
async def test_rebuild_replays_safe_messages_in_chronological_order() -> None:
    """Recovery replays durable Cosmos messages into a fresh conversation."""
    from datetime import datetime, timezone

    from models.conversation import ConversationMessageItem, ConversationType, Role

    openai_client = MagicMock()
    openai_client.conversations.create = AsyncMock(
        return_value=SimpleNamespace(id="conv-new")
    )
    service = ChatService()
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
        openai_client,
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


def test_stateless_session_ids_are_isolated_by_agent() -> None:
    """Warm stateless sessions are reused only by the agent that created them."""
    service = ChatService(openai_client=MagicMock())

    service._set_stateless_session_id("azure-sdk-agent", "sdk-session")
    service._set_stateless_session_id("azure-mcp-agent", "mcp-session")

    assert service._get_stateless_session_id("azure-sdk-agent") == "sdk-session"
    assert service._get_stateless_session_id("azure-mcp-agent") == "mcp-session"


def test_extract_reference_candidates_from_multiple_tool_sources() -> None:
    service = ChatService()
    output = json.dumps(
        {
            "results": [
                {
                    "title": "Knowledge document",
                    "source": "knowledge",
                    "link": "https://example.com/knowledge",
                    "content": "Supporting evidence",
                },
                {
                    "title": "GitHub issue",
                    "html_url": "https://github.com/Azure/example/issues/1",
                    "body": "Issue evidence",
                },
                {
                    "title": "Duplicate document",
                    "url": "https://example.com/knowledge",
                    "snippet": "Additional evidence",
                },
            ]
        }
    )
    item = ResponseOutputMessage.model_validate(
        {
            "id": "tool-output",
            "type": "message",
            "role": "assistant",
            "status": "completed",
            "content": [],
            "call_id": "call-1",
            "output": output,
        }
    )

    candidates = service._extract_reference_candidates([item])

    assert [candidate.link for candidate in candidates] == [
        "https://example.com/knowledge",
        "https://github.com/Azure/example/issues/1",
    ]
    assert candidates[0].content == "Supporting evidence\nAdditional evidence"


@pytest.mark.asyncio
async def test_select_references_validates_and_deduplicates_model_urls() -> None:
    selected_url = "https://example.com/selected"
    openai_client = MagicMock()
    openai_client.chat.completions.create = AsyncMock(
        return_value=SimpleNamespace(
            choices=[
                SimpleNamespace(
                    message=SimpleNamespace(
                        content=json.dumps(
                            {
                                "urls": [
                                    selected_url,
                                    "https://example.com/unknown",
                                    selected_url,
                                ]
                            }
                        )
                    )
                )
            ]
        )
    )
    project_client = MagicMock()
    project_client.get_openai_client.return_value = openai_client
    service = ChatService(
        project_client=project_client,
        settings=lambda _key, default=None: default,
    )
    candidates = [
        Reference(
            title="Selected",
            source="test",
            link=selected_url,
            content="Direct support",
        ),
        Reference(
            title="Unused",
            source="test",
            link="https://example.com/unused",
            content="Unrelated",
        ),
    ]

    selected = await service._select_references("Grounded answer", candidates)

    assert selected == [candidates[0]]
    openai_client.chat.completions.create.assert_awaited_once()


@pytest.mark.asyncio
async def test_postprocess_repairs_missing_references_from_tool_candidates() -> None:
    candidate = Reference(
        title="Supporting document",
        source="test",
        link="https://example.com/support",
        content="Direct support",
    )
    tool_output = ResponseOutputMessage.model_validate(
        {
            "id": "tool-output",
            "type": "message",
            "role": "assistant",
            "status": "completed",
            "content": [],
            "call_id": "call-1",
            "output": candidate.model_dump_json(),
        }
    )
    response = SimpleNamespace(
        id="response-1",
        output_text="Grounded answer without a References section.",
        output=[tool_output],
    )
    service = ChatService(settings=lambda _key, default=None: default)
    service._select_references = AsyncMock(return_value=[candidate])
    request = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(role="user", content="Question"),
    )

    result = await service._postprocess(request, response, None)

    assert result.answer == response.output_text
    assert result.references == [candidate]
    service._select_references.assert_awaited_once_with(
        response.output_text, [candidate]
    )


@pytest.mark.asyncio
async def test_postprocess_keeps_model_references_without_repair_call() -> None:
    response = SimpleNamespace(
        id="response-1",
        output_text=(
            "Grounded answer.\n\n**References**\n"
            "- [Supporting document](https://example.com/support)"
        ),
        output=[],
    )
    service = ChatService(settings=lambda _key, default=None: default)
    service._select_references = AsyncMock()
    request = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(role="user", content="Question"),
    )

    result = await service._postprocess(request, response, None)

    assert result.answer == "Grounded answer."
    assert result.references is not None
    assert [reference.link for reference in result.references] == [
        "https://example.com/support"
    ]
    service._select_references.assert_not_awaited()
