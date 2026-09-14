"""Unit tests for ChatService memory scope resolution."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest
from openai.types.responses import (
    ResponseFunctionToolCall,
    ResponseFunctionToolCallOutputItem,
    ResponseFunctionWebSearch,
    ResponseOutputMessage,
)

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.chat import ChatRequest, Message as ChatMessage
from models.knowledge import Reference, SearchKnowledgeBaseResult
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


def test_postprocess_captures_ordered_tool_trace_and_all_knowledge() -> None:
    service = ChatService(settings=lambda _key, default="": default)
    search_result = SearchKnowledgeBaseResult(
        results=[
            Reference(
                title="TypeSpec guide",
                source="typespec",
                blob_path="docs/guide.md",
                link="https://example.test/guide",
                content="Authoritative guidance.",
            ),
            Reference(
                title="TypeSpec guide",
                source="typespec",
                blob_path="docs/guide.md",
                link="https://example.test/guide",
                content="A second relevant chunk.",
            ),
        ]
    )
    items = [
        ResponseFunctionToolCall(
            arguments='{"queries":["operation id"],"mode":"hybrid"}',
            call_id="call-search",
            name="search_knowledge_base",
            type="function_call",
        ),
        ResponseOutputMessage.model_construct(
            id="output-search",
            call_id="call-search",
            output=search_result.model_dump_json(),
            content=[],
            role="assistant",
            status="completed",
            type="message",
        ),
        ResponseFunctionToolCall(
            arguments='{"directory":"Azure/typespec-azure","regex_pattern":"clientLocation"}',
            call_id="call-grep",
            name="file_access_grep",
            type="function_call",
        ),
        ResponseFunctionToolCallOutputItem(
            id="output-grep",
            call_id="call-grep",
            output="packages/compiler/src/checker.ts:42: clientLocation",
            status="completed",
            type="function_call_output",
        ),
        ResponseFunctionWebSearch(
            id="web-search-1",
            action={
                "type": "search",
                "query": "TypeSpec implementation",
                "sources": [
                    {
                        "type": "url",
                        "url": "https://example.test/source",
                    }
                ],
            },
            status="completed",
            type="web_search_call",
        ),
    ]
    req = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        message=ChatMessage(role="user", content="How is this implemented?"),
        with_full_context=True,
    )
    response = SimpleNamespace(
        id="response-1",
        output=items,
        output_text="Use the documented API.",
    )

    result = service._postprocess(req, response, agent_conversation_id=None)

    contexts = json.loads(result.full_context or "[]")
    traces = [
        json.loads(context["document_content"])
        for context in contexts
        if context["document_source"] == "agent_tool_trace"
    ]
    evidence = [
        context
        for context in contexts
        if context["document_source"] != "agent_tool_trace"
    ]

    assert [trace["tool_name"] for trace in traces] == [
        "search_knowledge_base",
        "file_access_grep",
        "web_search",
    ]
    assert traces[0]["arguments"] == {
        "queries": ["operation id"],
        "mode": "hybrid",
    }
    assert "Authoritative guidance." in traces[0]["output"]
    assert "clientLocation" in traces[1]["output"]
    assert traces[2]["arguments"] == {
        "type": "search",
        "query": "TypeSpec implementation",
    }
    assert "https://example.test/source" in traces[2]["output"]
    assert evidence == [
        {
            "document_title": "TypeSpec guide",
            "document_link": "https://example.test/guide",
            "document_source": "typespec",
            "document_content": "Authoritative guidance.",
            "score": 0.0,
        },
        {
            "document_title": "TypeSpec guide",
            "document_link": "https://example.test/guide",
            "document_source": "typespec",
            "document_content": "A second relevant chunk.",
            "score": 0.0,
        }
    ]


def test_tool_trace_preserves_full_output() -> None:
    service = ChatService(settings=lambda _key, default="": default)
    output = "x" * 10_000
    items = [
        ResponseFunctionToolCall(
            arguments='{"url":"https://example.test"}',
            call_id="call-1",
            name="web_fetch",
            type="function_call",
        ),
        ResponseFunctionToolCallOutputItem(
            id="output-1",
            call_id="call-1",
            output=output,
            status="completed",
            type="function_call_output",
        ),
    ]

    contexts = service._build_tool_trace_contexts(items, "response-1")

    assert len(contexts) == 1
    assert json.loads(contexts[0].document_content)["output"] == output


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
