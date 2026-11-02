"""Grouped next-step generation, catalog snapshots and non-notifying responses."""

from __future__ import annotations

import asyncio
import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import httpx
import pytest
import yaml
from azure.core.exceptions import ResourceNotFoundError
from pydantic import ValidationError

from models.chat import ChatRequest, ChatResponse
from models.guided_next_step import (
    GeneratedGuidedAnswer,
    NextStep,
    validate_guided_answer,
)
from services.chat_service import ChatService
from services.support_contact_service import SupportContactService, parse_support_catalog
from utils.azure_ai_foundry_agent import ConversationBrokenError

ROOT = Path(__file__).resolve().parents[1]
SAMPLE = ROOT / "config" / "support-contact.example.yaml"
CONFIG = SAMPLE.read_bytes()
STEP = {
    "category": "human_action",
    "targets": [
        {"contact_id": "reviewer_a", "support_areas": ["sdk_python", "sdk_java"]},
        {"contact_id": "reviewer_b", "support_areas": ["sdk_dotnet"]},
    ],
}
ANSWER = (
    "**Approvals needed**\n\n"
    "{{contact:reviewer_a}} Please review Python and Java.\n\n"
    "{{contact:reviewer_b}} Please review .NET.\n\n"
    "PR: https://example.test/pr/123"
)


def settings(key, default=None):
    return "support-contact.yaml" if key == "SUPPORT_CONTACT_BLOB" else default


def generated(**changes):
    return {
        "schema_version": "guided-next-step-v1",
        "answer": ANSWER,
        "next_step": STEP,
        **changes,
    }


def request(**changes):
    return ChatRequest(
        tenant_id="api_spec_review_bot",
        message={"role": "user", "content": "Question"},
        **changes,
    )


def response(payload):
    return SimpleNamespace(
        id="response-1", status="completed", output=[],
        output_text=json.dumps(payload) if isinstance(payload, dict) else payload,
    )


def test_native_schema_requires_full_proposal_and_no_confidence_dependency():
    schema = GeneratedGuidedAnswer.model_json_schema()
    assert list(schema["properties"]) == ["schema_version", "answer", "next_step"]
    assert set(schema["required"]) == set(schema["properties"])
    assert schema["additionalProperties"] is False
    assert set(schema["$defs"]["NextStep"]["required"]) == {"category", "targets"}
    assert set(schema["$defs"]["NextStepTarget"]["required"]) == {"contact_id", "support_areas"}
    assert "assessment" not in schema["properties"]
    result = GeneratedGuidedAnswer.model_validate(generated())
    assert result.model_dump(mode="json") == generated()


def test_catalog_projection_hides_real_identities():
    raw = yaml.safe_load(CONFIG)
    raw["contacts"]["reviewer_a"].update(
        teams_user_id="private-identity", maintainer="private-owner",
    )
    catalog = parse_support_catalog(yaml.safe_dump(raw).encode())
    context = json.loads(catalog.model_context().split("\n", 1)[1])
    assert context["support_areas"] == raw["support_areas"]
    assert context["revision"] == catalog.revision
    assert "private" not in catalog.model_context()
    assert "notification_enabled" not in catalog.model_context()


@pytest.mark.parametrize("data", [
    b"", b"[]", b"support_areas: []",
    b"support_areas: []\nsupport_areas: []\ncontacts: {}",
    b"support_areas: []\ncontacts: {a: {}, a: {}}",
    b"support_areas: [{key: bad-key, description: Test, contact_id: a}]\ncontacts: {a: {}}",
    b"support_areas: [{key: area, description: ' ', contact_id: a}]\ncontacts: {a: {}}",
    b"support_areas: [{key: area, description: Test, contact_id: missing}]\ncontacts: {}",
    b"support_areas: [{key: area, description: Test, contact_id: a}, {key: area, description: Other, contact_id: a}]\ncontacts: {a: {}}",
    b"support_areas: []\ncontacts: {a: {extra: true}}",
    b"support_areas: []\ncontacts: {a: {notification_enabled: 'false'}}",
    b"!!python/object/apply:os.system ['echo unsafe']",
])
def test_invalid_catalog_fails(data):
    with pytest.raises((ValueError, yaml.YAMLError)):
        parse_support_catalog(data)


@pytest.mark.parametrize("step", [
    {"category": "unknown", "targets": []},
    {"category": "missing_information", "targets": STEP["targets"]},
    {"category": "human_action", "targets": [STEP["targets"][0]] * 2},
    {"category": "human_action", "targets": [
        {"contact_id": "reviewer_a", "support_areas": ["sdk_python", "sdk_python"]},
    ]},
    {"category": "human_help", "targets": [
        {"contact_id": "reviewer_a", "support_areas": []},
    ]},
    {"category": "human_action", "targets": [], "notify": True},
])
def test_invalid_target_contract_fails(step):
    with pytest.raises(ValidationError):
        NextStep.model_validate(step)


@pytest.mark.parametrize("changes", [
    {"answer": " "},
    {"schema_version": "guided-next-step-v2"},
    {"next_step": {"category": "human_action", "targets": [
        {"contact_id": "reviewer_a", "support_areas": ["sdk_dotnet"]},
    ]}, "answer": "{{contact:reviewer_a}} Review this."},
    {"answer": ANSWER + "\n{{contact:reviewer_a}} Again."},
    {"answer": ANSWER + "\n{{contact:unknown}}"},
    {"answer": ANSWER.replace("{{contact:reviewer_a}}", "{{contact}}")},
    {"answer": ANSWER.replace("{{contact:reviewer_a}}", "")},
    {"answer": ANSWER + "<at>person</at>"},
    {"next_step": None},
])
def test_invalid_generated_proposal_is_explicitly_rejected(changes, caplog):
    with pytest.raises(ValueError):
        ChatService()._postprocess(request(), response(generated(**changes)), None, parse_support_catalog(CONFIG))
    assert "Invalid guided answer" in caplog.text


def test_grouped_answer_is_returned_once_with_metadata_without_mentions():
    result = ChatService()._postprocess(
        request(conversation_type="teams_channel"), response(generated()), None,
        parse_support_catalog(CONFIG),
    )
    assert result.answer == ANSWER
    assert result.answer.count("https://example.test/pr/123") == 1
    assert result.next_step.model_dump(mode="json") == STEP
    assert result.mentions is None
    assert "teams_user_id" not in result.model_dump_json()
    assert ChatResponse.model_validate_json(result.model_dump_json()) == result


@pytest.mark.parametrize("step", [None, {"category": "missing_information", "targets": []},
                                      {"category": "human_help", "targets": []}])
def test_no_contact_path_does_not_add_another_request(step):
    answer = "Please share the PR link and failing check."
    result = ChatService()._postprocess(
        request(), response(generated(answer=answer, next_step=step)), None,
        parse_support_catalog(CONFIG),
    )
    assert result.answer == answer
    assert result.answer.count("PR link") == 1
    assert (result.next_step is None) == (step is None)


def test_disabled_catalog_and_legacy_agents(caplog):
    with pytest.raises(ValueError, match="requires this turn"):
        validate_guided_answer(GeneratedGuidedAnswer.model_validate(generated()), None)
    payload = generated(answer="Normal answer.", next_step=None)
    assert ChatService()._postprocess(request(), response(payload), None).answer == "Normal answer."
    result = ChatService()._postprocess(
        request(), response("Older plain-text answer."), None, parse_support_catalog(CONFIG),
    )
    assert result.answer == "Older plain-text answer." and result.next_step is None
    assert "unavailable from older agent" in caplog.text


@pytest.mark.parametrize("configured", [False, True])
def test_existing_verified_mentions_remain_when_catalog_is_disabled(monkeypatch, configured):
    from models.identity import ResolveGitHubUsersResult, TeamsMention, TeamsUser

    mention = TeamsMention(
        text="<at>verified@example.test</at>",
        mentioned=TeamsUser(id="verified-id", name="Verified user"),
    )
    service = ChatService()
    monkeypatch.setattr(service, "_extract_tool_results", lambda _: {
        "resolve_github_users": ResolveGitHubUsersResult(mentions={"user": mention}),
    })
    req = request(conversation_type="teams_channel")
    if configured:
        # Even verified tool identities must not activate mentions in a guided preview.
        result = service._postprocess(req, response(generated()), None, parse_support_catalog(CONFIG))
        assert result.mentions is None
    else:
        result = service._postprocess(
            req, response(generated(answer=f"Please ask {mention.text}.", next_step=None)), None,
        )
        assert result.mentions == [mention]


def test_structured_answer_keeps_reference_processing():
    raw = generated(answer=ANSWER + "\n\n**References**\n- [Guide](https://example.test/guide)")
    result = ChatService()._postprocess(request(), response(raw), None, parse_support_catalog(CONFIG))
    assert result.answer == ANSWER
    assert result.references[0].link == "https://example.test/guide"


def test_malformed_guided_output_is_not_displayed_as_json(caplog):
    with pytest.raises(json.JSONDecodeError):
        ChatService()._postprocess(
            request(), response('{"schema_version":"guided-next-step-v1","answer":'), None,
        )
    assert "Malformed guided answer" in caplog.text


@pytest.mark.asyncio
async def test_catalog_disabled_by_default():
    loader = AsyncMock()
    service = SupportContactService(settings=lambda key, default=None: default, loader=loader)
    assert await service.get_catalog() is None
    loader.assert_not_awaited()


@pytest.mark.asyncio
async def test_concurrent_loads_share_cache_and_refresh_is_per_turn(monkeypatch):
    clock = [0.0]
    monkeypatch.setattr("services.support_contact_service.time.monotonic", lambda: clock[0])
    loader = AsyncMock(side_effect=[CONFIG, CONFIG.replace(b"reviewer_b", b"reviewer_c")])
    service = SupportContactService(settings=settings, loader=loader)
    first = await asyncio.gather(*(service.get_catalog() for _ in range(10)))
    assert all(catalog is first[0] for catalog in first)
    loader.assert_awaited_once_with("bot-configs", "support-contact.yaml")
    clock[0] = 301.0
    new = await service.get_catalog()
    assert first[0].revision != new.revision
    generated_answer = GeneratedGuidedAnswer.model_validate(generated())
    validate_guided_answer(generated_answer, first[0])
    with pytest.raises(ValueError):
        validate_guided_answer(generated_answer, new)


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", [b"", b"invalid yaml", ResourceNotFoundError("missing")])
async def test_refresh_failure_does_not_silently_return_stale_routes(monkeypatch, failure, caplog):
    clock = [0.0]
    monkeypatch.setattr("services.support_contact_service.time.monotonic", lambda: clock[0])
    loader = AsyncMock(side_effect=[CONFIG, failure])
    service = SupportContactService(settings=settings, loader=loader)
    await service.get_catalog()
    clock[0] = 301.0
    with pytest.raises(RuntimeError, match="valid support-contact"):
        await service.get_catalog()
    assert "Support-contact configuration failed" in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("ttl", ["-1", "nan", "inf", "bad"])
async def test_invalid_ttl_is_explicit(ttl):
    service = SupportContactService(
        settings=lambda key, default=None: ttl if key == "SUPPORT_CONTACT_CACHE_TTL_SECONDS" else settings(key, default),
        loader=AsyncMock(return_value=CONFIG),
    )
    with pytest.raises(RuntimeError):
        await service.get_catalog()


@pytest.mark.asyncio
async def test_container_setting_and_blob_changes_do_not_reuse_wrong_cache():
    config = {"SUPPORT_CONTACT_BLOB": "first.yaml"}
    loader = AsyncMock(return_value=CONFIG)
    service = SupportContactService(settings=config.get, loader=loader)
    await service.get_catalog()
    config.update(SUPPORT_CONTACT_BLOB="second.yaml", STORAGE_CONFIG_CONTAINER="other")
    await service.get_catalog()
    assert loader.await_args.args == ("other", "second.yaml")
    assert loader.await_count == 2
    config["SUPPORT_CONTACT_BLOB"] = ""
    assert await service.get_catalog() is None
    assert loader.await_count == 2


@pytest.mark.asyncio
@pytest.mark.parametrize("existing", [False, True])
@pytest.mark.parametrize("recover", [False, True])
async def test_catalog_context_in_every_turn_and_recovery(monkeypatch, existing, recover):
    import services.chat_service as module

    loader = AsyncMock(side_effect=[CONFIG, CONFIG.replace(b"reviewer_b", b"reviewer_c")])
    configured = lambda key, default=None: (
        "0" if key == "SUPPORT_CONTACT_CACHE_TTL_SECONDS" else settings(key, default)
    )
    service = ChatService(settings=configured, project_client=MagicMock(), openai_client=MagicMock())
    service._support_contact_service = SupportContactService(settings=configured, loader=loader)
    service._get_agent = AsyncMock(return_value=SimpleNamespace(name="chat-agent", version="1"))
    service._resolve_agent_configuration = MagicMock(return_value=("chat-agent", "1"))
    service._build_tenant_system_message = MagicMock(return_value="Tenant instructions.")
    service._resolve_conversation = AsyncMock(return_value=("hosted-conversation", not existing))
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
            if item.get("role") == "system" and item["content"].startswith("[support_catalog]")
        ]
        assert len(contexts) == 1
        if recover and not failed:
            failed = True
            raise ConversationBrokenError("Synthetic failure")
        context = json.loads(contexts[0].split("\n", 1)[1])
        seen.append(context)
        assert all(set(area) == {"key", "description", "contact_id"} for area in context["support_areas"])
        key = context["support_areas"][-1]["contact_id"]
        return "trace", response(generated(
            answer=f"{{{{contact:{key}}}}} Please review .NET.",
            next_step={"category": "human_action", "targets": [
                {"contact_id": key, "support_areas": ["sdk_dotnet"]},
            ]},
        ))

    monkeypatch.setattr(module, "HostedAgentClient", lambda _: SimpleNamespace(invoke=invoke))
    tasks = []
    monkeypatch.setattr(module.BackgroundTaskTracker, "instance", lambda: SimpleNamespace(track=tasks.append))
    req = request(conversation_id="thread", conversation_type="teams_channel")
    first = await service.chat(req)
    second = await service.chat(req)
    await asyncio.gather(*tasks)
    assert first.next_step.targets[0].contact_id == "reviewer_b"
    assert second.next_step.targets[0].contact_id == "reviewer_c"
    assert seen[0]["revision"] != seen[1]["revision"]
    assert loader.await_count == 2
    assert service._rebuild_conversation_after_failure.await_count == int(recover)
    assert service._save_bot_answer_to_conversation.await_args.args[2] == second.answer


@pytest.mark.asyncio
@pytest.mark.parametrize("endpoint", ["/completion", "/agent/chat"])
async def test_both_endpoints_expose_routing_metadata_without_displaying_it(monkeypatch, endpoint):
    import server

    expected = ChatResponse(
        id="preview", answer=ANSWER, has_result=True, next_step=NextStep.model_validate(STEP),
    )
    monkeypatch.setattr(server._chat_service, "chat", AsyncMock(return_value=expected))
    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=server.app), base_url="http://test",
    ) as client:
        result = await client.post(endpoint, json=request().model_dump(mode="json"))
    assert result.status_code == 200
    assert result.json()["answer"] == ANSWER
    assert result.json()["next_step"] == STEP
    assert result.json()["mentions"] is None


@pytest.mark.asyncio
async def test_hosted_agent_startup_enables_structured_next_steps_without_replacing_tools(monkeypatch):
    from agents.chat_agent import init as entrypoint

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
    assert options["instructions"] == (
        (prompt / "instruction.md").read_text(encoding="utf-8").strip() + "\n\n"
        + (prompt / "next_step.md").read_text(encoding="utf-8").strip()
    )
    assert options["default_options"]["response_format"] is GeneratedGuidedAnswer
    assert entrypoint.IdentityTools.return_value.resolve_github_users in options["tools"]
    assert client.get_web_search_tool.return_value in options["tools"]
    server.run_async.assert_awaited_once()
