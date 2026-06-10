"""Tests for ChatService memory scope resolution."""

from __future__ import annotations

import sys
from pathlib import Path

from models.chat import ChatRequest, Message as ChatMessage

# Ensure the project root is on sys.path so ``services`` resolves.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from services.chat_service import ChatService


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


def test_graph_citations_to_references_preserves_metadata() -> None:
    """Graph citations should round-trip into Reference with source='graphrag'."""
    from models.knowledge import GraphCitation
    from services.chat_service import _graph_citations_to_references

    citations = [
        GraphCitation(
            title="Doc A",
            link="https://example.com/a",
            snippet="excerpt",
        )
    ]
    refs = _graph_citations_to_references(citations)
    assert len(refs) == 1
    assert refs[0].title == "Doc A"
    assert refs[0].link == "https://example.com/a"
    assert refs[0].source == "graphrag"
    assert refs[0].content == "excerpt"


def test_merge_references_dedups_by_link_and_title() -> None:
    """Vector wins on conflicts (primary-source evidence first)."""
    from models.knowledge import Reference
    from services.chat_service import _merge_references

    vector = [
        Reference(title="Doc A", source="ai_search", link="https://example.com/a", content="VECTOR"),
        Reference(title="Doc B", source="ai_search", link="https://example.com/b", content="VEC-B"),
    ]
    graph = [
        Reference(title="Doc A", source="graphrag", link="https://example.com/a", content="GRAPH"),
        Reference(title="Doc C", source="graphrag", link="https://example.com/c", content="GRAPH-C"),
    ]

    merged = _merge_references(vector, graph)
    titles = [r.title for r in merged]
    # Doc A deduped (vector wins), Doc B kept, Doc C appended.
    assert titles == ["Doc A", "Doc B", "Doc C"]
    # Vector copy survived.
    assert merged[0].content == "VECTOR"
    assert merged[0].source == "ai_search"


def test_merge_references_empty_secondary_returns_primary_copy() -> None:
    from models.knowledge import Reference
    from services.chat_service import _merge_references

    vector = [Reference(title="A", source="ai_search", link="l", content="c")]
    merged = _merge_references(vector, [])
    assert merged == vector
    # Returned list is a copy, not the same object.
    assert merged is not vector
