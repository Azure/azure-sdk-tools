"""Unit tests for ChatService memory scope resolution."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest

from models.chat import ChatRequest, Message as ChatMessage
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
    """Warm stateless sessions are reused only by their agent and version."""
    service = ChatService(openai_client=MagicMock())

    service._set_stateless_session_id("azure-sdk-agent", "1", "sdk-session")
    service._set_stateless_session_id("azure-sdk-agent", "2", "sdk-session-v2")
    service._set_stateless_session_id("azure-mcp-agent", "1", "mcp-session")

    assert service._get_stateless_session_id("azure-sdk-agent", "1") == "sdk-session"
    assert service._get_stateless_session_id("azure-sdk-agent", "2") == "sdk-session-v2"
    assert service._get_stateless_session_id("azure-mcp-agent", "1") == "mcp-session"
    service._set_stateless_session_id("azure-sdk-agent", "1", None)
    assert service._get_stateless_session_id("azure-sdk-agent", "1") is None
    assert service._get_stateless_session_id("azure-sdk-agent", "2") == "sdk-session-v2"


def test_request_version_selection(monkeypatch):
    from config.app_config import AGENT_DEPLOYMENTS_CONFIG_KEY

    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "enable")
    values = {}
    service = ChatService(settings=lambda key, default=None: values.get(key, default))
    for expected in ["42", "43"]:
        values[AGENT_DEPLOYMENTS_CONFIG_KEY] = json.dumps({"feature/foo": {"agent": expected}})
        assert service._resolve_request_version(
            "<p>[branch:feature/foo] question</p>", "agent"
        ) == expected
    assert service._resolve_request_version("question", "agent") is None
    assert service._resolve_request_version(
        "[agent:v42] question", "agent"
    ) is None
    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "disable")
    assert service._resolve_request_version("question", "agent") is None
    from services.chat_service import AgentRoutingError
    with pytest.raises(AgentRoutingError):
        service._resolve_request_version("[branch:feature/foo] question", "agent")


@pytest.mark.parametrize("content", [
    "[branch:feature/foo]", "[branch:] q", "[branch:feature/foo] [branch:feature/bar] q",
    "title: [branch:feature/foo]", "title: [branch:] q",
    "title: [branch:feature/foo] [branch:feature/bar] q",
])
def test_invalid_version_prefix(monkeypatch, content):
    from services.chat_service import AgentRoutingError
    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "enable")
    service = ChatService(settings=lambda key, default=None: default)
    with pytest.raises(AgentRoutingError):
        service._resolve_request_version(content, "agent")


@pytest.mark.parametrize("trailing", ["", '"'])
def test_branch_tag_in_channel_title(monkeypatch, trailing):
    from config.app_config import AGENT_DEPLOYMENTS_CONFIG_KEY
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "enable")
    branch = "jiaqizhang-dev-api-stewardship-chatbot"
    values = {AGENT_DEPLOYMENTS_CONFIG_KEY: json.dumps({branch: {"agent": "42"}})}
    service = ChatService(settings=MagicMock(side_effect=values.get))
    message = f"title: [branch:{branch}] test message\n\n<p>test</p>{trailing}"
    assert service._resolve_request_version(message, "agent") == "42"
    untagged = "title: test message\n\n<p>test</p>"
    assert service._resolve_request_version(untagged, "agent") is None
    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "disable")
    with pytest.raises(AgentRoutingError) as exc:
        service._resolve_request_version(message, "agent")
    assert exc.value.status_code == 403


def test_missing_deployment_fails(monkeypatch):
    from services.chat_service import AgentRoutingError
    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "enable")
    service = ChatService(settings=lambda key, default=None: default)
    with pytest.raises(AgentRoutingError) as exc:
        service._resolve_request_version("[branch:missing] q", "agent")
    assert exc.value.status_code == 404


def test_registry_selects_matching_branch_and_agent(monkeypatch):
    from config.app_config import AGENT_DEPLOYMENTS_CONFIG_KEY
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "enable")
    values = {AGENT_DEPLOYMENTS_CONFIG_KEY: json.dumps({
        "feature/Case/path": {"chat-agent": "42", "mcp-agent": "17"},
        "other-branch": {"chat-agent": "43"},
    })}
    service = ChatService(settings=MagicMock(side_effect=values.get))
    assert service._resolve_request_version(
        "[branch:feature/Case/path] question", "chat-agent"
    ) == "42"
    assert service._resolve_request_version(
        "[branch:feature/Case/path] question", "mcp-agent"
    ) == "17"
    assert service._resolve_request_version(
        "[branch:other-branch] question", "chat-agent"
    ) == "43"
    for branch, agent in [("missing", "chat-agent"), ("other-branch", "mcp-agent")]:
        with pytest.raises(AgentRoutingError) as exc:
            service._resolve_request_version(f"[branch:{branch}] question", agent)
        assert exc.value.status_code == 404


def test_invalid_registry_does_not_fall_back_to_default(monkeypatch):
    from config.app_config import AGENT_DEPLOYMENTS_CONFIG_KEY

    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "enable")
    values = {AGENT_DEPLOYMENTS_CONFIG_KEY: "not json"}
    service = ChatService(settings=MagicMock(side_effect=values.get))
    with pytest.raises(ValueError):
        service._resolve_request_version("[branch:feature/foo] question", "agent")
    assert service._resolve_request_version("question", "agent") is None


@pytest.mark.parametrize("routing", ["enable", "disable", "invalid"])
def test_routing_reads_only_environment(monkeypatch, routing):
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", routing)
    settings = MagicMock(side_effect=AssertionError("Routing must not read App Configuration"))
    service = ChatService(settings=settings)
    if routing == "enable":
        assert service._resolve_request_version("q", "agent") is None
    elif routing == "disable":
        assert service._resolve_request_version("q", "agent") is None
        with pytest.raises(AgentRoutingError) as exc:
            service._resolve_request_version("[branch:feature/foo] q", "agent")
        assert exc.value.status_code == 403
    else:
        with pytest.raises(RuntimeError, match="AI_FOUNDRY_AGENT_ROUTING must be enable or disable"):
            service._resolve_request_version("q", "agent")
    settings.assert_not_called()


def test_routing_defaults_to_disabled(monkeypatch):
    from services.chat_service import AgentRoutingError

    monkeypatch.delenv("AI_FOUNDRY_AGENT_ROUTING", raising=False)
    settings = MagicMock(return_value="enable")
    service = ChatService(settings=settings)
    assert service._resolve_request_version("q", "agent") is None
    with pytest.raises(AgentRoutingError) as exc:
        service._resolve_request_version("[branch:feature/foo] q", "agent")
    assert exc.value.status_code == 403
    settings.assert_not_called()


@pytest.mark.asyncio
async def test_routing_http_message_and_errors(monkeypatch):
    import httpx
    import server
    from models.chat import ChatResponse
    from services.chat_service import AgentRoutingError

    chat = AsyncMock(return_value=ChatResponse(id="response", answer="ok", has_result=True))
    monkeypatch.setattr(server._chat_service, "chat", chat)
    refresh = AsyncMock()
    monkeypatch.setattr(server.app_config, "refresh", refresh)
    payload = {"tenant_id": "general_qa_bot", "message": {
        "role": "user", "content": "[branch:feature/foo] question",
    }}
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server.app), base_url="http://test") as client:
        for path in ("/completion", "/agent/chat"):
            response = await client.post(path, json=payload, headers={"X-Agent-Version": "42"})
            assert response.status_code == 200
            assert chat.call_args.kwargs == {}
            assert chat.call_args.args[0].message.content == "[branch:feature/foo] question"
        chat.side_effect = AgentRoutingError("missing deployment", 404)
        response = await client.post("/completion", json=payload)
        assert response.status_code == 404
        assert refresh.await_count == 3


@pytest.mark.asyncio
@pytest.mark.parametrize("version", ["0", "invalid", ""])
async def test_version_values_reach_foundry_and_errors_propagate(monkeypatch, version):
    from azure.core.exceptions import ResourceNotFoundError
    from config.app_config import AGENT_DEPLOYMENTS_CONFIG_KEY

    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "enable")
    values = {AGENT_DEPLOYMENTS_CONFIG_KEY: json.dumps({"feature/foo": {"agent": version}})}
    service = ChatService(settings=MagicMock(side_effect=values.get))
    assert service._resolve_request_version(
        "[branch:feature/foo] q", "agent"
    ) == version
    project = MagicMock()
    failure = ResourceNotFoundError("Foundry rejected version")
    project.agents.get_version = AsyncMock(side_effect=failure)
    with pytest.raises(ResourceNotFoundError) as exc:
        await service._get_agent(project, "agent", version)
    assert exc.value is failure
    project.agents.get_version.assert_awaited_once_with("agent", version)
    project.agents.get.assert_not_called()


@pytest.mark.asyncio
@pytest.mark.parametrize("branch_version,configured_version,expected", [
    ("42", "17", "42"),
    ("", "17", ""),
    (None, "17", "17"),
    (None, "", None),
    (None, None, None),
])
async def test_chat_selects_request_version_before_configured_version(
    monkeypatch, branch_version, configured_version, expected,
):
    from azure.core.exceptions import ResourceNotFoundError
    from config.app_config import AGENT_DEPLOYMENTS_CONFIG_KEY

    monkeypatch.setenv("AI_FOUNDRY_AGENT_ROUTING", "enable")
    settings = {"AI_FOUNDRY_AGENT_NAME": "agent", "AI_FOUNDRY_AGENT_VERSION": configured_version}
    content = "question"
    if branch_version is not None:
        settings[AGENT_DEPLOYMENTS_CONFIG_KEY] = json.dumps({"feature/foo": {"agent": branch_version}})
        content = "[branch:feature/foo] question"
    project = MagicMock()
    failure = ResourceNotFoundError("Foundry unavailable")
    project.agents.get_version = AsyncMock(side_effect=failure)
    project.agents.get = AsyncMock(side_effect=failure)
    service = ChatService(project_client=project, settings=settings.get)
    request = ChatRequest(
        tenant_id="general_qa_bot", message=ChatMessage(role="user", content=content),
    )
    with pytest.raises(ResourceNotFoundError) as exc:
        await service.chat(request)
    assert exc.value is failure
    if expected is None:
        project.agents.get.assert_awaited_once_with("agent")
        project.agents.get_version.assert_not_called()
    else:
        project.agents.get_version.assert_awaited_once_with("agent", expected)
        project.agents.get.assert_not_called()
