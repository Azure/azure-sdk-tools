"""Behavioral contracts for the opt-in first-pass decision experiments."""

import json
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest
from pydantic import ValidationError

from models.chat import ChatRequest, Message
from models.knowledge import DocumentContext, Reference
from services.decision_context import (
    DecisionContext, DecisionEvidence, prepare_decision_context, validate_mode,
)


def context(mode="quoted"):
    return DecisionContext(mode, (
        DecisionEvidence("doc", "documentation", DocumentContext(
            document_title="Schedule configuration",
            document_content="The pipeline is scheduled daily at 17:00 Pacific.",
        )),
        DecisionEvidence("request_context", "request_context", DocumentContext(
            document_content="My PR missed the source fetch in the current run.",
        )),
    ))


def payload(kind="policy", quote="scheduled daily at 17:00 Pacific", source="doc",
            text="The pipeline is scheduled daily.", condition=""):
    return json.dumps({
        "claims": [{
            "kind": kind, "text": text,
            "citations": [{"source_id": source, "quote": quote}],
            "condition": condition,
        }],
        "missing_facts": [],
    })


def test_exact_quote_is_required():
    assert context().render(payload()) == "The pipeline is scheduled daily."
    answer = context().render(payload(quote="guaranteed to arrive tomorrow"))
    assert "withheld" in answer
    assert "The pipeline is scheduled daily." not in answer


def test_unknown_source_cannot_borrow_a_real_quote():
    assert "withheld" in context().render(payload(source="invented"))


@pytest.mark.parametrize("kind", ["execution_outcome", "authorization_outcome"])
def test_role_guard_rejects_outcomes_from_configuration(kind):
    answer = context("role_bound").render(payload(
        kind=kind, text="Your PR will arrive tomorrow.",
    ))
    assert "Your PR will arrive tomorrow." not in answer
    assert "withheld" in answer


def test_role_guard_requires_a_recommendation_condition():
    assert "withheld" in context("role_bound").render(payload(kind="recommendation"))
    assert context("role_bound").render(payload(
        kind="recommendation", condition="the configuration applies to your branch",
    )).startswith("If the configuration applies to your branch:")


def test_request_context_cannot_establish_policy():
    assert "withheld" in context("role_bound").render(payload(
        quote="missed the source fetch", source="request_context",
    ))


def test_extractive_renderer_does_not_publish_paraphrased_promise():
    answer = context("extractive").render(payload(text="Your PR will arrive tomorrow."))
    assert "tomorrow" not in answer
    assert "scheduled daily at 17:00 Pacific" in answer


def test_malformed_contract_is_an_explicit_error():
    with pytest.raises(ValidationError):
        context().render("This is not JSON.")
    with pytest.raises(ValueError, match="Unknown"):
        validate_mode("typo")


@pytest.mark.asyncio
@pytest.mark.parametrize("tenant,mode", [
    ("api_spec_review_bot", ""), ("azure_sdk_qa_bot", "quoted"),
])
async def test_default_and_other_tenants_do_not_retrieve(tenant, mode):
    service = SimpleNamespace(retrieve=AsyncMock())
    req = ChatRequest(tenant_id=tenant, message=Message(role="user", content="question"))
    assert await prepare_decision_context(req, mode, service) is None
    service.retrieve.assert_not_awaited()


@pytest.mark.asyncio
async def test_retrieved_content_and_provenance_are_not_replaced_by_claims():
    reference = Reference(
        title="Live source", source="wiki", link="https://example.test/source",
        content="Exact automatically acquired source text.",
    )
    service = SimpleNamespace(retrieve=AsyncMock(return_value=SimpleNamespace(
        knowledge_list=[reference, reference],
    )))
    req = ChatRequest(
        tenant_id="api_spec_review_bot",
        message=Message(role="user", content="What did the run fetch?"),
    )
    prepared = await prepare_decision_context(req, "evidence_only", service)
    assert len(prepared.evidence) == 2
    assert prepared.evidence[0].document.document_source == "wiki"
    assert prepared.evidence[0].document.document_content == reference.content
    assert prepared.evidence[1].role == "request_context"
    assert "automatically acquired" in prepared.input_text()


@pytest.mark.asyncio
async def test_actual_chat_path_delivers_contract_and_returns_actual_sources(monkeypatch):
    import services.chat_service as module

    prepared = context()
    monkeypatch.setattr(module, "get_tenant_config", lambda tenant: None)
    monkeypatch.setattr(module, "prepare_decision_context", AsyncMock(return_value=prepared))
    invoke = AsyncMock(return_value=("trace", SimpleNamespace(
        id="real-response-id", status="completed", output=[], output_text=payload(),
    )))
    monkeypatch.setattr(module.HostedAgentClient, "invoke", invoke)
    service = module.ChatService(settings=lambda key, default=None: default)
    service._get_project_client = lambda: None
    service._get_openai_client = lambda agent=None: None
    service._get_agent = AsyncMock(return_value=SimpleNamespace(name="agent", version="267"))
    service._build_tenant_system_message = lambda tenant: "Original system message"
    req = ChatRequest(
        tenant_id="api_spec_review_bot",
        message=Message(role="user", content="Which source was fetched?"),
        with_full_context=True,
    )
    result = await service.chat(req)
    inputs = invoke.call_args.kwargs["conversation_items"]
    assert any(item["role"] == "user" and "[decision_evidence]" in item["content"] for item in inputs)
    assert any(item["role"] == "system" and "DecisionAnswer" in item["content"] for item in inputs)
    assert result.id == "real-response-id"
    assert result.answer == "The pipeline is scheduled daily."
    docs = json.loads(result.full_context)
    assert docs[0]["document_content"] == prepared.evidence[0].document.document_content
    assert docs[1]["document_content"] == prepared.evidence[1].document.document_content


def test_quote_binding_does_not_claim_semantic_verification():
    assert context("quoted").render(payload(text="Your PR will arrive tomorrow.")) == (
        "Your PR will arrive tomorrow."
    )
