"""Unit tests for ChatService memory scope resolution."""

from __future__ import annotations

import asyncio
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest
from azure.ai.projects.models import AgentVersionDetails
from azure.core.async_paging import AsyncItemPaged

from models.chat import ChatRequest, Message as ChatMessage
from services.chat_service import ChatService


@pytest.mark.parametrize("conversation_type, include_tag, last_lookup_fails", [
    ("teams_channel", True, False), ("teams_channel", False, False),
    (None, True, False), ("teams_channel", True, True),
])
@pytest.mark.parametrize("output_type", ["function_call_output", "message"])
def test_mentions_come_from_verified_tool_results_and_explicit_markup_only(
    conversation_type, include_tag, last_lookup_fails, output_type
):
    import json

    from openai.types.responses import (
        Response, ResponseFunctionToolCall, ResponseFunctionToolCallOutputItem,
        ResponseOutputMessage, ResponseOutputText,
    )

    from models.identity import ResolveGitHubUsersResult, TeamsMention, TeamsUser

    mention = TeamsMention(
        text="<at>linked.person@microsoft.com</at>",
        mentioned=TeamsUser(
            name="Linked Person",
            id="11111111-2222-3333-4444-555555555555",
        ),
    )
    output = []
    # Separate tool calls must not discard the previously resolved reviewer.
    lookups = [
        ResolveGitHubUsersResult(mentions={"linked-user": mention}),
        ResolveGitHubUsersResult(errors={"unlinked": "No linked Microsoft account was found."}),
        ResolveGitHubUsersResult(errors={"service": "Not a person."}),
    ]
    if last_lookup_fails:
        lookups.append(ResolveGitHubUsersResult(errors={"linked-user": "HTTP 403"}))
    for index, result in enumerate(lookups):
        call_id = f"lookup-{index}"
        tool_output: ResponseFunctionToolCallOutputItem | ResponseOutputMessage
        if output_type == "function_call_output":
            tool_output = ResponseFunctionToolCallOutputItem.model_construct(
                type=output_type, id=call_id, call_id=call_id,
                output=result.model_dump_json(), status="completed",
            )
        else:
            tool_output = ResponseOutputMessage.model_construct(
                type="message", id=call_id, role="assistant", status="completed", content=[],
                call_id=call_id,
                output=json.dumps([result.model_dump_json()]),
            )
        output.extend([
            ResponseFunctionToolCall.model_construct(
                type="function_call",
                name="resolve_github_users",
                call_id=call_id,
                arguments=json.dumps({"usernames": [*result.mentions, *result.errors]}),
            ),
            tool_output,
        ])
    answer = f"Ping {mention.text if include_tag else '@linked-user'} or <at>unverified@microsoft.com</at>."
    output.append(ResponseOutputMessage.model_construct(
        type="message", id="answer", role="assistant", status="completed",
        content=[ResponseOutputText.model_construct(
            type="output_text", text=answer, annotations=[],
        )],
    ))
    response = Response.model_construct(id="reply", output=output)
    request = ChatRequest(
        tenant_id="azure_sdk_qa_bot",
        conversation_type=conversation_type,
        message=ChatMessage(role="user", content="Who can review?"),
    )

    result = ChatService()._postprocess(request, response, None)

    assert result.answer == answer
    assert result.mentions == (
        [mention] if conversation_type and include_tag and not last_lookup_fails else None
    )
    if result.mentions:
        assert result.model_dump(mode="json")["mentions"] == [{
            "text": mention.text,
            "mentioned": {"id": mention.mentioned.id, "name": "Linked Person"},
        }]


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


def _version(version, branch="feature/foo", status="active"):
    return AgentVersionDetails({
        "version": version, "metadata": {"branchname": branch}, "status": status,
    })


def _project_with_versions(*pages):
    project = MagicMock()

    async def get_next(token):
        await asyncio.sleep(0)
        return int(token or 0)

    async def extract_data(index):
        async def items():
            for version in pages[index]:
                yield version
        return (str(index + 1) if index + 1 < len(pages) else None), items()

    project.agents.list_versions.side_effect = lambda *args, **kwargs: AsyncItemPaged(
        get_next, extract_data,
    )
    return project


@pytest.mark.asyncio
async def test_branch_lookup_logs_selection_without_message_content(monkeypatch, caplog):
    import logging

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version("43", status="creating"), _version("42")])
    service = ChatService(project_client=project)
    with caplog.at_level(logging.INFO, logger="utils.azure_ai_foundry"):
        assert await service._resolve_request_version(
            "[branch:feature/foo] private question", "agent"
        ) == "42"
    assert "Looking up Foundry agent=agent branch=feature/foo" in caplog.text
    assert "Skipping inactive agent=agent version=43 branch=feature/foo status=creating" in caplog.text
    assert "Selected Foundry agent=agent version=42 branch=feature/foo status=active" in caplog.text
    assert "private question" not in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("content", [
    "[branch:feature/foo]question",
    "[branch:feature/foo]问题",
    "title: [branch:feature/foo]question",
    "<p>[branch:feature/foo]</p><p>question</p>",
    "[branch:feature/foo]<br />question",
    "<div>[branch:feature/foo]</div><div>question</div>",
    "<p><span>[branch:feature/</span><b>foo]</b></p><p>question</p>",
])
async def test_branch_routing_accepts_content_without_whitespace(monkeypatch, content):
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version("42")])
    service = ChatService(project_client=project)
    assert await service._resolve_request_version(content, "agent") == "42"
    project.agents.list_versions.assert_called_once_with("agent", order="desc")


@pytest.mark.asyncio
async def test_request_version_selection(monkeypatch):
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version("42")])
    settings = MagicMock(side_effect=AssertionError("Branch routing must not read App Configuration"))
    service = ChatService(project_client=project, settings=settings)
    assert await service._resolve_request_version(
        "<p>[branch:feature/foo] question</p>", "agent"
    ) == "42"
    assert await service._resolve_request_version("question", "agent") is None
    assert await service._resolve_request_version(
        "[agent:v42] question", "agent"
    ) is None
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "false")
    assert await service._resolve_request_version("question", "agent") is None
    from services.chat_service import AgentRoutingError
    with pytest.raises(AgentRoutingError):
        await service._resolve_request_version("[branch:feature/foo] question", "agent")
    project.agents.list_versions.assert_called_once_with("agent", order="desc")
    settings.assert_not_called()


@pytest.mark.asyncio
@pytest.mark.parametrize("content", [
    "[branch:feature/foo]", "[branch:] q", "[branch:feature/foo] [branch:feature/bar] q",
    "[branch:feature/foo][branch:feature/bar]q",
    "title: [branch:feature/foo]", "title: [branch:] q",
    "title: [branch:feature/foo] [branch:feature/bar] q",
])
async def test_invalid_version_prefix(monkeypatch, content):
    from services.chat_service import AgentRoutingError
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    service = ChatService(settings=lambda key, default=None: default)
    with pytest.raises(AgentRoutingError):
        await service._resolve_request_version(content, "agent")


@pytest.mark.asyncio
@pytest.mark.parametrize("trailing", ["", '"'])
async def test_branch_tag_in_channel_title(monkeypatch, trailing):
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    branch = "jiaqizhang-dev-api-stewardship-chatbot"
    project = _project_with_versions([_version("42", branch)])
    service = ChatService(project_client=project)
    message = f"title: [branch:{branch}] test message\n\n<p>test</p>{trailing}"
    assert await service._resolve_request_version(message, "agent") == "42"
    untagged = "title: test message\n\n<p>test</p>"
    assert await service._resolve_request_version(untagged, "agent") is None
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "false")
    with pytest.raises(AgentRoutingError) as exc:
        await service._resolve_request_version(message, "agent")
    assert exc.value.status_code == 403


@pytest.mark.asyncio
async def test_missing_deployment_fails(monkeypatch):
    from services.chat_service import AgentRoutingError
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version("42")])
    service = ChatService(project_client=project)
    with pytest.raises(AgentRoutingError) as exc:
        await service._resolve_request_version("[branch:missing] q", "agent")
    assert exc.value.status_code == 404


@pytest.mark.asyncio
async def test_metadata_selects_matching_branch_and_agent_across_pages(monkeypatch):
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    chat_project = _project_with_versions(
        [_version("43", "other-branch")], [_version("42", "feature/Case/path")],
    )
    mcp_project = _project_with_versions([_version("17", "feature/Case/path")])
    project = MagicMock()
    project.agents.list_versions.side_effect = lambda agent, **kwargs: (
        chat_project if agent == "chat-agent" else mcp_project
    ).agents.list_versions(agent, **kwargs)
    service = ChatService(project_client=project)
    assert await service._resolve_request_version(
        "[branch:feature/Case/path] question", "chat-agent"
    ) == "42"
    assert await service._resolve_request_version(
        "[branch:feature/Case/path] question", "mcp-agent"
    ) == "17"
    assert await service._resolve_request_version(
        "[branch:other-branch] question", "chat-agent"
    ) == "43"
    for branch, agent in [("missing", "chat-agent"), ("other-branch", "mcp-agent")]:
        with pytest.raises(AgentRoutingError) as exc:
            await service._resolve_request_version(f"[branch:{branch}] question", agent)
        assert exc.value.status_code == 404


@pytest.mark.asyncio
@pytest.mark.parametrize("status", [
    "creating", "failed", "deleting", "deleted", "provisioning", "unexpected", None,
])
async def test_newest_unusable_version_does_not_hide_active_version(monkeypatch, caplog, status):
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version("43", status=status), _version("42"), _version("41")])
    project.agents.get_version = AsyncMock(return_value=_version("43", status=status))
    service = ChatService(project_client=project)
    assert await service._resolve_request_version("[branch:feature/foo] q", "agent") == "42"
    if status is None:
        project.agents.get_version.assert_awaited_once_with("agent", "43")
        assert "did not return readiness status" in caplog.text
    else:
        project.agents.get_version.assert_not_awaited()


@pytest.mark.asyncio
async def test_missing_list_status_uses_version_details(monkeypatch):
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version("43", status=None), _version("42")])
    project.agents.get_version = AsyncMock(return_value=_version("43"))
    service = ChatService(project_client=project)
    assert await service._resolve_request_version("[branch:feature/foo] q", "agent") == "43"
    project.agents.get_version.assert_awaited_once_with("agent", "43")


@pytest.mark.asyncio
async def test_deleted_candidate_does_not_hide_older_active_version(monkeypatch):
    from azure.core.exceptions import ResourceNotFoundError

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version("43", status=None), _version("42")])
    project.agents.get_version = AsyncMock(side_effect=ResourceNotFoundError("deleted"))
    service = ChatService(project_client=project)
    assert await service._resolve_request_version("[branch:feature/foo] q", "agent") == "42"


@pytest.mark.asyncio
async def test_untagged_versions_are_not_selected(monkeypatch):
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([AgentVersionDetails({
        "version": "43", "metadata": {}, "status": "active",
    })])
    service = ChatService(project_client=project)
    with pytest.raises(AgentRoutingError) as exc:
        await service._resolve_request_version("[branch:feature/foo] q", "agent")
    assert exc.value.status_code == 404
    project.agents.get_version.assert_not_called()


@pytest.mark.asyncio
@pytest.mark.parametrize("status", ["creating", "failed", None])
async def test_no_active_version_returns_404(monkeypatch, status):
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version("43", status=status)])
    project.agents.get_version = AsyncMock(return_value=_version("43", status=status))
    service = ChatService(project_client=project)
    with pytest.raises(AgentRoutingError) as exc:
        await service._resolve_request_version("[branch:feature/foo] q", "agent")
    assert exc.value.status_code == 404


@pytest.mark.asyncio
@pytest.mark.parametrize("present", [True, False])
async def test_branch_lookup_reads_foundry_for_each_request(monkeypatch, present):
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    versions = [_version("42")] if present else []
    project = _project_with_versions(versions)
    service = ChatService(project_client=project)
    results = await asyncio.gather(*(
        service._resolve_request_version("[branch:feature/foo] q", "agent") for _ in range(5)
    ), return_exceptions=True)
    if present:
        assert results == ["42"] * 5
    else:
        assert all(isinstance(result, AgentRoutingError) and result.status_code == 404 for result in results)
    assert project.agents.list_versions.call_count == 5
    project.agents.list_versions.assert_called_with("agent", order="desc")
    versions.insert(0, _version("43"))
    assert await service._resolve_request_version("[branch:feature/foo] q", "agent") == "43"
    assert project.agents.list_versions.call_count == 6


@pytest.mark.asyncio
@pytest.mark.parametrize("missing_agent", [True, False])
async def test_foundry_errors_are_not_cached(monkeypatch, missing_agent):
    from azure.core.exceptions import ResourceNotFoundError, ServiceRequestError
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = MagicMock()
    project.agents.list_versions.side_effect = (
        ResourceNotFoundError("missing") if missing_agent else ServiceRequestError("unavailable")
    )
    service = ChatService(project_client=project)
    for _ in range(2):
        with pytest.raises(AgentRoutingError if missing_agent else ServiceRequestError):
            await service._resolve_request_version("[branch:feature/foo] q", "agent")
    assert project.agents.list_versions.call_count == 2


@pytest.mark.asyncio
@pytest.mark.parametrize("routing", ["true", "false", "invalid"])
async def test_routing_reads_only_environment(monkeypatch, routing):
    from services.chat_service import AgentRoutingError

    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", routing)
    settings = MagicMock(side_effect=AssertionError("Routing must not read App Configuration"))
    service = ChatService(settings=settings)
    if routing == "true":
        assert await service._resolve_request_version("q", "agent") is None
    elif routing == "false":
        assert await service._resolve_request_version("q", "agent") is None
        with pytest.raises(AgentRoutingError) as exc:
            await service._resolve_request_version("[branch:feature/foo] q", "agent")
        assert exc.value.status_code == 403
    else:
        with pytest.raises(RuntimeError, match="ENABLE_AI_FOUNDRY_AGENT_ROUTING must be true or false"):
            await service._resolve_request_version("q", "agent")
    settings.assert_not_called()


@pytest.mark.asyncio
async def test_routing_defaults_to_disabled(monkeypatch):
    from services.chat_service import AgentRoutingError

    monkeypatch.delenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", raising=False)
    settings = MagicMock(return_value="true")
    service = ChatService(settings=settings)
    assert await service._resolve_request_version("q", "agent") is None
    with pytest.raises(AgentRoutingError) as exc:
        await service._resolve_request_version("[branch:feature/foo] q", "agent")
    assert exc.value.status_code == 403
    settings.assert_not_called()


@pytest.mark.asyncio
async def test_routing_http_message_and_errors(monkeypatch):
    import httpx
    import server
    from models.chat import ChatResponse
    from services.chat_service import AgentRoutingError

    chat = AsyncMock(return_value=ChatResponse(
        id="response", answer="ok", has_result=True, agent_name="returned-agent",
    ))
    monkeypatch.setattr(server._chat_service, "chat", chat)
    payload = {"tenant_id": "general_qa_bot", "message": {
        "role": "user", "content": "[branch:feature/foo] question",
    }}
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server.app), base_url="http://test") as client:
        for path in ("/completion", "/agent/chat"):
            response = await client.post(path, json=payload, headers={"X-Agent-Version": "42"})
            assert response.status_code == 200
            assert response.json()["agent_name"] == "returned-agent"
            assert chat.call_args.kwargs == {}
            assert chat.call_args.args[0].message.content == "[branch:feature/foo] question"
        for status in (403, 404):
            chat.side_effect = AgentRoutingError("branch routing failed", status)
            response = await client.post("/completion", json=payload)
            assert response.status_code == status


@pytest.mark.asyncio
@pytest.mark.parametrize("version", ["0", "invalid", ""])
async def test_version_values_reach_foundry_and_errors_propagate(monkeypatch, version):
    from azure.core.exceptions import ResourceNotFoundError
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    project = _project_with_versions([_version(version)])
    service = ChatService(project_client=project)
    assert await service._resolve_request_version(
        "[branch:feature/foo] q", "agent"
    ) == version
    failure = ResourceNotFoundError("Foundry rejected version")
    project.agents.get_version = AsyncMock(side_effect=failure)
    with pytest.raises(ResourceNotFoundError) as exc:
        await service._get_agent(project, "agent", version)
    assert exc.value is failure
    project.agents.get_version.assert_awaited_once_with("agent", version)
    project.agents.get.assert_not_called()


@pytest.mark.asyncio
@pytest.mark.parametrize("tenant_id", ["general_qa_bot", "azure_mcp_server"])
async def test_chat_returns_the_invoked_agent_name(monkeypatch, tenant_id):
    import services.chat_service as chat_service
    from utils.background_tasks import BackgroundTaskTracker

    project = MagicMock()
    agent = SimpleNamespace(name="actual-deployment", version="42")
    service = ChatService(
        project_client=project, openai_client=MagicMock(),
        settings=lambda key, default=None: default,
    )
    service._get_agent = AsyncMock(return_value=agent)
    service._resolve_request_version = AsyncMock(return_value="42")
    service._save_bot_answer_to_conversation = AsyncMock()
    response = SimpleNamespace(
        id="response-1", status="completed", output=[], output_text="answer",
    )
    hosted = MagicMock()
    hosted.invoke = AsyncMock(return_value=("trace-1", response))
    monkeypatch.setattr(chat_service, "HostedAgentClient", MagicMock(return_value=hosted))

    result = await service.chat(ChatRequest(
        tenant_id=tenant_id, message=ChatMessage(role="user", content="question"),
    ))
    await asyncio.sleep(0)
    await BackgroundTaskTracker.instance().shutdown()

    assert result.agent_name == "actual-deployment"
    assert result.model_dump(mode="json")["agent_name"] == "actual-deployment"
    assert hosted.invoke.call_args.kwargs["agent_ref"]["name"] == result.agent_name


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
    monkeypatch.setenv("ENABLE_AI_FOUNDRY_AGENT_ROUTING", "true")
    settings = {"AI_FOUNDRY_AGENT_NAME": "agent", "AI_FOUNDRY_AGENT_VERSION": configured_version}
    content = "question"
    if branch_version is not None:
        content = "[branch:feature/foo] question"
    project = _project_with_versions([_version(branch_version)])
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
