"""Offline tests for wiki page candidate handling."""

from __future__ import annotations

import asyncio
from types import SimpleNamespace
from typing import Any, cast
from unittest.mock import AsyncMock

from azure.core.exceptions import HttpResponseError
import pytest

from config.tenant_config import TenantID
from models.knowledge import KnowledgeChunk
from tools.knowledge_tools import (
    KnowledgeTools,
    _deduplicate_wiki_pages,
    _refs_from_expanded,
)
from utils.azure_ai_search import NON_WIKI_FILTER, SearchClient


def test_wiki_page_deduplication_keeps_highest_scored_chunk():
    low = KnowledgeChunk(
        chunk_id="page-chunk-1",
        title="@added",
        source="wiki_entity",
        page_type="entity",
        rerank_score=0.02,
    )
    high = KnowledgeChunk(
        chunk_id="page-chunk-2",
        title="@added",
        source="wiki_entity",
        page_type="entity",
        rerank_score=0.05,
    )

    got = _deduplicate_wiki_pages([low, high])

    assert got == [high]


class _AsyncSearchResults:
    def __init__(self, docs):
        self._docs = docs

    def __aiter__(self):
        async def _iterate():
            for doc in self._docs:
                yield doc

        return _iterate()


class _HierarchySearchStub:
    async def search(self, **kwargs):
        return _AsyncSearchResults(
            [
                {
                    "chunk_id": "before",
                    "title": "versioning.md",
                    "chunk": "Earlier section context",
                    "context_id": "typespec_docs",
                    "header_1": "Versioning",
                    "header_2": None,
                    "header_3": None,
                },
                {
                    "chunk_id": "matched",
                    "title": "versioning.md",
                    "chunk": "Exact matched rule",
                    "context_id": "typespec_docs",
                    "header_1": "Versioning",
                    "header_2": None,
                    "header_3": None,
                },
            ]
        )


def test_hierarchy_expansion_keeps_matched_passage_first():
    client = object.__new__(SearchClient)
    cast(Any, client)._search_client = _HierarchySearchStub()
    hit = KnowledgeChunk(
        chunk_id="matched",
        title="versioning.md",
        source="typespec_docs",
        content="Exact matched rule",
        header1="Versioning",
    )

    expanded = asyncio.run(client.expand_by_hierarchy(hit))

    assert expanded.content.index("Exact matched rule") < expanded.content.index(
        "Earlier section context"
    )
    assert expanded.content.count("Exact matched rule") == 1


@pytest.mark.parametrize("source", ["static_typespec_qa", "static_api_spec_view_qa"])
@pytest.mark.parametrize("matched_index", [0, 1, 2, 27])
def test_static_qa_expands_the_whole_topic_from_any_hit(source, matched_index):
    identity = {"source": source, "title": "history.md", "header1": "SDK issue"}
    chunks = [
        KnowledgeChunk(chunk_id="intro", content="Topic introduction.", **identity),
        KnowledgeChunk(
            chunk_id="question", header2="Question", content="Original question.", **identity
        ),
        KnowledgeChunk(
            chunk_id="logs", header2="Question", header3="Logs",
            content="Original diagnostic log.", **identity
        ),
        *[
            KnowledgeChunk(
                chunk_id=f"answer-{index}", header2="Answer", header3=f"Step {index}",
                content=f"Answer fragment [{index:02}].", **identity
            )
            for index in range(25)
        ],
    ]

    async def search(**kwargs):
        selected = chunks if kwargs["top"] is None else chunks[:kwargs["top"]]
        return _AsyncSearchResults([c.model_dump() for c in selected])

    backend = SimpleNamespace(search=AsyncMock(side_effect=search))
    client = object.__new__(SearchClient)
    client._search_client = backend

    expanded = asyncio.run(client.expand_by_hierarchy(chunks[matched_index]))

    backend.search.assert_awaited_once()
    request = backend.search.call_args.kwargs
    assert request["top"] is None
    assert request["order_by"] == ["ordinal_position asc"]
    assert f"context_id eq '{source}'" in request["filter"]
    assert "title eq 'history.md'" in request["filter"]
    assert "header_1 eq 'SDK issue'" in request["filter"]
    assert NON_WIKI_FILTER in request["filter"]
    assert "header_2" not in request["filter"]
    assert "header_3" not in request["filter"]
    for chunk in chunks:
        assert expanded.content.count(chunk.content) == 1
    assert expanded.content.index("Answer fragment [00].") < expanded.content.index(
        "Original question."
    )
    assert expanded.source == source
    assert expanded.title == "history.md"
    assert expanded.header1 == "SDK issue"
    assert expanded.header2 == expanded.header3 == ""


def test_static_qa_deduplication_uses_source_document_and_topic():
    question = KnowledgeChunk(
        chunk_id="question", source="static_typespec_qa", title="history.md",
        header1="Topic", header2="Question",
    )
    answer = question.model_copy(update={"chunk_id": "answer", "header2": "Answer"})
    distinct = [
        question.model_copy(update={"chunk_id": "topic-2", "header1": "Other topic"}),
        question.model_copy(update={"chunk_id": "document-2", "title": "other.md"}),
        question.model_copy(update={"chunk_id": "source-2", "source": "static_api_spec_view_qa"}),
        question.model_copy(update={"chunk_id": "wiki", "page_type": "summary"}),
    ]

    assert SearchClient.deduplicate_chunks([question, answer, *distinct]) == [
        question, *distinct
    ]


def test_wiki_backfill_deduplicates_whole_qa_before_document_limit():
    question = KnowledgeChunk(
        chunk_id="question", source="static_typespec_qa", title="history.md",
        header1="Topic", header2="Question",
    )
    answer = question.model_copy(update={"chunk_id": "answer", "header2": "Answer"})
    second_topic = question.model_copy(update={"chunk_id": "topic-2", "header1": "Other topic"})
    other_document = question.model_copy(update={"chunk_id": "document-2", "title": "other.md"})
    client = object.__new__(SearchClient)
    client.fused_search = AsyncMock(return_value=[question, answer, second_topic, other_document])
    pages = [
        chunk.model_copy(update={"chunk_id": f"wiki-{index}", "page_type": "summary"})
        for index, chunk in enumerate((question, other_document))
    ]

    backfilled = asyncio.run(client.backfill_wiki_sources(pages))

    assert backfilled == [question, second_topic, other_document]
    assert NON_WIKI_FILTER in client.fused_search.call_args.kwargs["extra_filter"]


@pytest.mark.parametrize("page_type", ["summary", "entity", "concept"])
def test_wiki_pages_from_static_qa_keep_normal_section_expansion(page_type):
    hit = KnowledgeChunk(
        chunk_id="wiki", source="static_typespec_qa", title="history.md",
        page_type=page_type, header1="Topic", header2="Question", header3="Detail",
        content="Wiki evidence",
    )
    backend = SimpleNamespace(search=AsyncMock(return_value=_AsyncSearchResults([])))
    client = object.__new__(SearchClient)
    client._search_client = backend

    expanded = asyncio.run(client.expand_by_hierarchy(hit))

    request = backend.search.call_args.kwargs
    assert request["top"] == 20
    assert "header_2 eq 'Question'" in request["filter"]
    assert "header_3 eq 'Detail'" in request["filter"]
    assert NON_WIKI_FILTER not in request["filter"]
    assert "Wiki evidence" in expanded.content
    assert expanded.header2 == "Question"
    assert expanded.header3 == "Detail"


@pytest.mark.parametrize(("title", "header1"), [("", "Topic"), ("history.md", "")])
def test_static_qa_without_complete_identity_keeps_bounded_section_expansion(title, header1):
    hit = KnowledgeChunk(
        chunk_id="question", source="static_typespec_qa", title=title,
        header1=header1, header2="Question", content="Original question.",
    )
    backend = SimpleNamespace(search=AsyncMock(return_value=_AsyncSearchResults([])))
    client = object.__new__(SearchClient)
    client._search_client = backend

    expanded = asyncio.run(client.expand_by_hierarchy(hit))

    backend.search.assert_awaited_once()
    request = backend.search.call_args.kwargs
    assert request["top"] == 20
    assert "header_2 eq 'Question'" in request["filter"]
    assert NON_WIKI_FILTER not in request["filter"]
    assert "Original question." in expanded.content
    assert expanded.header2 == "Question"


def test_static_qa_expansion_surfaces_later_page_failure():
    class FailingPages:
        def __aiter__(self):
            async def iterate():
                yield {"chunk_id": "first", "header_2": "Answer", "chunk": "Partial answer"}
                raise HttpResponseError("next result page failed")
            return iterate()

    client = object.__new__(SearchClient)
    client._search_client = SimpleNamespace(
        search=AsyncMock(return_value=FailingPages())
    )
    hit = KnowledgeChunk(
        chunk_id="question", source="static_typespec_qa", title="history.md",
        header1="Topic", header2="Question", content="Question",
    )

    with pytest.raises(HttpResponseError, match="next result page failed"):
        asyncio.run(client.expand_by_hierarchy(hit))


@pytest.mark.parametrize("header", ["question", "Question", "QUESTION"])
@pytest.mark.parametrize("max_chars", [1100, 3000])
def test_question_expansion_keeps_paired_answer_before_long_logs(header, max_chars):
    question = KnowledgeChunk(
        chunk_id="question",
        source="static_typespec_qa",
        title="typespec.md",
        header1="SDK validation errors",
        header2=header,
        header3="Logs",
        content="Long diagnostic log. " * 400,
    )
    answers = [
        {
            "chunk_id": "answer-1",
            "header_2": "Answer",
            "header_3": "Private staging",
            "chunk": "These failures can be ignored only on private repository main.",
        },
        {
            "chunk_id": "answer-2",
            "header_2": "Answer",
            "header_3": "Shipping",
            "chunk": "For shipping branches, resolve the SDK tooling version mismatch.",
        },
    ]

    async def search(**kwargs):
        return _AsyncSearchResults([question.model_dump(), *answers])

    client = object.__new__(SearchClient)
    backend = SimpleNamespace(search=AsyncMock(side_effect=search))
    client._search_client = backend

    expanded = asyncio.run(client.expand_by_hierarchy(question))
    ref = _refs_from_expanded(
        [expanded], [question], max_content_chars=max_chars
    )[0]

    assert "## Paired answer" in ref.content
    assert "only on private repository main" in ref.content
    assert "For shipping branches" in ref.content
    assert "### Private staging" in ref.content
    assert "### Shipping" in ref.content
    assert ref.content.index("## Paired answer") < ref.content.index("## Surrounding section")
    assert ref.content.count(answers[0]["chunk"]) == 1
    assert ref.content.count(answers[1]["chunk"]) == 1
    assert ref.content.endswith("... [truncated]")
    assert len(ref.content) == max_chars + len("\n... [truncated]")
    backend.search.assert_awaited_once()
    answer_call = backend.search.call_args
    assert "header_2" not in answer_call.kwargs["filter"]
    assert "header_3" not in answer_call.kwargs["filter"]
    assert answer_call.kwargs["top"] is None
    assert answer_call.kwargs["order_by"] == ["ordinal_position asc"]


def test_paired_answer_lookup_keeps_document_topic_and_source_filters():
    question = KnowledgeChunk(
        source="static_qa's_qa",
        title="file's.md",
        header1="Resource's name",
        header2="question",
    )
    backend = SimpleNamespace(
        search=AsyncMock(side_effect=lambda **_: _AsyncSearchResults([]))
    )
    client = object.__new__(SearchClient)
    client._search_client = backend
    asyncio.run(client.expand_by_hierarchy(question))

    backend.search.assert_awaited_once()
    for call in backend.search.call_args_list:
        assert "title eq 'file''s.md'" in call.kwargs["filter"]
        assert "context_id eq 'static_qa''s_qa'" in call.kwargs["filter"]
        assert "header_1 eq 'Resource''s name'" in call.kwargs["filter"]
        assert "service_type" not in call.kwargs["filter"]
        assert "search.ismatch" not in call.kwargs["filter"]
    answer_filter = backend.search.call_args.kwargs["filter"]
    assert NON_WIKI_FILTER in answer_filter
    assert "header_2" not in answer_filter
    assert "header_3" not in answer_filter


def test_question_without_paired_answer_preserves_question_and_reports_gap(caplog):
    question = KnowledgeChunk(
        chunk_id="q",
        source="static_typespec_qa",
        title="typespec.md",
        header1="Unanswered question",
        header2="question",
        content="How do I validate this?",
    )
    client = object.__new__(SearchClient)
    client._search_client = SimpleNamespace(
        search=AsyncMock(side_effect=lambda **_: _AsyncSearchResults([]))
    )

    with caplog.at_level("INFO", logger="utils.azure_ai_search"):
        expanded = asyncio.run(client.expand_by_hierarchy(question))

    assert "How do I validate this?" in expanded.content
    assert "Paired answer" not in expanded.content
    assert "No paired answer found" in caplog.text


@pytest.mark.parametrize(
    ("page_type", "header1", "header2"),
    [
        ("summary", "Topic", "question"),
        ("entity", "Topic", "question"),
        ("", "Topic", "answer"),
        ("", "Topic", "question"),
        ("", "Topic", "Overview"),
        ("", "", "question"),
    ],
)
@pytest.mark.parametrize("source", ["typespec_docs", "static_arm_docs"])
def test_non_question_sections_do_not_trigger_paired_answer_lookup(
    page_type, header1, header2, source
):
    hit = KnowledgeChunk(
        source=source,
        title="docs.md",
        header1=header1,
        header2=header2,
        page_type=page_type,
        content="Matched evidence",
    )
    client = object.__new__(SearchClient)
    backend = SimpleNamespace(
        search=AsyncMock(side_effect=lambda **_: _AsyncSearchResults([]))
    )
    client._search_client = backend

    expanded = asyncio.run(client.expand_by_hierarchy(hit))

    backend.search.assert_awaited_once()
    assert backend.search.call_args.kwargs["top"] == 20
    assert "search.in(header_2" not in backend.search.call_args.kwargs["filter"]
    assert "Matched evidence" in expanded.content


def test_paired_answer_lookup_surfaces_search_failures():
    async def search(**kwargs):
        raise HttpResponseError("answer lookup failed")

    client = object.__new__(SearchClient)
    client._search_client = SimpleNamespace(search=AsyncMock(side_effect=search))
    question = KnowledgeChunk(
        source="static_typespec_qa",
        title="typespec.md",
        header1="Question title",
        header2="question",
    )

    with pytest.raises(HttpResponseError, match="answer lookup failed"):
        asyncio.run(client.expand_by_hierarchy(question))


@pytest.mark.parametrize("track", ["search_knowledge_base", "wiki_search"])
def test_both_retrieval_tracks_expand_paired_answer_under_source_scope(track):
    question = KnowledgeChunk(
        chunk_id="q",
        source="static_typespec_qa",
        title="typespec.md",
        header1="Versioned resource name",
        header2="question",
        content="How can I version this name?",
    )
    wiki_page = KnowledgeChunk(
        chunk_id="wiki",
        source="static_typespec_qa",
        title="typespec.md",
        header1="Versioned resource name",
        page_type="summary",
        content="Wiki overview",
    )

    async def search(**kwargs):
        if NON_WIKI_FILTER in kwargs["filter"]:
            return _AsyncSearchResults([
                question.model_dump(),
                {
                    "chunk_id": "answer",
                    "header_2": "Answer",
                    "chunk": "Version the resource and all of its operations.",
                },
            ])
        return _AsyncSearchResults([wiki_page.model_dump()])

    client = object.__new__(SearchClient)
    client._search_client = SimpleNamespace(search=AsyncMock(side_effect=search))
    wrapper = SimpleNamespace(
        fused_search=AsyncMock(
            return_value=[wiki_page] if track == "wiki_search" else [question]
        ),
        deduplicate_chunks=SearchClient.deduplicate_chunks,
        top_k=1,
        backfill_wiki_sources=AsyncMock(return_value=[question]),
        expand_by_hierarchy=AsyncMock(wraps=client.expand_by_hierarchy),
    )
    tools = KnowledgeTools(search_client=wrapper)

    if track == "search_knowledge_base":
        result = asyncio.run(tools.search_knowledge_base(
            queries=["Versioned resource name"],
            sources=["static_typespec_qa"],
            tenant_id=TenantID.TYPESPEC_CHANNEL_QA_BOT.value,
            service_type="management-plane",
        ))
    else:
        result = asyncio.run(tools.wiki_search(
            queries=["Versioned resource name"],
            sources=["static_typespec_qa"],
            tenant_id=TenantID.TYPESPEC_CHANNEL_QA_BOT.value,
        ))

    assert "Version the resource and all of its operations." in result.results[-1].content
    expected_scope = "context_id eq 'static_typespec_qa'"
    if track == "search_knowledge_base":
        expected_scope += " and (service_type eq 'management-plane' or service_type eq null)"
    initial_filters = wrapper.fused_search.await_args.args[1]
    assert initial_filters["static_typespec_qa"] == expected_scope
    assert all(
        not call.kwargs for call in wrapper.expand_by_hierarchy.call_args_list
    )
    for call in client._search_client.search.call_args_list:
        assert "service_type" not in call.kwargs["filter"]
        assert "context_id eq 'static_typespec_qa'" in call.kwargs["filter"]
        assert "title eq 'typespec.md'" in call.kwargs["filter"]
        assert "header_1 eq 'Versioned resource name'" in call.kwargs["filter"]
        if NON_WIKI_FILTER in call.kwargs["filter"]:
            assert "header_2" not in call.kwargs["filter"]
            assert "header_3" not in call.kwargs["filter"]
            assert call.kwargs["top"] is None
    if track == "wiki_search":
        wrapper.backfill_wiki_sources.assert_awaited_once()
        assert expected_scope in wrapper.backfill_wiki_sources.call_args.kwargs["source_filter"]


def test_wiki_references_apply_the_requested_content_limit():
    expanded = [
        SimpleNamespace(
            title="Versioning",
            header1=None,
            header2=None,
            header3=None,
            source="typespec_docs",
            link="",
            content="x" * 2000,
        )
    ]
    scored = [KnowledgeChunk(chunk_id="1", rerank_score=0.5)]

    refs = _refs_from_expanded(expanded, scored, max_content_chars=1100)

    assert refs[0].content == ("x" * 1100) + "\n... [truncated]"
