"""Offline tests for wiki page candidate handling."""

from __future__ import annotations

import asyncio
from types import SimpleNamespace
from typing import Any, cast

from models.knowledge import KnowledgeChunk
from tools.knowledge_tools import _deduplicate_wiki_pages, _refs_from_expanded
from utils.azure_ai_search import SearchClient


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
