"""Offline unit tests for wiki ``chunk_refs`` parsing.

Wiki pages store their source refs as a JSON-array string in ``chunk_refs_str``;
``KnowledgeChunk`` parses it back into a list.
"""

from __future__ import annotations

from models.knowledge import KnowledgeChunk


def test_chunk_refs_parsed_from_json_string():
    c = KnowledgeChunk.model_validate(
        {"chunk_id": "e1", "page_type": "entity", "chunk_refs_str": '["a/b.md", "c/d.md"]'}
    )
    assert c.chunk_refs == ["a/b.md", "c/d.md"]


def test_empty_or_missing_chunk_refs_yield_empty_list():
    for source in (
        {"chunk_id": "r1"},                       # missing (raw chunk)
        {"chunk_id": "s1", "chunk_refs_str": None},  # null
        {"chunk_id": "i1", "chunk_refs_str": "[]"},  # empty array
    ):
        assert KnowledgeChunk.model_validate(source).chunk_refs == []


def test_malformed_chunk_refs_str_falls_back_to_single_element():
    c = KnowledgeChunk.model_validate({"chunk_id": "m1", "chunk_refs_str": "not-json"})
    assert c.chunk_refs == ["not-json"]
