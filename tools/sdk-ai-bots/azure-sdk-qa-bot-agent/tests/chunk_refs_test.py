"""Offline unit tests for wiki chunk_refs parsing (query-time backfill routing).

Wiki pages store the source docs they were built from as a JSON-array string in
the index field ``chunk_refs_str`` (Edm.String, because index projections cannot
populate a collection from a scalar). ``KnowledgeChunk`` parses that back into a
list so ``backfill_wiki_sources`` can route a retrieved wiki page to its sources.
"""

from __future__ import annotations

from models.knowledge import KnowledgeChunk


def test_entity_chunk_refs_parsed_from_json_string():
    c = KnowledgeChunk.model_validate(
        {
            "chunk_id": "e1",
            "title": "Pagination",
            "page_type": "entity",
            "chunk_refs_str": '["a/b.md", "c/d.md"]',
        }
    )
    assert c.chunk_refs == ["a/b.md", "c/d.md"]


def test_raw_chunk_has_empty_chunk_refs():
    c = KnowledgeChunk.model_validate({"chunk_id": "r1", "title": "raw"})
    assert c.chunk_refs == []


def test_null_chunk_refs_str_coerced_to_empty():
    c = KnowledgeChunk.model_validate(
        {"chunk_id": "s1", "title": "foo/bar.md", "page_type": "summary", "chunk_refs_str": None}
    )
    assert c.chunk_refs == []


def test_empty_json_array_string():
    c = KnowledgeChunk.model_validate(
        {"chunk_id": "i1", "title": "index", "page_type": "index", "chunk_refs_str": "[]"}
    )
    assert c.chunk_refs == []


def test_malformed_chunk_refs_str_falls_back_to_single_element():
    c = KnowledgeChunk.model_validate(
        {"chunk_id": "m1", "title": "t", "page_type": "entity", "chunk_refs_str": "not-json"}
    )
    assert c.chunk_refs == ["not-json"]


def test_populate_by_name_still_accepts_list():
    c = KnowledgeChunk(chunk_id="n1", chunk_refs=["p", "q"])
    assert c.chunk_refs == ["p", "q"]
