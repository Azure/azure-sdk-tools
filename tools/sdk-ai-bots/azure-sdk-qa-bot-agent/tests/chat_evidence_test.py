"""Offline tests for the evidence returned to evaluation callers."""

import json

import pytest
from openai.types.responses import (
    Response,
    ResponseFunctionToolCall,
    ResponseOutputMessage,
    ResponseOutputText,
)
from pydantic import BaseModel

from models.chat import AdditionalInfo, AdditionalInfoType, ChatRequest, Message
from services.chat_service import ChatService


class ToolOutput(BaseModel):
    """Hosted-agent output fields, independent of the SDK's union version."""

    type: str = "function_call_output"
    call_id: str
    output: str


def _response(*calls):
    output = []
    for index, (name, content) in enumerate(calls):
        call_id = f"call-{index}"
        output.extend([
            ResponseFunctionToolCall.model_construct(
                type="function_call", call_id=call_id, name=name, arguments="{}",
            ),
            ToolOutput(
                call_id=call_id,
                output=json.dumps({"results": [{
                    "title": f"Source {index}", "source": "kb",
                    "link": f"https://example.com/{index}", "content": content,
                }]}),
            ),
        ])
    output.append(ResponseOutputMessage.model_construct(
        id="answer", type="message", role="assistant", status="completed",
        content=[ResponseOutputText.model_construct(
            type="output_text", text="Answer", annotations=[],
        )],
    ))
    return Response.model_construct(id="response-id", output=output)


def _request(**kwargs):
    return ChatRequest(
        tenant_id="api_spec_review_bot", message=Message(role="user", content="Question"),
        **kwargs,
    )


def test_current_tool_outputs_preserve_all_search_and_wiki_evidence():
    service = object.__new__(ChatService)
    response = _response(
        ("search_knowledge_base", "First source"),
        ("wiki_search", "Wiki source"),
        ("search_knowledge_base", "Second source"),
    )
    result = service._postprocess(_request(with_full_context=True), response, None)
    contexts = json.loads(result.full_context)
    assert [doc["document_content"] for doc in contexts] == [
        "First source", "Second source", "Wiki source",
    ]
    assert result.answer == "Answer"


def test_legacy_message_tool_outputs_remain_supported():
    service = object.__new__(ChatService)
    response = _response(("search_knowledge_base", "Legacy source"))
    tool_output = response.output[1]
    response.output[1] = ResponseOutputMessage.model_construct(
        id="legacy", type="message", role="assistant", content=[],
        call_id=tool_output.call_id,
        output=json.dumps([tool_output.output]),
    )
    result = service._postprocess(_request(with_full_context=True), response, None)
    assert json.loads(result.full_context)[0]["document_content"] == "Legacy source"


def test_supplied_text_evidence_preserves_content_and_marks_provenance():
    service = object.__new__(ChatService)
    request = _request(
        with_full_context=True,
        additional_infos=[
            AdditionalInfo(type=AdditionalInfoType.Text, content="Original source excerpt",
                           link="https://example.com/source"),
            AdditionalInfo(type=AdditionalInfoType.Link, content="A link", link="https://example.com"),
            AdditionalInfo(type=AdditionalInfoType.Image, content="image"),
            AdditionalInfo(type=AdditionalInfoType.Text, content=" "),
        ],
    )
    result = service._postprocess(request, _response(), None)
    contexts = json.loads(result.full_context)
    assert len(contexts) == 1
    assert contexts[0]["document_content"] == "Original source excerpt"
    assert contexts[0]["document_source"] == "user_provided"
    assert contexts[0]["document_link"] == "https://example.com/source"
    assert request.message.content not in result.full_context
    assert result.answer not in result.full_context


@pytest.mark.parametrize("with_full_context", [False, None])
def test_context_is_only_returned_when_requested(with_full_context):
    service = object.__new__(ChatService)
    result = service._postprocess(
        _request(with_full_context=with_full_context),
        _response(("search_knowledge_base", "Source")),
        None,
    )
    assert result.full_context is None


def test_missing_evidence_does_not_use_the_answer_as_context():
    service = object.__new__(ChatService)
    result = service._postprocess(_request(with_full_context=True), _response(), None)
    assert result.full_context is None


def test_malformed_tool_output_is_logged_and_not_returned_as_evidence(caplog):
    service = object.__new__(ChatService)
    response = _response(("search_knowledge_base", "Source"))
    response.output[1].output = '{"results":"invalid"}'
    result = service._postprocess(_request(with_full_context=True), response, None)
    assert result.full_context is None
    assert "Failed to decode tool output for search_knowledge_base" in caplog.text
