"""Offline unit tests for wiki ``chunk_refs`` parsing.

Wiki pages store their source refs as a JSON-array string in ``chunk_refs_str``;
``KnowledgeChunk`` parses it back into a list.
"""

from __future__ import annotations

import asyncio

from models.knowledge import KnowledgeChunk
from utils.azure_ai_search import SearchClient, split_source_ref


def test_split_source_ref_separates_context_and_title():
    assert split_source_ref("typespec_docs/foo/README.md") == (
        "typespec_docs",
        "foo/README.md",
    )
    assert split_source_ref("python_docs/README.md") == ("python_docs", "README.md")
    # A ref with no folder keeps an empty context and the whole path as title.
    assert split_source_ref("README.md") == ("", "README.md")
    assert split_source_ref("") == ("", "")


def test_chunk_refs_parsed_from_json_string():
    c = KnowledgeChunk.model_validate(
        {
            "chunk_id": "e1",
            "page_type": "entity",
            "chunk_refs_str": '["a/b.md", "c/d.md"]',
        }
    )
    assert c.chunk_refs == ["a/b.md", "c/d.md"]


def test_wiki_fields_normalize_null_values():
    wiki = KnowledgeChunk.model_validate(
        {
            "chunk_id": "wiki",
            "header_1": "@maxItems",
            "header_2": None,
            "header_3": None,
            "page_type": "entity",
            "chunk_refs_str": None,
            "@search.reranker_score": None,
        }
    )
    assert wiki.header1 == "@maxItems"
    assert wiki.header2 == ""
    assert wiki.header3 == ""
    assert wiki.page_type == "entity"
    assert wiki.chunk_refs == []
    assert wiki.rerank_score == 0.0

    for source in (
        {"chunk_id": "r1"},
        {"chunk_id": "i1", "chunk_refs_str": "[]"},
    ):
        assert KnowledgeChunk.model_validate(source).chunk_refs == []


def test_malformed_chunk_refs_str_is_ignored():
    c = KnowledgeChunk.model_validate({"chunk_id": "m1", "chunk_refs_str": "not-json"})
    assert c.chunk_refs == []


class _BackfillSearchClient(SearchClient):
    def __init__(self):
        self.queries = []
        self.source_filters = {}
        self.extra_filter: str | None = None

    async def fused_search(
        self, queries, source_filters, *, extra_filter=None, use_agentic=False
    ):
        self.queries = queries
        self.source_filters = source_filters
        self.extra_filter = extra_filter
        return [
            KnowledgeChunk(
                chunk_id="raw-1",
                title="a.md",
                source="typespec_docs",
                content="relevant source passage",
            )
        ]


def test_wiki_backfill_reranks_referenced_documents_with_user_query():
    client = _BackfillSearchClient()
    wiki = KnowledgeChunk(
        chunk_id="wiki-1",
        title="@added",
        source="wiki_entity",
        page_type="entity",
        chunk_refs=["typespec_docs/a.md", "typespec_docs/b.md"],
    )

    got = asyncio.run(
        client.backfill_wiki_sources(
            [wiki],
            queries=["How does @added affect API versions?"],
            source_filter="(context_id eq 'typespec_docs')",
        )
    )

    assert [c.chunk_id for c in got] == ["raw-1"]
    assert client.queries == ["How does @added affect API versions?"]
    assert "title eq 'a.md'" in next(iter(client.source_filters.values()))
    assert client.extra_filter is not None
    assert "page_type eq null" in client.extra_filter
