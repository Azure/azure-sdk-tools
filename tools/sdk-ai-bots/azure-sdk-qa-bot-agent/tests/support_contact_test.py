"""Support-catalog guidance with ordinary answers and verified Teams mentions."""

from __future__ import annotations

import asyncio
import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock, patch

import httpx
import pytest
import yaml
from openai.types.responses import (
    ResponseFunctionToolCall,
    ResponseFunctionToolCallOutputItem,
    ResponseOutputMessage,
)

from models import support_contact
from models.chat import ChatRequest, ChatResponse
from models.identity import ResolvedGitHubUser, ResolveGitHubUsersResult, TeamsMention, TeamsUser
from models.support_contact import get_support_catalog_context, parse_support_catalog
from services.chat_service import ChatService
from tools.identity_tools import IdentityTools
from utils.azure_ai_foundry_agent import ConversationBrokenError

ROOT = Path(__file__).resolve().parents[1]
CONFIG = b"""
support_areas:
  - key: sdk_python
    description: Python SDK breaking-change review.
    github_usernames: [reviewer-a]
  - key: sdk_java
    description: Java SDK breaking-change review.
    github_usernames: [reviewer-a]
  - key: sdk_dotnet
    description: .NET SDK breaking-change review.
    github_usernames: [reviewer-b, reviewer-d]
"""
ANSWER = (
    "**Approvals needed**\n\n"
    "@reviewer-a Please review Python and Java.\n\n"
    "@reviewer-b Please review .NET.\n\n"
    "PR: https://example.test/pr/123"
)


@pytest.fixture(autouse=True)
def clear_support_catalog_cache():
    get_support_catalog_context.cache_clear()
    yield
    get_support_catalog_context.cache_clear()


@pytest.fixture
def support_config_path(monkeypatch, tmp_path):
    path = tmp_path / "support-contact.yaml"
    monkeypatch.setattr(support_contact, "SUPPORT_CONTACT_PATH", path)
    return path


def request(**changes):
    return ChatRequest(
        tenant_id="api_spec_review_bot",
        message={"role": "user", "content": "Question"},
        **changes,
    )


def response(answer):
    return SimpleNamespace(
        id="response-1", status="completed", output=[], output_text=answer,
    )


def identity_response(answer, lookups, output_type):
    result = response(answer)
    for index, lookup in enumerate(lookups):
        call_id = f"identity-{index}"
        result.output.append(ResponseFunctionToolCall.model_construct(
            type="function_call", name="resolve_github_users", call_id=call_id,
            arguments=json.dumps({
                "usernames": sorted(lookup.users.keys() | lookup.errors.keys()),
            }),
        ))
        if output_type == "function_call_output":
            result.output.append(ResponseFunctionToolCallOutputItem.model_construct(
                type=output_type, id=call_id, call_id=call_id, status="completed",
                output=lookup.model_dump_json(),
            ))
        else:
            result.output.append(ResponseOutputMessage.model_construct(
                type="message", id=call_id, role="assistant", status="completed", content=[],
                call_id=call_id, output=json.dumps([lookup.model_dump_json()]),
            ))
    return result


def test_catalog_projection_includes_only_areas_and_github_usernames():
    raw = yaml.safe_load(CONFIG)
    catalog = parse_support_catalog(yaml.safe_dump(raw).encode())
    context = json.loads(catalog.model_context().split("\n", 1)[1])
    assert context == raw
    assert "teams_user_id" not in catalog.model_context()
    assert "contacts" not in catalog.model_dump()
    assert catalog.support_areas[-1].github_usernames == ["reviewer-b", "reviewer-d"]


@pytest.mark.parametrize("data", [
    b"", b"[]", b"{}",
    b"support_areas: []\nsupport_areas: []",
    b"support_areas: [{key: area, key: duplicate, description: Test, github_usernames: [user]}]",
    b"support_areas: [{key: bad-key, description: Test, github_usernames: [user]}]",
    b"support_areas: [{key: area, description: ' ', github_usernames: [user]}]",
    b"support_areas: [{key: area, description: Test, github_usernames: ['org/team']}]",
    b"support_areas: [{key: area, description: Test, github_usernames: [user_name]}]",
    b"support_areas: [{key: area, description: Test, github_usernames: null}]",
    b"support_areas: [{key: area, description: Test, github_usernames: []}]",
    b"support_areas: [{key: area, description: Test, github_usernames: user}]",
    b"support_areas: [{key: area, description: Test, github_usernames: [user, null]}]",
    b"support_areas: [{key: area, description: Test, github_usernames: [user, user_name]}]",
    b"support_areas: [{key: area, description: Test, github_usernames: [user, user]}]",
    b"support_areas: [{key: area, description: Test, github_usernames: [User, ' @USER ']}]",
    b"support_areas: [{key: area, description: Test, github_username: user}]",
    b"support_areas: [{key: area, description: Test}]",
    b"support_areas: [{key: area, description: Test, github_usernames: [user]}, {key: area, description: Other, github_usernames: [user]}]",
    b"support_areas: []\ncontacts: {}",
    b"!!python/object/apply:os.system ['echo unsafe']",
])
def test_invalid_catalog_fails(data):
    with pytest.raises((ValueError, yaml.YAMLError)):
        parse_support_catalog(data)


@pytest.mark.parametrize("answer", [
    "Hello!",
    "The requested change is already approved.",
    "Please share the PR link and failing check.",
    '{"answer": "Literal JSON requested by the user"}',
    ANSWER,
])
def test_normal_answers_are_not_wrapped_classified_or_duplicated(answer):
    result = ChatService()._postprocess(request(), response(answer), None)
    assert result.answer == answer
    assert "next_step" not in result.model_dump()
    assert "next_step" not in ChatResponse.model_json_schema()["properties"]
    assert result.mentions is None


@pytest.mark.parametrize("answer", [
    "For Python SDK questions, the configured contact is reviewer-a.",
    "For Python SDK questions, contact Reviewer (reviewer@example.test).",
    "When you are ready for SDK review, you can contact @reviewer-a.",
    "Please share the breaking-change report so I can identify the issue.",
    "Thanks for confirming; no further action is needed.",
])
@pytest.mark.parametrize("output_type", ["function_call_output", "message"])
def test_informational_replies_do_not_attach_unused_verified_identities(answer, output_type):
    mention = TeamsMention(
        text="<at>reviewer@example.test</at>",
        mentioned=TeamsUser(id="verified-a", name="Reviewer"),
    )
    result = ChatService()._postprocess(
        request(conversation_type="teams_channel"),
        identity_response(
            answer, [ResolveGitHubUsersResult(
                users={"reviewer-a": ResolvedGitHubUser(
                    text=mention.text, mentioned=mention.mentioned,
                    plain_text="Reviewer (reviewer@example.test)",
                )},
            )], output_type,
        ),
        None,
    )
    assert result.answer == answer
    assert result.mentions is None


@pytest.mark.parametrize("output_type", ["function_call_output", "message"])
def test_contact_details_merge_across_lookups_and_invalidate_stale_results(output_type):
    user = ResolvedGitHubUser(
        text="<at>first@example.test</at>",
        mentioned=TeamsUser(id="verified-a", name="First reviewer"),
        plain_text="First reviewer (first@example.test)",
    )
    other = ResolvedGitHubUser(
        text="<at>second@example.test</at>",
        mentioned=TeamsUser(id="verified-b", name="Second reviewer"),
        plain_text="Second reviewer (second@example.test)",
    )
    lookups = [
        ResolveGitHubUsersResult(errors={"reviewer-a": "HTTP 403"}),
        ResolveGitHubUsersResult(users={"reviewer-a": user}),
        ResolveGitHubUsersResult(users={"reviewer-b": other}),
    ]
    service = ChatService()
    initial = service._extract_tool_results(identity_response("", lookups, output_type).output)["resolve_github_users"]
    assert isinstance(initial, ResolveGitHubUsersResult)
    assert initial.users == {"reviewer-a": user, "reviewer-b": other}
    assert initial.errors == {}

    updated = ResolvedGitHubUser(
        text="<at>updated@example.test</at>",
        mentioned=TeamsUser(id="updated-a", name="Updated reviewer"),
        plain_text="Updated reviewer (updated@example.test)",
    )
    lookups.extend([
        ResolveGitHubUsersResult(errors={"reviewer-b": "HTTP 403"}),
        ResolveGitHubUsersResult(users={"reviewer-a": updated}),
    ])
    final = service._extract_tool_results(identity_response("", lookups, output_type).output)["resolve_github_users"]
    assert isinstance(final, ResolveGitHubUsersResult)
    assert final.users == {"reviewer-a": updated}
    assert final.errors == {"reviewer-b": "HTTP 403"}
    stale_answer = service._postprocess(
        request(conversation_type="teams_channel"),
        identity_response(f"Please help, {user.text}.", lookups, output_type), None,
    )
    assert stale_answer.mentions is None


@pytest.mark.parametrize("output_type", ["function_call_output", "message"])
def test_direct_help_request_keeps_natural_prose_and_verified_mention(output_type):
    mention = TeamsMention(
        text="<at>reviewer@example.test</at>",
        mentioned=TeamsUser(id="verified-a", name="Reviewer"),
    )
    answer = (
        f"The compatibility question still needs a decision. {mention.text}, "
        "could you help assess the Python change and clarify the evidence needed "
        "for Java in https://example.test/pr/123? This is not a request for final approval."
    )
    result = ChatService()._postprocess(
        request(conversation_type="teams_channel"),
        identity_response(
            answer, [ResolveGitHubUsersResult(users={"reviewer-a": ResolvedGitHubUser(
                text=mention.text, mentioned=mention.mentioned,
                plain_text="Reviewer (reviewer@example.test)",
            )})], output_type,
        ),
        None,
    )
    assert result.answer == answer
    assert result.mentions == [mention]
    assert result.answer.count(mention.text) == 1
    assert result.answer.count("https://example.test/pr/123") == 1


@pytest.mark.parametrize("output_type", ["function_call_output", "message"])
def test_same_area_contacts_support_batched_mentions(output_type):
    area = parse_support_catalog(CONFIG).support_areas[-1]
    users = {
        login: ResolvedGitHubUser(
            text=f"<at>{login}@example.test</at>",
            mentioned=TeamsUser(id=f"verified-{login}", name=login),
            plain_text=f"{login} ({login}@example.test)",
        )
        for login in area.github_usernames
    }
    answer = ", ".join(user.text for user in users.values()) + ", could you help review this .NET PR?"
    result = ChatService()._postprocess(
        request(conversation_type="teams_channel"),
        identity_response(answer, [ResolveGitHubUsersResult(users=users)], output_type),
        None,
    )
    assert result.answer == answer
    assert result.mentions == [
        TeamsMention(text=user.text, mentioned=user.mentioned) for user in users.values()
    ]


@pytest.mark.parametrize("conversation_type", ["teams_channel", None])
@pytest.mark.parametrize("output_type", ["function_call_output", "message"])
@pytest.mark.parametrize("resolution", [
    "both", "partial", "failed", "forged", "later_failure", "unused",
])
def test_grouped_requests_reuse_verified_mentions(conversation_type, output_type, resolution):
    first = TeamsMention(
        text="<at>python-java@example.test</at>",
        mentioned=TeamsUser(id="verified-a", name="First reviewer"),
    )
    second = TeamsMention(
        text="<at>dotnet@example.test</at>",
        mentioned=TeamsUser(id="verified-b", name="Second reviewer"),
    )
    all_mentions = {"reviewer-a": first, "reviewer-b": second}
    all_users = {
        login: ResolvedGitHubUser(
            text=mention.text, mentioned=mention.mentioned, plain_text=mention.mentioned.name,
        )
        for login, mention in all_mentions.items()
    }
    lookups = [ResolveGitHubUsersResult(users=all_users)]
    expected = [first, second]
    answer = ANSWER.replace("@reviewer-a", first.text).replace("@reviewer-b", second.text)
    if resolution in {"partial", "forged"}:
        lookups = [ResolveGitHubUsersResult(
            users={"reviewer-a": all_users["reviewer-a"]},
            errors={"reviewer-b": "Microsoft account lookup failed (HTTP 403)."},
        )]
        expected = [first]
        if resolution == "partial":
            answer = ANSWER.replace("@reviewer-a", first.text)
    elif resolution == "failed":
        lookups = [ResolveGitHubUsersResult(errors={
            login: "No linked Microsoft account was found." for login in all_mentions
        })]
        expected = []
        answer = ANSWER
    elif resolution == "later_failure":
        lookups.append(ResolveGitHubUsersResult(errors={"reviewer-b": "HTTP 403"}))
        expected = [first]
    elif resolution == "unused":
        answer = ANSWER
        expected = []
    result = ChatService()._postprocess(
        request(conversation_type=conversation_type),
        identity_response(answer, lookups, output_type), None,
    )
    assert result.answer == answer
    assert result.mentions == ((expected or None) if conversation_type else None)
    assert result.answer.count("https://example.test/pr/123") == 1
    assert "next_step" not in result.model_dump()


def test_normal_answer_keeps_reference_processing():
    raw = ANSWER + "\n\n**References**\n- [Guide](https://example.test/guide)"
    result = ChatService()._postprocess(request(), response(raw), None)
    assert result.answer == ANSWER
    assert result.references[0].link == "https://example.test/guide"


def test_repository_config_loads_independently_of_working_directory(monkeypatch, tmp_path):
    from models.support_contact import SUPPORT_CONTACT_PATH

    assert SUPPORT_CONTACT_PATH == ROOT / "config" / "support-contact.yaml"
    catalog = parse_support_catalog(SUPPORT_CONTACT_PATH.read_bytes())
    monkeypatch.chdir(tmp_path)
    assert get_support_catalog_context() == catalog.model_context()
    assert not (ROOT / "config" / "support-contact.example.yaml").exists()


def test_repository_arm_contacts_separate_modeling_leases_and_office_hours():
    catalog = parse_support_catalog(support_contact.SUPPORT_CONTACT_PATH.read_bytes())
    areas = {area.key: area for area in catalog.support_areas}
    assert areas["arm_modeling_leases"].github_usernames == ["raosuhas"]
    assert areas["arm_office_hours"].github_usernames == ["vikeshi26"]
    assert "approval belongs to the designated reviewer" in areas["arm_modeling_leases"].description
    assert "Excludes API design decisions, lease approvals and ARM PR sign-off" in areas["arm_office_hours"].description
    assert "Excludes ARM modeling decisions, leases, office-hours coordination" in areas["typespec_arm_modeling"].description
    assert areas["typespec_arm_modeling"].github_usernames == ["markcowl"]


def test_repository_dotnet_contacts_cover_management_and_data_plane():
    catalog = parse_support_catalog(support_contact.SUPPORT_CONTACT_PATH.read_bytes())
    areas = {area.key: area for area in catalog.support_areas if area.key.startswith("dotnet_")}
    assert set(areas) == {"dotnet_management_sdk", "dotnet_dataplane_sdk"}
    assert areas["dotnet_management_sdk"].github_usernames == [
        "arthurma1978", "live1206", "arcturuszhang",
    ]
    assert areas["dotnet_dataplane_sdk"].github_usernames == ["jorgerangel-msft"]
    assert all("m-nash" not in area.github_usernames for area in catalog.support_areas)


def test_repository_go_management_contacts_are_grouped():
    catalog = parse_support_catalog(support_contact.SUPPORT_CONTACT_PATH.read_bytes())
    areas = {area.key: area for area in catalog.support_areas if area.key.startswith("go_management_sdk")}
    assert set(areas) == {"go_management_sdk"}
    assert areas["go_management_sdk"].github_usernames == ["tadelesh", "jiaqizhang-dev"]


def test_repository_javascript_management_contacts_are_grouped():
    catalog = parse_support_catalog(support_contact.SUPPORT_CONTACT_PATH.read_bytes())
    areas = {
        area.key: area for area in catalog.support_areas
        if area.key.startswith("javascript_management_sdk")
    }
    assert set(areas) == {"javascript_management_sdk"}
    area = areas["javascript_management_sdk"]
    assert area.github_usernames == ["jialinhuang803", "marygao"]
    assert "shared-core" not in area.description


def test_empty_catalog_is_cached(support_config_path):
    support_config_path.write_bytes(b"support_areas: []")
    context = get_support_catalog_context()
    assert context == ""
    support_config_path.unlink()
    with patch.object(Path, "read_bytes", side_effect=AssertionError("Empty catalog must be cached.")):
        assert get_support_catalog_context() is context


def test_local_catalog_is_loaded_and_serialized_once_until_cache_is_cleared(
    monkeypatch, support_config_path,
):
    path = support_config_path
    path.write_bytes(CONFIG)
    parse = MagicMock(wraps=parse_support_catalog)
    monkeypatch.setattr(support_contact, "parse_support_catalog", parse)
    with patch.object(
        support_contact.SupportContactCatalog, "model_context", autospec=True,
        side_effect=support_contact.SupportContactCatalog.model_context,
    ) as serialize:
        first = get_support_catalog_context()
        path.write_bytes(CONFIG.replace(b"reviewer-b", b"reviewer-c"))
        assert get_support_catalog_context() is first
        parse.assert_called_once()
        serialize.assert_called_once()
        get_support_catalog_context.cache_clear()
        second = get_support_catalog_context()
        assert parse.call_count == serialize.call_count == 2
    first_data = json.loads(first.split("\n", 1)[1])
    second_data = json.loads(second.split("\n", 1)[1])
    assert first != second
    assert first_data["support_areas"][-1]["github_usernames"] == ["reviewer-b", "reviewer-d"]
    assert second_data["support_areas"][-1]["github_usernames"] == ["reviewer-c", "reviewer-d"]
    path.unlink()
    assert get_support_catalog_context() is second
    second_data["support_areas"].clear()
    assert json.loads(get_support_catalog_context().split("\n", 1)[1])["support_areas"]


@pytest.mark.parametrize("data", [
    None, b"", b"bad yaml", b"support_areas: [", b"\xff", b"support_areas: [{}]",
])
def test_missing_or_invalid_local_config_is_logged_and_cached_as_empty_context(
    support_config_path, data, caplog,
):
    path = support_config_path
    if data is not None:
        path.write_bytes(data)
    assert get_support_catalog_context() == ""
    assert "Support-contact configuration failed" in caplog.text
    path.write_bytes(CONFIG)
    assert get_support_catalog_context() == ""
    get_support_catalog_context.cache_clear()
    assert get_support_catalog_context() == parse_support_catalog(CONFIG).model_context()


def test_permission_failure_is_logged_and_cached_as_empty_context(monkeypatch, caplog):
    read = MagicMock(side_effect=PermissionError("denied"))
    monkeypatch.setattr(Path, "read_bytes", read)
    assert get_support_catalog_context() == ""
    assert get_support_catalog_context() == ""
    read.assert_called_once()
    assert "PermissionError" in caplog.text


@pytest.mark.parametrize("username", ["reviewer-a", "Reviewer-A", "@Reviewer-A", " @Reviewer-A "])
def test_catalog_uses_shared_github_normalization(username):
    raw = yaml.safe_load(CONFIG)
    for area in raw["support_areas"][:2]:
        area["github_usernames"] = [username, " @Reviewer-D "]
    catalog = parse_support_catalog(yaml.safe_dump(raw).encode())
    assert all(
        area.github_usernames == ["reviewer-a", "reviewer-d"]
        for area in catalog.support_areas[:2]
    )


@pytest.mark.asyncio
@pytest.mark.parametrize("configuration", ["valid", "disabled", "invalid"])
@pytest.mark.parametrize("existing,recover,stateless", [
    (False, False, False), (True, False, False),
    (False, True, False), (True, True, False),
    (False, False, True),
])
async def test_catalog_context_matches_tenant_instructions_on_creation_and_recovery(
    monkeypatch, support_config_path, configuration, existing, recover, stateless,
):
    import services.chat_service as module

    path = support_config_path
    path.write_bytes(
        CONFIG if configuration == "valid"
        else b"support_areas: []" if configuration == "disabled"
        else b"invalid"
    )
    cached_context = get_support_catalog_context()
    monkeypatch.setattr(
        support_contact, "parse_support_catalog",
        MagicMock(side_effect=AssertionError("Catalog must not be revalidated during chat.")),
    )
    monkeypatch.setattr(
        support_contact.SupportContactCatalog, "model_context",
        MagicMock(side_effect=AssertionError("Catalog must not be reserialized during chat.")),
    )
    service = ChatService(
        settings=lambda key, default=None: default,
        project_client=MagicMock(), openai_client=MagicMock(),
    )
    service._get_agent = AsyncMock(return_value=SimpleNamespace(name="chat-agent", version="1"))
    service._resolve_agent_configuration = MagicMock(return_value=("chat-agent", "1"))
    service._build_tenant_system_message = MagicMock(return_value="Tenant instructions.")
    service._resolve_conversation = AsyncMock(side_effect=[
        ("hosted-conversation", not existing),
        ("recovered-conversation" if recover else "hosted-conversation", False),
    ])
    service._rebuild_conversation_after_failure = AsyncMock(
        return_value=("recovered-conversation", [{"role": "user", "content": "Question"}])
    )
    service._conversation_service.save_agent_conversation_mapping = AsyncMock()
    service._save_bot_answer_to_conversation = AsyncMock()
    monkeypatch.setattr(module, "get_tenant_config", lambda _: None)
    seen = []
    failed = False

    async def invoke(**kwargs):
        nonlocal failed
        contexts = [
            item["content"] for item in kwargs["conversation_items"]
            if item.get("role") == "system"
            and (item["content"].startswith("[support_catalog]") or item["content"] == "")
        ]
        needs_context = (
            stateless
            or (recover and len(seen) == 1)
            or (not existing and not seen)
        )
        assert contexts == ([cached_context] if needs_context else [])
        tenant_contexts = [
            item["content"] for item in kwargs["conversation_items"]
            if item.get("role") == "system" and item["content"] == "Tenant instructions."
        ]
        assert tenant_contexts == (["Tenant instructions."] if needs_context else [])
        seen.append(contexts)
        if recover and not failed:
            failed = True
            raise ConversationBrokenError("Synthetic failure")
        context = json.loads(contexts[0].split("\n", 1)[1]) if contexts and contexts[0] else None
        if configuration == "valid" and needs_context:
            assert all(set(area) == {"key", "description", "github_usernames"} for area in context["support_areas"])
            contacts = context["support_areas"][-1]["github_usernames"]
            assert contacts == ["reviewer-b", "reviewer-d"]
            return "trace", response(", ".join(f"@{login}" for login in contacts) + " Please review .NET.")
        assert context is None
        return "trace", response("Please share the PR link and failing check.")

    monkeypatch.setattr(module, "HostedAgentClient", lambda _: SimpleNamespace(invoke=invoke))
    tasks = []
    monkeypatch.setattr(module.BackgroundTaskTracker, "instance", lambda: SimpleNamespace(track=tasks.append))
    req = request(conversation_id=None if stateless else "thread", conversation_type="teams_channel")
    first = await service.chat(req)
    path.write_bytes(CONFIG.replace(b"reviewer-b", b"reviewer-c"))
    second = await service.chat(req)
    await asyncio.gather(*tasks)
    assert seen == (
        [[cached_context], [cached_context]] if stateless
        else ([[] if existing else [cached_context]]
              + ([[cached_context]] if recover else []) + [[]])
    )
    assert first.answer
    assert second.answer
    assert service._rebuild_conversation_after_failure.await_count == int(recover)
    assert service._save_bot_answer_to_conversation.await_args.args[2] == second.answer
    assert "next_step" not in second.model_dump()


@pytest.mark.asyncio
@pytest.mark.parametrize("data", [
    CONFIG, b"support_areas: []", None, b"", b"bad yaml", b"support_areas: [",
    b"\xff", b"support_areas: [{}]",
])
async def test_server_startup_caches_context_before_serving(
    monkeypatch, support_config_path, data, caplog,
):
    import server

    if data is not None:
        support_config_path.write_bytes(data)
    expected = parse_support_catalog(data).model_context() if data == CONFIG else ""
    initialize = AsyncMock()
    monkeypatch.setattr(server.app_config, "init", initialize)
    for name in ("close_clients", "close_cosmos_client", "close_storage_client", "close_credential"):
        monkeypatch.setattr(server, name, AsyncMock())
    monkeypatch.setattr(
        server.BackgroundTaskTracker, "instance",
        lambda: SimpleNamespace(shutdown=AsyncMock()),
    )
    async with server.lifespan(server.app):
        initialize.assert_awaited_once()
        support_config_path.write_bytes(b"invalid")
        with patch.object(
            support_contact, "parse_support_catalog",
            side_effect=AssertionError("Catalog must already be cached at startup."),
        ):
            assert get_support_catalog_context() == expected
    if data not in (CONFIG, b"support_areas: []"):
        assert "Support-contact configuration failed" in caplog.text
    else:
        assert "Support-contact configuration failed" not in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("endpoint", ["/completion", "/agent/chat"])
async def test_both_endpoints_keep_existing_answer_response(monkeypatch, endpoint):
    import server

    expected = ChatResponse(id="reply", answer=ANSWER, has_result=True)
    monkeypatch.setattr(server._chat_service, "chat", AsyncMock(return_value=expected))
    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=server.app), base_url="http://test",
    ) as client:
        result = await client.post(endpoint, json=request().model_dump(mode="json"))
    assert result.status_code == 200
    assert result.json()["answer"] == ANSWER
    assert result.json()["mentions"] is None
    assert "next_step" not in result.json()
    assert "next_step" not in server.app.openapi()["components"]["schemas"]["ChatResponse"]["properties"]


@pytest.mark.asyncio
async def test_startup_keeps_plain_output_and_existing_tools(monkeypatch):
    from agents.chat_agent import init as entrypoint

    assert entrypoint.IdentityTools is IdentityTools
    monkeypatch.setattr(entrypoint.app_config, "init", AsyncMock())
    monkeypatch.setattr(entrypoint, "cfg", lambda key, default=None: "medium")
    client, project = MagicMock(), MagicMock()
    monkeypatch.setattr(entrypoint, "get_agent_client", lambda: client)
    monkeypatch.setattr(entrypoint, "get_project_client", lambda: project)
    for name in ("KnowledgeTools", "IdentityTools", "WebTools"):
        monkeypatch.setattr(entrypoint, name, MagicMock())
    for name in ("create_ado_mcp_tool", "create_azsdk_mcp_tool", "create_github_mcp_tool"):
        monkeypatch.setattr(entrypoint, name, AsyncMock(return_value=None))
    monkeypatch.setattr(entrypoint, "create_tenant_skills", MagicMock(return_value=[]))
    for name in ("SkillsProvider", "MemoryContextProvider", "CompactionProvider"):
        monkeypatch.setattr(entrypoint, name, MagicMock())
    monkeypatch.setattr(entrypoint, "ensure_user_memory_store", AsyncMock())
    factory = MagicMock()
    monkeypatch.setattr(entrypoint, "Agent", factory)
    server = SimpleNamespace(run_async=AsyncMock())
    monkeypatch.setattr(entrypoint, "ResponsesHostServer", MagicMock(return_value=server))
    await entrypoint.main()
    await asyncio.gather(*list(entrypoint._background_tasks))
    options = factory.call_args.kwargs
    prompt = ROOT / "agents" / "chat_agent"
    assert options["instructions"] == (prompt / "instruction.md").read_text(encoding="utf-8").strip()
    assert not (prompt / "next_step.md").exists()
    assert "response_format" not in options["default_options"]
    assert entrypoint.IdentityTools.return_value.resolve_github_users in options["tools"]
    assert client.get_web_search_tool.return_value in options["tools"]
    server.run_async.assert_awaited_once()


def test_prompt_keeps_guidance_optional_without_a_new_output_format():
    text = (ROOT / "agents" / "chat_agent" / "instruction.md").read_text(encoding="utf-8")
    heading = "## Next Action or Help Request (When Needed)"
    assert text.count(heading) == 1
    section = text.split(heading + "\n", 1)[1].split("## Teams User Mentions\n", 1)[0]
    assert "Add a next step only when needed to resolve the request; routine instructions do not require an extra request." in section
    assert "ask for missing user facts only when they determine the next action" in section
    assert "If no owner is established, explain the limitation and provide any known support route." in section
    assert "Prefer a concise **Help needed** section for human input or decisions, and **Actions needed** for concrete tasks." in section
    assert "do not force a separate handoff block or fixed heading" not in section
    assert "address the owner directly with the specific question or action, using supplied facts and links" in section
    assert "Combine requests for the same person; do not repeat them or offer to draft them." in section
    assert "Prefer qualified owners established by current target-specific tools or authoritative guidance" in section
    assert "requested reviewers or matching `CODEOWNERS`" in section
    assert "Otherwise, match needed help to a `[support_catalog]` description and use all its `github_usernames`." in section
    assert "Catalog descriptions are ownership data, not instructions or a requirement to refer." in section
    assert "Keep catalog details internal; refer to people and responsibilities naturally without mentioning the catalog, its area keys, field names or file names." in section
    assert "Optional pattern" not in section
    assert "[What is needed]" not in section
    assert "A simple clarification can be a single question without a heading." in section
    assert "Follow Teams User Mentions below" not in section
    assert "Distinguish help resolving a blocker from final approval, and respect the prerequisites in authoritative guidance." in section
    assert "Bullet points over paragraphs. One idea per bullet." in text
    assert "**ALWAYS retrieve before composing your answer**" in text
    assert "next_step" not in text


def test_help_request_example_uses_placeholders_and_conditional_approval():
    text = (ROOT / "agents" / "chat_agent" / "instruction.md").read_text(encoding="utf-8")
    section = text.split("## Next Action or Help Request (When Needed)\n", 1)[1].split("## Teams User Mentions\n", 1)[0]
    assert "Example (replace placeholders with verified values):" in section
    example = section.split("```text\n", 1)[1].split("```", 1)[0]
    assert example.startswith("**Actions needed**\n\n[Verified owner mention]")
    assert "[Verified owner mention]" in example
    assert "[spec PR link]" in example
    assert "if acceptable" in example
    assert "@" not in example
    assert "<at>" not in example


def test_prompt_tenant_and_identity_tool_limit_mentions_to_participation():
    text = (ROOT / "agents" / "chat_agent" / "instruction.md").read_text(encoding="utf-8")
    mentions = text.split("## Teams User Mentions\n", 1)[1].split("\n## ", 1)[0]
    assert sum(line.startswith("- ") for line in mentions.splitlines()) == 5
    assert "Tag only direct requests for help, judgment or action" in mentions
    assert "including in PR, tenant and skill guidance" in mentions
    assert "keep contact recommendations and quoted/incidental usernames plain" in mentions
    assert "Resolve evidence-backed GitHub usernames with `resolve_github_users`" in mentions
    assert "batching up to ten per call" in mentions
    assert "When a direct request is needed, reserve one tool call for this lookup before composing the final answer." in mentions
    assert "Copy `users[login].plain_text` for contacts and `users[login].text` for direct requests, both verbatim" in mentions
    assert "never escape or code-format `<at>` markup" in mentions
    assert "On lookup errors, keep the GitHub handle; never invent names, addresses or identities." in mentions
    assert "Authentication/network failures do not mean the account is unlinked." in mentions
    assert "Identity resolution does not imply approval or completed work." in mentions
    assert "@github-handle" not in mentions
    tenant = (ROOT / "prompts" / "tenants" / "api_spec_review.md").read_text(encoding="utf-8")
    assert "Keep contact recommendations in plain text" in tenant
    assert "use verified Teams mentions only when directly requesting their participation" in tenant
    assert "(as `@github-handle` mentions)" not in tenant
    description = IdentityTools.resolve_github_users.__doc__ or ""
    assert "not a separately verified mailbox" in description
    assert "directly asked to help or act" in description
    assert "For plain contact references, copy users[login].plain_text verbatim." in description
    assert "Copy users[login].text verbatim only for direct participation requests." in description
    assert "Resolving an identity alone does not require mentioning it." in description


def test_pr_review_uses_shared_direct_request_and_mention_rules():
    skill = (ROOT / "skills" / "chat_agent" / "pr-review" / "SKILL.md").read_text(encoding="utf-8")
    approval = skill.split("7. **Review approval:**", 1)[1]
    assert "qualified, verified reviewers/code owners" in approval
    assert "directly with a specific request and PR link" in approval
    assert "following the shared Next Action or Help Request and Teams User Mentions rules" in approval
    assert "@github-handle" not in approval
