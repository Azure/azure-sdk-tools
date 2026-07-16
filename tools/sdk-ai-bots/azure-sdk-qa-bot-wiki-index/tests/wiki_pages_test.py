"""Offline unit tests for wiki page discovery + index document mapping.

No Azure connectivity required (pure logic). Run with::

    python -m pytest tests/wiki_pages_test.py
"""

from __future__ import annotations

from azure_sdk_qa_bot_wiki_index.documents import (
    WIKI_CONCEPT_CONTEXT,
    WIKI_ENTITY_CONTEXT,
    make_concept_doc,
    make_entity_doc,
    make_summary_doc,
)
from azure_sdk_qa_bot_wiki_index.extraction import discover_concepts, discover_entities
from azure_sdk_qa_bot_wiki_index.reader import rel_title, source_folder


def _corpus():
    docs = []
    for i in range(6):
        docs.append(
            (
                f"typespec_docs/doc{i}.md",
                "# Versioning\nUse @added and @removed for api version changes.\n"
                "## Paging\n@pageable enables pagination with nextLink.\n"
                "@added(Versions.v2) marks a member added.",
            )
        )
    return docs


def test_source_folder_and_rel_title():
    sp = "typespec_docs/getting-started#basics#06-versioning.mdx"
    assert source_folder(sp) == "typespec_docs"
    assert rel_title(sp) == "getting-started#basics#06-versioning.mdx"
    assert source_folder("readme.md") == ""
    assert rel_title("readme.md") == "readme.md"


def test_discover_entities_frequency_threshold():
    ents = discover_entities(_corpus(), min_docs=4)
    assert "@added" in ents
    assert "@removed" in ents
    assert "@pageable" in ents
    # excerpts attached
    assert all(ents[k] for k in ents)


def test_discover_concepts_seed_coverage():
    cons = discover_concepts(_corpus(), min_docs=3)
    assert "API versioning" in cons
    assert "Pagination" in cons


def test_summary_doc_field_mapping():
    d = make_summary_doc(
        "typespec_docs", "getting-started#06-versioning.mdx", "06-versioning", "Card body"
    ).to_index_doc()
    assert d["chunk_id"].startswith("wiki-summary-")
    assert not d["chunk_id"].startswith("_")
    assert d["title"] == "getting-started#06-versioning.mdx"
    assert d["header_1"] == "06-versioning (knowledge)"
    assert d["context_id"] == "typespec_docs"
    assert d["chunk_refs"] == ["getting-started#06-versioning.mdx"]
    assert d["page_type"] == "summary"
    assert d["parent_id"] == "" and d["header_2"] == "" and d["header_3"] == ""
    assert "text_vector" not in d  # only present once embedded


def test_entity_and_concept_doc_scope():
    ed = make_entity_doc("@added", "body", ["typespec_docs/doc0.md"], ["concept:API versioning"]).to_index_doc()
    assert ed["context_id"] == WIKI_ENTITY_CONTEXT
    assert ed["page_type"] == "entity"
    assert ed["title"] == "entity:@added"
    assert ed["header_1"] == "@added (knowledge)"
    assert ed["related_slugs"] == ["concept:API versioning"]

    cd = make_concept_doc("API versioning", "body", [], []).to_index_doc()
    assert cd["context_id"] == WIKI_CONCEPT_CONTEXT
    assert cd["page_type"] == "concept"
    assert cd["title"] == "concept:API versioning"


def test_summary_key_is_stable():
    a = make_summary_doc("typespec_docs", "x#y.md", "y", "body one")
    b = make_summary_doc("typespec_docs", "x#y.md", "y", "different body")
    assert a.chunk_id == b.chunk_id
