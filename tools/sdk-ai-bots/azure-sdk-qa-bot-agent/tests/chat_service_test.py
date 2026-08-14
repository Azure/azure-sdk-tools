"""Unit tests for ChatService memory scope resolution."""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import patch

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.chat import ChatRequest, Message as ChatMessage
from config.tenant_config import TenantID, get_tenant_config
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


def test_chat_service_resolves_agent_by_tenant() -> None:
    settings = {
        "AI_FOUNDRY_AGENT_NAME": "sdk-agent",
        "AI_FOUNDRY_AGENT_VERSION": "3",
        "AZURE_MCP_SERVER_AGENT_NAME": "mcp-agent",
        "AZURE_MCP_SERVER_AGENT_VERSION": "7",
    }

    with patch("services.chat_service.cfg", side_effect=settings.get):
        assert ChatService._resolve_agent_configuration(
            get_tenant_config(TenantID.TYPESPEC_CHANNEL_QA_BOT)
        ) == ("sdk-agent", "3")
        assert ChatService._resolve_agent_configuration(
            get_tenant_config(TenantID.AZURE_MCP_SERVER)
        ) == ("mcp-agent", "7")
