"""Regression tests for preserving references through response postprocessing."""

from types import SimpleNamespace

import pytest

from models.chat import ChatRequest, Message
from models.knowledge import Reference
from services.chat_service import ChatService


@pytest.mark.parametrize(
    "heading", ["**References**", "References", "### References", "**References**:"]
)
def test_extracts_linked_references_with_common_headings(heading: str) -> None:
    text = f"Answer.\n\n{heading}\n- [Guide](https://example.com/guide)"

    answer, references = ChatService._extract_references_from_text(text)

    assert answer == "Answer."
    assert len(references) == 1
    assert references[0].title == "Guide"
    assert references[0].link == "https://example.com/guide"


@pytest.mark.parametrize("bullet", ["-", "*", "+", "1.", "1)"])
def test_resolves_plain_titles_from_tool_results(bullet: str) -> None:
    reference = Reference(
        title="Contribution model",
        link="https://example.com/contribution",
        source="knowledge",
        content="Guidance",
    )

    answer, references = ChatService._extract_references_from_text(
        f"Answer.\n\n**References**\n{bullet} Contribution model", [reference]
    )

    assert answer == "Answer."
    assert references == [reference]


def test_enriches_linked_reference_using_tool_result() -> None:
    reference = Reference(
        title="Guide | knowledge",
        link="https://example.com/guide",
        source="knowledge",
        content="Guidance",
    )

    answer, references = ChatService._extract_references_from_text(
        "Answer.\n\n**References**\n- [Guide](https://example.com/guide)",
        [reference],
    )

    assert answer == "Answer."
    assert references == [reference]


@pytest.mark.parametrize("heading", ["**References**", "References"])
def test_preserves_supplied_onboarding_reference_shape(heading: str, caplog) -> None:
    text = (
        f"PR status and onboarding guidance.\n\n{heading}\n"
        "- PR: microsoft/mcp-pr #680 - Add monitor traces commands for Azure Monitor\n"
        "- Issue: microsoft/mcp-pr #697 - [ONBOARD] Azure Monitor Traces tools\n"
        "- Contribution model for the Azure MCP Server\n"
        "- Getting started with the Azure MCP Server"
    )

    answer, references = ChatService._extract_references_from_text(text)

    assert answer == text
    assert references == []
    assert "unresolved entry" in caplog.text


@pytest.mark.parametrize(
    "extra",
    [
        "- Unknown title",
        "- [Broken link](https://example.com",
        "Additional guidance after references.",
    ],
)
def test_does_not_partially_strip_references(extra: str, caplog) -> None:
    text = (
        "Answer.\n\n**References**\n"
        f"- [Guide](https://example.com/guide)\n{extra}"
    )

    answer, references = ChatService._extract_references_from_text(text)

    assert answer == text
    assert references == []
    assert "Preserving references section" in caplog.text


def test_preserves_partially_resolvable_plain_titles() -> None:
    text = "Answer.\n\n**References**\n- Known title\n- Unknown title"
    reference = Reference(
        title="Known title", source="knowledge", link="https://example.com/known"
    )

    answer, references = ChatService._extract_references_from_text(text, [reference])

    assert answer == text
    assert references == []


@pytest.mark.parametrize("text", ["Answer without sources.", "Answer.\n\n**References**"])
def test_preserves_text_without_extractable_references(text: str) -> None:
    assert ChatService._extract_references_from_text(text) == (text, [])


def test_does_not_treat_inline_references_word_as_a_section() -> None:
    text = "The **References** field contains source links.\n- Some other list item"
    assert ChatService._extract_references_from_text(text) == (text, [])


@pytest.mark.parametrize("linked", [True, False])
def test_postprocess_keeps_references_visible_in_completion_payload(linked: bool) -> None:
    entry = "[Guide](https://example.com/guide)" if linked else "Guide without a URL"
    text = f"Answer.\n\n**References**\n- {entry}"
    service = ChatService()
    request = ChatRequest(
        tenant_id="azure_mcp_server", message=Message(role="user", content="Question")
    )
    response = SimpleNamespace(id="response", output=[], output_text=text)

    result = service._postprocess(request, response, None)
    payload = result.model_dump(mode="json")

    if linked:
        assert payload["answer"] == "Answer."
        assert payload["references"][0]["link"] == "https://example.com/guide"
    else:
        assert payload["answer"] == text
        assert payload["references"] is None
