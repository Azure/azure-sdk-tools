"""Unit tests for ChatService memory scope resolution."""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

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


def test_merge_references_dedups_by_link_only() -> None:
    """Vector wins on link conflicts; non-conflicting graph hits appended."""
    from models.knowledge import Reference
    from services.chat_service import _merge_references

    vector = [
        Reference(title="Doc A", source="ai_search", link="https://example.com/a", content="VECTOR"),
        Reference(title="Doc B", source="ai_search", link="https://example.com/b", content="VEC-B"),
    ]
    graph = [
        # Same link as vector Doc A — even with a different title, dedup by
        # link drops it and the vector copy wins.
        Reference(title="Doc A (graph title)", source="graphrag", link="https://example.com/a", content="GRAPH"),
        Reference(title="Doc C", source="graphrag", link="https://example.com/c", content="GRAPH-C"),
    ]

    merged = _merge_references(vector, graph)
    titles = [r.title for r in merged]
    # Doc A deduped by link (vector wins), Doc B kept, Doc C appended.
    assert titles == ["Doc A", "Doc B", "Doc C"]
    # Vector copy survived (verbatim title + content, not the graph variant).
    assert merged[0].title == "Doc A"
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


def test_merge_references_keeps_graph_only_hits_without_link() -> None:
    """Graph references without a URL must still survive the merge so the
    bot's full_context payload (and the agent's reference enrichment
    lookup) sees them alongside vector hits."""
    from models.knowledge import Reference
    from services.chat_service import _merge_references

    vector = [
        Reference(title="Doc A", source="ai_search", link="https://example.com/a", content="VEC"),
    ]
    graph = [
        # Same doc surfaced by graph with no URL — link mismatch means it's
        # treated as a different key. That's fine: both copies carry useful
        # text and the agent's enrichment lookup matches on title too.
        Reference(title="Doc B (no link)", source="graphrag", link="", content="GRAPH-B"),
        # Same title but with link — different key, distinct entry kept.
        Reference(title="Doc C", source="graphrag", link="", content="GRAPH-C"),
    ]
    merged = _merge_references(vector, graph)
    titles = [r.title for r in merged]
    assert "Doc A" in titles
    assert "Doc B (no link)" in titles
    assert "Doc C" in titles
    # Graph entries preserved verbatim (source still 'graphrag').
    graph_only = [r for r in merged if r.source == "graphrag"]
    assert len(graph_only) == 2
