"""Offline coverage of always-on answering-model assessments."""

from __future__ import annotations

import asyncio
import itertools
import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import httpx
import pytest
from pydantic import ValidationError

from models.chat import ChatRequest, ChatResponse
from models.evidence_assessment import (
    CURRENT_RUBRIC_VERSION,
    CURRENT_SCHEMA_VERSION,
    AspectResult,
    AspectValue,
    EvidenceAssessment,
    EvidenceAspects,
    GeneratedAssessmentAnswer,
)
from models.knowledge import DocumentContext, Reference, SearchKnowledgeBaseResult
from services.chat_service import ChatService
from services.evidence_assessment_service import provisional_band

ASPECTS = tuple(EvidenceAspects.model_fields)
ASSESSMENT_PROMPT = (
    Path(__file__).resolve().parents[1] / "agents" / "chat_agent" / "evidence_assessment.md"
)


def _criteria(instructions: str) -> dict[str, dict[str, str]]:
    criteria = {}
    for line in instructions.splitlines():
        if line.startswith("| **"):
            heading, *definitions = [cell.strip() for cell in line.strip("|").split("|")]
            aspect = heading.strip("*").lower().replace(" ", "_")
            criteria[aspect] = dict(zip(("pass", "limited", "fail"), definitions, strict=True))
    return criteria


def _aspect(value: AspectValue) -> AspectResult:
    reasons = {
        "pass": "The inspected evidence meets this requirement.",
        "limited": "A disclosed, bounded limitation remains in a non-decisive part.",
        "fail": "An essential requirement remains unmet.",
        "not_applicable": "This aspect is irrelevant to the request.",
    }
    return AspectResult(value=value, reason=reasons[value])


def _aspects(value: AspectValue = "pass") -> EvidenceAspects:
    return EvidenceAspects.model_validate({aspect: _aspect(value) for aspect in ASPECTS})


def _generated(
    value: AspectValue | None = "pass",
    *,
    answer: str = "Single answer",
) -> GeneratedAssessmentAnswer:
    return GeneratedAssessmentAnswer(
        schema_version=CURRENT_SCHEMA_VERSION,
        rubric_version=CURRENT_RUBRIC_VERSION,
        answer=answer,
        status="valid" if value is not None else "abstained",
        aspects=_aspects(value) if value is not None else None,
    )


def test_static_instructions_use_one_versioned_rubric_and_request_brief_reasons():
    instructions = ASSESSMENT_PROMPT.read_text(encoding="utf-8").strip()
    criteria = _criteria(instructions)
    assert tuple(criteria) == ASPECTS
    assert CURRENT_RUBRIC_VERSION == "evidence-v4"
    assert instructions.count(f"Rubric version: {CURRENT_RUBRIC_VERSION}.") == 1
    assert instructions.count("## Assessment rules") == 1
    assert instructions.count("## Aspect criteria") == 1
    assert "same final generation" in instructions
    assert "one short, single-line sentence" in instructions
    assert "Use `not_applicable` only for an irrelevant aspect and explain why" in instructions
    assert "missing evidence is not irrelevance" in instructions
    assert "only when assessment cannot be performed, not merely because evidence is weak" in instructions
    assert "Teams" not in instructions
    assert all(set(definitions) == {"pass", "limited", "fail"} for definitions in criteria.values())
    assert "Fail before Limited before Pass" in instructions
    assert "all important claims, directly or by sound deduction with supported premises" in criteria["support"]["pass"]
    assert "recent observations of the correct target" in criteria["support"]["pass"]
    assert "all necessary premises are supported" in criteria["support"]["limited"]
    assert "disclosed, evidence-bounded uncertainty is non-decisive" in criteria["support"]["limited"]
    assert "unverified prerequisite that could change the main recommendation" in criteria["support"]["fail"]
    assert "Caveats do not repair it." in criteria["support"]["fail"]
    assert "Unverified claims of live state or completion Fail" in criteria["support"]["fail"]
    assert "documentation alone cannot verify them" in criteria["support"]["fail"]
    assert "at the requested specificity" in criteria["fit"]["pass"]
    assert "supplementary guidance stays in scope" in criteria["fit"]["pass"]
    assert "materially limits only a secondary part" in criteria["fit"]["limited"]
    assert "main recommendation applies" in criteria["fit"]["limited"]
    assert "named applicability condition" in criteria["fit"]["limited"]
    assert "unknown condition prevents judging whether the main recommendation applies" in criteria["fit"]["fail"]
    assert "name that condition, not merely an inaccessible source" in criteria["fit"]["fail"]
    assert "attributable, trustworthy and authoritative for each important claim type" in criteria["source_quality"]["pass"]
    assert "Missing factual support alone is not an authority failure." in criteria["source_quality"]["pass"]
    assert "Minimum authority is met" in criteria["source_quality"]["limited"]
    assert "non-decisive provenance or authority limitation" in criteria["source_quality"]["limited"]
    assert "secondary guidance cannot substitute" in criteria["source_quality"]["fail"]
    assert "evidence-backed reconciliation of scope, time and authority" in criteria["consistency"]["pass"]
    assert "Required/approved labels and different-commit reviews are not inherently conflicting" in criteria["consistency"]["pass"]
    assert "disclosed, separable, non-decisive disagreement" in criteria["consistency"]["limited"]
    assert "all supported interpretations preserve the main diagnosis, recommendation and next step" in criteria["consistency"]["limited"]
    assert "choosing a source alone does not resolve it" in criteria["consistency"]["fail"]
    assert "All main and explicitly required outcomes are resolved" in criteria["coverage"]["pass"]
    assert "optional omissions or relevant extras do not lower the rating" in criteria["coverage"]["pass"]
    assert "main outcome is resolved" in criteria["coverage"]["limited"]
    assert "material secondary requested part remains unresolved and needs follow-up" in criteria["coverage"]["limited"]
    assert "main requested outcome remains unresolved or an essential requested part is missing" in criteria["coverage"]["fail"]
    assert "clarification alone does not resolve the original task" in criteria["coverage"]["fail"]


def test_assessment_rules_preserve_normal_retrieval_and_aspect_specific_thresholds():
    instructions = ASSESSMENT_PROMPT.read_text(encoding="utf-8")
    assert "Use the normal answering and retrieval workflow" in instructions
    assert "do not retrieve additional evidence solely for assessment" in instructions
    assert "do not retrieve or execute anything" not in instructions
    rules = instructions.split("## Assessment rules\n", 1)[1].split("## Aspect criteria\n", 1)[0]
    assert len([line for line in rules.splitlines() if line.startswith("- ")]) == 3
    assert "important claims (those that can change the diagnosis, recommendation, next step or expected result)" in rules
    assert "the full request for Coverage" in rules
    assert "Fail before Limited before Pass without averaging claims" in rules
    assert "Limited requires" not in rules
    assert "Pass means" not in rules
    assert "Fail means" not in rules


def test_design_label_definitions_match_the_runtime_rubric():
    design = Path(__file__).resolve().parents[2] / "docs" / "confidence-aware-participation-design.md"
    section = design.read_text(encoding="utf-8").split("### 2.2 Evidence aspects\n", 1)[1]
    section = section.split("### 2.3 Evidence bands\n", 1)[0]
    assert _criteria(section) == _criteria(ASSESSMENT_PROMPT.read_text(encoding="utf-8"))
    models = ASSESSMENT_PROMPT.parents[2] / "tsp" / "models.tsp"
    definitions = models.read_text(encoding="utf-8")
    assert f'schema_version: "{CURRENT_SCHEMA_VERSION}";' in definitions
    assert f'rubric_version: "{CURRENT_RUBRIC_VERSION}";' in definitions


def test_generation_schema_requires_all_five_value_reason_pairs():
    schema = GeneratedAssessmentAnswer.model_json_schema()
    assert list(schema["properties"]) == [
        "schema_version", "rubric_version", "answer", "status", "aspects",
    ]
    assert schema["properties"]["schema_version"]["const"] == CURRENT_SCHEMA_VERSION
    assert schema["properties"]["rubric_version"]["const"] == CURRENT_RUBRIC_VERSION
    assert set(schema["required"]) == {
        "schema_version", "rubric_version", "answer", "status", "aspects",
    }
    assert set(schema["$defs"]["EvidenceAspects"]["required"]) == set(ASPECTS)
    result_schema = schema["$defs"]["AspectResult"]
    assert set(result_schema["required"]) == {"value", "reason"}
    assert result_schema["additionalProperties"] is False
    assert set(result_schema["properties"]["value"]["enum"]) == {
        "pass", "limited", "fail", "not_applicable",
    }


@pytest.mark.parametrize("reason", [
    None, 1, "", " \t ", "First line.\nSecond line.", "First line.\rSecond line.",
    "First line.\r\nSecond line.", "First line.\u2028Second line.",
])
def test_aspect_reasons_must_be_nonblank_single_line_strings(reason):
    with pytest.raises(ValidationError):
        AspectResult.model_validate({"value": "pass", "reason": reason})


@pytest.mark.parametrize("value", ["pass", "limited", "fail", "not_applicable"])
def test_every_aspect_value_requires_a_reason(value):
    with pytest.raises(ValidationError):
        AspectResult.model_validate({"value": value})
    result = AspectResult(value=value, reason="  Observed evidence explains this value.  ")
    assert result.reason == "Observed evidence explains this value."


@pytest.mark.parametrize("aspect", ASPECTS)
def test_valid_generation_requires_every_aspect(aspect):
    raw = _generated().model_dump()
    del raw["aspects"][aspect]
    with pytest.raises(ValidationError):
        GeneratedAssessmentAnswer.model_validate(raw)


@pytest.mark.parametrize("raw", [
    {"value": "abstain", "reason": "Unable to assess."},
    {"value": "unknown", "reason": "An unsupported label."},
    {"value": "pass", "reason": "Supported.", "probability": 1.0},
])
def test_aspect_result_rejects_unknown_labels_and_fields(raw):
    with pytest.raises(ValidationError):
        AspectResult.model_validate(raw)


def test_generated_answer_preserves_reasons_without_experimental_capture_fields():
    generated = _generated()
    raw = generated.model_dump(mode="json")
    assert generated.schema_version == "confidence-runtime-v3"
    assert generated.rubric_version == CURRENT_RUBRIC_VERSION
    assert not {
        "mode", "answering", "decision", "model_context", "evidence_sha256",
        "confidence", "needs_expert_help", "notify_experts",
    } & raw.keys()
    assert GeneratedAssessmentAnswer.model_validate_json(generated.model_dump_json()) == generated
    assert raw["aspects"]["support"] == {"value": "pass", "reason": _aspect("pass").reason}


@pytest.mark.parametrize("field,value", [
    ("schema_version", "confidence-runtime-v1"),
    ("schema_version", "confidence-runtime-v2"),
    ("rubric_version", "replay-v1"),
    ("rubric_version", "evidence-v2"),
    ("rubric_version", "evidence-v3"),
])
def test_old_results_cannot_be_silently_reinterpreted_as_current_assessments(field, value):
    raw = _generated().model_dump()
    raw[field] = value
    with pytest.raises(ValidationError):
        GeneratedAssessmentAnswer.model_validate(raw)


def test_bands_are_deterministic_and_use_values_not_explanation_text():
    assert provisional_band(_aspects()) == "High"
    assert provisional_band(_aspects("limited")) == "Medium"
    assert provisional_band(_aspects("fail")) == "Low"
    assert provisional_band(_aspects("not_applicable")) is None
    changed_reasons = EvidenceAspects.model_validate({
        aspect: {"value": "fail", "reason": "Explanation text cannot change the aspect value."}
        for aspect in ASPECTS
    })
    assert provisional_band(changed_reasons) == provisional_band(_aspects("fail"))


def test_all_aspect_combinations_use_existing_confidence_bands():
    for combination in itertools.product(("pass", "limited", "fail", "not_applicable"), repeat=5):
        aspects = EvidenceAspects.model_validate({
            aspect: _aspect(value) for aspect, value in zip(ASPECTS, combination)
        })
        if "fail" in combination:
            expected = "Low"
        elif "limited" in combination:
            expected = "Medium"
        elif "pass" in combination:
            expected = "High"
        else:
            expected = None
        assert provisional_band(aspects) == expected


def test_assessment_abstention_is_not_an_aspect_value_or_success():
    raw = _generated().model_dump()
    raw["aspects"] = None
    with pytest.raises(ValidationError):
        GeneratedAssessmentAnswer.model_validate(raw)
    raw = _generated().model_dump()
    raw["status"] = "abstained"
    with pytest.raises(ValidationError):
        GeneratedAssessmentAnswer.model_validate(raw)
    assert _generated(None).aspects is None
    assert provisional_band(_generated(None).aspects) is None


@pytest.mark.parametrize("value", ["pass", "limited", "fail", "not_applicable", None])
def test_normal_postprocessing_returns_plain_answer_and_logs_assessment(caplog, value):
    caplog.set_level("INFO", logger="services.chat_service")
    generated = _generated(value)
    response = SimpleNamespace(id="response-1", output=[], output_text=generated.model_dump_json())
    request = ChatRequest(tenant_id="api_spec_review_bot", message={"role": "user", "content": "Question"})
    result = ChatService()._postprocess(request, response, None)
    assert result.answer == generated.answer
    assert not {"confidence", "notify_experts"} & result.model_dump().keys()
    assert result.full_context is None
    assert "Evidence assessment: response_id=response-1" in caplog.text
    assert f'"status":"{generated.status}"' in caplog.text
    expected_band = {"pass": "High", "limited": "Medium", "fail": "Low", "not_applicable": None, None: None}[value]
    expected_assessment = {
        **generated.model_dump(mode="json", exclude={"answer"}),
        "provisional_band": expected_band,
    }
    assert result.assessment is not None
    assert result.assessment.model_dump(mode="json") == expected_assessment
    assert result.model_dump(mode="json")["assessment"] == expected_assessment
    assert ChatResponse.model_validate_json(result.model_dump_json()).assessment == result.assessment
    record = next(record for record in caplog.records if record.getMessage().startswith("Evidence assessment:"))
    assert json.loads(record.getMessage().split("assessment=", 1)[1]) == expected_assessment
    if value is not None:
        assert _aspect(value).reason in caplog.text


def test_normal_postprocessing_preserves_document_context_for_existing_evaluation():
    reference = Reference(
        title="Guide", link="https://example.com/guide", source="guidelines", content="Source passage",
    )
    generated = _generated(answer="Guidance\n\n**References**\n- [Guide](https://example.com/guide)")
    service = ChatService()
    service._extract_tool_results = MagicMock(return_value={
        "search_knowledge_base": SearchKnowledgeBaseResult(results=[reference]),
    })
    request = ChatRequest(
        tenant_id="api_spec_review_bot", message={"role": "user", "content": "Question"},
        with_full_context=True,
    )
    response = SimpleNamespace(id="response-1", output=[], output_text=generated.model_dump_json())
    result = service._postprocess(request, response, "hosted-conversation")
    assert result.answer == "Guidance"
    assert result.agent_conversation_id == "hosted-conversation"
    assert result.references[0].content == "Source passage"
    assert json.loads(result.full_context) == [DocumentContext.from_reference(reference).model_dump(mode="json")]
    assert result.assessment.aspects == generated.aspects
    assert "answer" not in result.assessment.model_dump()


@pytest.mark.parametrize("answer", ["Plain answer.", '{"result": "User-requested JSON"}'])
def test_plain_output_from_older_deployments_is_preserved(answer):
    response = SimpleNamespace(id="plain-response", output=[], output_text=answer)
    request = ChatRequest(tenant_id="api_spec_review_bot", message={"role": "user", "content": "Question"})
    result = ChatService()._postprocess(request, response, None)
    assert result.answer == answer
    assert result.assessment is None
    assert result.model_dump()["assessment"] is None
    assert json.loads(result.model_dump_json())["assessment"] is None
    assert result.model_dump()["full_context"] is None


@pytest.mark.parametrize("assessment_fields", [{}, {"assessment": None}], ids=["omitted", "null"])
def test_assessment_is_optional_without_expert_or_delivery_contract_changes(assessment_fields):
    response = ChatResponse.model_validate({
        "id": "response-1", "answer": "Plain answer.", "has_result": True, **assessment_fields,
    })
    assert response.assessment is None
    assert response.model_dump(mode="json")["assessment"] is None
    assert not {"confidence", "notify_experts"} & ChatResponse.model_fields.keys()
    assert "channel_id" not in ChatRequest.model_fields
    assert "assessment" not in ChatResponse.model_json_schema()["required"]
    assert set(ChatResponse.model_json_schema(mode="serialization")["properties"]) == set(ChatResponse.model_fields)
    schema = EvidenceAssessment.model_json_schema()
    assert set(schema["required"]) == {
        "schema_version", "rubric_version", "status", "aspects", "provisional_band",
    }
    assert "answer" not in schema["properties"]


@pytest.mark.parametrize("change", ["missing_aspects", "abstained_with_aspects", "abstained_with_band"])
def test_response_assessment_preserves_status_invariants(change):
    raw = {
        **_generated().model_dump(mode="json", exclude={"answer"}),
        "provisional_band": "High",
    }
    if change == "missing_aspects":
        raw["aspects"] = None
    elif change == "abstained_with_aspects":
        raw["status"] = "abstained"
    else:
        raw.update(status="abstained", aspects=None)
    with pytest.raises(ValidationError):
        EvidenceAssessment.model_validate(raw)


@pytest.mark.asyncio
@pytest.mark.parametrize("endpoint", ["/completion", "/agent/chat"])
@pytest.mark.parametrize("value", ["pass", "limited", "fail", "not_applicable", None, "legacy"])
async def test_chat_endpoints_serialize_assessment_without_changing_visible_answer(monkeypatch, endpoint, value):
    import server

    request = ChatRequest(
        tenant_id="api_spec_review_bot", message={"role": "user", "content": "Question"},
    )
    output_text = "Plain answer." if value == "legacy" else _generated(value).model_dump_json()
    response = SimpleNamespace(id="response-1", output=[], output_text=output_text)
    expected = ChatService()._postprocess(request, response, None)
    service = SimpleNamespace(chat=AsyncMock(return_value=expected))
    monkeypatch.setattr(server, "_chat_service", service)
    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=server.app), base_url="http://test",
    ) as client:
        result = await client.post(endpoint, json=request.model_dump(mode="json"))

    assert result.status_code == 200
    assert result.json() == expected.model_dump(mode="json")
    if value == "legacy":
        assert result.json()["assessment"] is None
    else:
        assert result.json()["assessment"]["status"] == ("abstained" if value is None else "valid")
        assert "answer" not in result.json()["assessment"]
    service.chat.assert_awaited_once()


def test_both_chat_routes_document_the_assessment_response():
    import server

    schema = server.app.openapi()
    response_schema = schema["components"]["schemas"]["ChatResponse"]
    assert "assessment" in response_schema["properties"]
    assert "assessment" not in response_schema["required"]
    assert {"type": "null"} in response_schema["properties"]["assessment"]["anyOf"]
    for endpoint in ("/completion", "/agent/chat"):
        documented = schema["paths"][endpoint]["post"]["responses"]["200"]["content"]["application/json"]["schema"]
        assert documented == {"$ref": "#/components/schemas/ChatResponse"}


@pytest.mark.asyncio
@pytest.mark.parametrize("value", ["pass", "fail", None])
async def test_chat_keeps_normal_conversation_and_persists_only_the_answer(monkeypatch, value):
    import services.chat_service as chat_module

    generated = _generated(value)
    response = SimpleNamespace(
        id="response-1", status="completed", output=[], output_text=generated.model_dump_json(),
    )
    client = SimpleNamespace(invoke=AsyncMock(return_value=("trace-1", response)))
    monkeypatch.setattr(chat_module, "HostedAgentClient", lambda _: client)
    monkeypatch.setattr(chat_module, "get_tenant_config", lambda _: SimpleNamespace())
    tasks = []
    monkeypatch.setattr(
        chat_module.BackgroundTaskTracker, "instance",
        lambda: SimpleNamespace(track=tasks.append),
    )
    openai = MagicMock()
    openai.conversations.retrieve = AsyncMock()
    service = ChatService(
        project_client=MagicMock(), openai_client=openai,
        settings=lambda key, default=None: default,
    )
    service._resolve_agent_configuration = MagicMock(return_value=("chat-agent", "1"))
    service._get_agent = AsyncMock(return_value=SimpleNamespace(name="chat-agent", version="1"))
    service._build_tenant_system_message = MagicMock(return_value="Tenant instructions.")
    service._conversation_service.get_agent_conversation_id = AsyncMock(return_value="hosted-conversation")
    service._conversation_service.save_conversation = AsyncMock()
    request = ChatRequest(
        tenant_id="api_spec_review_bot", conversation_id="thread-1", conversation_type="teams_channel",
        message={"role": "user", "content": "Question"},
    )

    result = await service.chat(request)
    await asyncio.gather(*tasks)

    assert result.answer == generated.answer
    assert result.id == response.id and result.trace_id == "trace-1"
    assert result.agent_conversation_id == "hosted-conversation"
    assert result.assessment.status == generated.status
    assert result.assessment.aspects == generated.aspects
    openai.conversations.retrieve.assert_awaited_once_with("hosted-conversation")
    assert client.invoke.await_args.kwargs["agent_conversation_id"] == "hosted-conversation"
    saved = service._conversation_service.save_conversation.await_args.args[0]
    assert saved.content == generated.answer


@pytest.mark.parametrize("change", ["missing_reason", "multiline_reason", "missing_aspect", "invalid_status", "old_rubric"])
def test_invalid_current_schema_assessment_fails_explicitly_in_backend(caplog, change):
    raw = _generated().model_dump()
    if change == "missing_reason":
        del raw["aspects"]["support"]["reason"]
    elif change == "multiline_reason":
        raw["aspects"]["support"]["reason"] = "First line.\nSecond line."
    elif change == "missing_aspect":
        del raw["aspects"]["support"]
    elif change == "old_rubric":
        raw["rubric_version"] = "evidence-v3"
    else:
        raw["status"] = "unknown"
    response = SimpleNamespace(id="invalid-response", output=[], output_text=json.dumps(raw))
    request = ChatRequest(tenant_id="api_spec_review_bot", message={"role": "user", "content": "Question"})
    with pytest.raises(ValidationError) as error:
        ChatService()._postprocess(request, response, None)
    if change == "old_rubric":
        assert any(item["loc"] == ("rubric_version",) for item in error.value.errors())
    assert "Invalid evidence assessment: response_id=invalid-response" in caplog.text


@pytest.mark.asyncio
async def test_agent_startup_always_enables_assessment_and_keeps_normal_runtime(monkeypatch):
    from agents.chat_agent import init as entrypoint
    from utils.tool_security import ToolOutputSecurityMiddleware

    monkeypatch.setenv("CONFIDENCE_ASSESSMENT_MODE", "off")
    monkeypatch.setenv("LOCAL_AGENT_ENDPOINT", "http://unused.invalid:8088")
    monkeypatch.setattr(entrypoint.app_config, "init", AsyncMock())
    monkeypatch.setattr(entrypoint, "cfg", lambda key, default=None: "medium")
    client, project = MagicMock(), MagicMock()
    monkeypatch.setattr(entrypoint, "get_agent_client", lambda: client)
    monkeypatch.setattr(entrypoint, "get_project_client", lambda: project)
    for name in ("KnowledgeTools", "WebTools"):
        monkeypatch.setattr(entrypoint, name, MagicMock())
    monkeypatch.setattr(entrypoint, "create_ado_mcp_tool", AsyncMock(return_value=None))
    azsdk_tool = object()
    azsdk_factory = AsyncMock(return_value=azsdk_tool)
    monkeypatch.setattr(entrypoint, "create_azsdk_mcp_tool", azsdk_factory)
    monkeypatch.setattr(entrypoint, "create_github_mcp_tool", AsyncMock(return_value=None))
    monkeypatch.setattr(entrypoint, "create_tenant_skills", MagicMock(return_value=[]))
    skills, memory, compaction = object(), object(), object()
    monkeypatch.setattr(entrypoint, "SkillsProvider", MagicMock(return_value=skills))
    memory_factory = MagicMock(return_value=memory)
    monkeypatch.setattr(entrypoint, "MemoryContextProvider", memory_factory)
    monkeypatch.setattr(entrypoint, "CompactionProvider", MagicMock(return_value=compaction))
    ensure_memory = AsyncMock()
    monkeypatch.setattr(entrypoint, "ensure_user_memory_store", ensure_memory)
    agent_factory = MagicMock()
    monkeypatch.setattr(entrypoint, "Agent", agent_factory)
    server = SimpleNamespace(run_async=AsyncMock())
    server_factory = MagicMock(return_value=server)
    monkeypatch.setattr(entrypoint, "ResponsesHostServer", server_factory)

    await entrypoint.main()
    await asyncio.gather(*list(entrypoint._background_tasks))

    options = agent_factory.call_args.kwargs
    original = ASSESSMENT_PROMPT.with_name("instruction.md").read_text(encoding="utf-8").strip()
    assessment = ASSESSMENT_PROMPT.read_text(encoding="utf-8").strip()
    assert options["instructions"] == original + "\n\n" + assessment
    assert options["default_options"]["response_format"] is GeneratedAssessmentAnswer
    assert options["default_options"]["include"] == ["web_search_call.action.sources"]
    assert options["context_providers"] == [skills, memory, compaction]
    assert len(options["middleware"]) == 1
    assert isinstance(options["middleware"][0], ToolOutputSecurityMiddleware)
    assert azsdk_tool in options["tools"]
    azsdk_factory.assert_awaited_once_with()
    assert client.get_web_search_tool.return_value in options["tools"]
    client.get_web_search_tool.assert_called_once_with(search_context_size="medium")
    memory_factory.assert_called_once_with(project)
    ensure_memory.assert_awaited_once_with(project)
    server_factory.assert_called_once_with(agent_factory.return_value)
    server.run_async.assert_awaited_once_with()
