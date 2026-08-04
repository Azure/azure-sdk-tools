"""Unit tests for the search-result model."""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from models.knowledge import KnowledgeChunk


def test_wiki_page_with_null_headers() -> None:
    """Wiki pages only set ``header_1``; the deeper levels come back null."""
    chunk = KnowledgeChunk.model_validate(
        {
            "chunk_id": "abc_pages_0",
            "title": "@maxItems",
            "chunk": "Array assertion decorator.",
            "context_id": "wiki_entity",
            "header_1": "@maxItems",
            "header_2": None,
            "header_3": None,
            "page_type": "entity",
            "chunk_refs_str": None,
            "@search.reranker_score": None,
        }
    )

    assert chunk.header1 == "@maxItems"
    assert chunk.header2 == ""
    assert chunk.header3 == ""
    assert chunk.page_type == "entity"
    assert chunk.chunk_refs == []
    assert chunk.rerank_score == 0.0
