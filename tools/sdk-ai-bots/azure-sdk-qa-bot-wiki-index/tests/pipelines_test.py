"""Offline unit tests for the split wiki / graph pipelines.

No Azure or LLM connectivity required — covers the deterministic graph math
(PMI weights, degree, 1-hop/2-hop edges) and the index document mapping.

    python -m pytest tests/pipelines_test.py
"""

from __future__ import annotations

import math

from azure_sdk_qa_bot_wiki_index.documents import (
    WIKI_ENTITY_CONTEXT,
    WIKI_RELATIONSHIP_CONTEXT,
    make_entity_doc,
    make_relationship_doc,
    make_wiki_doc,
)
from azure_sdk_qa_bot_wiki_index.graph_extract import Entity, Relationship
from azure_sdk_qa_bot_wiki_index.graph_weights import (
    build_entity_edges,
    compute_degrees,
    compute_weights,
)
from azure_sdk_qa_bot_wiki_index.reader import rel_title, source_folder


# --------------------------------------------------------------------------- #
# reader
# --------------------------------------------------------------------------- #
def test_source_folder_and_rel_title():
    sp = "typespec_docs/getting-started#basics#06-versioning.mdx"
    assert source_folder(sp) == "typespec_docs"
    assert rel_title(sp) == "getting-started#basics#06-versioning.mdx"
    assert source_folder("readme.md") == ""


# --------------------------------------------------------------------------- #
# document mapping
# --------------------------------------------------------------------------- #
def test_wiki_doc_mapping():
    d = make_wiki_doc("typespec_docs", "getting-started#06-versioning.mdx", "06-versioning", "body").to_index_doc()
    assert d["chunk_id"].startswith("wiki-wiki-")
    assert not d["chunk_id"].startswith("_")
    assert d["title"] == "getting-started#06-versioning.mdx"
    assert d["context_id"] == "typespec_docs"
    assert d["page_type"] == "wiki"
    assert d["chunk_refs"] == ["getting-started#06-versioning.mdx"]
    assert "text_vector" not in d


def test_entity_doc_mapping():
    d = make_entity_doc(
        "@added", "Marks a member added in a version.", entity_type="decorator",
        source_refs=["b.md", "a.md"], related=["entity:@removed"],
    ).to_index_doc()
    assert d["chunk_id"].startswith("wiki-entity-")
    assert d["context_id"] == WIKI_ENTITY_CONTEXT
    assert d["page_type"] == "entity"
    assert d["title"] == "entity:@added"
    assert d["header_1"] == "@added (decorator)"
    assert d["chunk_refs"] == ["a.md", "b.md"]  # sorted
    assert d["related_slugs"] == ["entity:@removed"]


def test_relationship_doc_mapping():
    d = make_relationship_doc(
        "@added", "@removed", "Both drive versioning transitions.",
        strength=8, weight=7.5, source_refs=["a.md"],
    ).to_index_doc()
    assert d["chunk_id"].startswith("wiki-rel-")
    assert d["context_id"] == WIKI_RELATIONSHIP_CONTEXT
    assert d["page_type"] == "relationship"
    assert d["title"] == "rel:@added->@removed"
    assert d["related_slugs"] == ["entity:@added", "entity:@removed"]
    assert "strength 8/10" in d["chunk"]


def test_keys_are_stable():
    a = make_wiki_doc("f", "x#y.md", "y", "body one")
    b = make_wiki_doc("f", "x#y.md", "y", "different body")
    assert a.chunk_id == b.chunk_id


# --------------------------------------------------------------------------- #
# graph math (PMI, degree, edges)
# --------------------------------------------------------------------------- #
def _fixture():
    # 10 docs. Relationship chunk_ids are a subset of the two entities'
    # co-occurrence (the realistic invariant WeKnora's findRelationChunkIDs holds).
    entities = {
        "@added": Entity("@added", "decorator", "adds", chunk_ids={"d1", "d2", "d3", "d4", "d5"}),
        "@removed": Entity("@removed", "decorator", "removes", chunk_ids={"d1", "d2", "d3", "d4"}),
        "@route": Entity("@route", "decorator", "routes", chunk_ids={"d4", "d5"}),
    }
    relationships = {
        # strong: high co-occurrence (d1-d4) + high strength
        ("@added", "@removed"): Relationship("@added", "@removed", "versioning pair", 9, chunk_ids={"d1", "d2", "d3", "d4"}),
        # weaker: single co-occurrence (d4) + low strength
        ("@removed", "@route"): Relationship("@removed", "@route", "weak", 3, chunk_ids={"d4"}),
    }
    return entities, relationships


def test_compute_weights_in_range_and_pmi_orders():
    entities, relationships = _fixture()
    compute_weights(entities, relationships, total_chunks=4)
    for rel in relationships.values():
        assert 1.0 <= rel.weight <= 10.0
    # strongly co-occurring + high-strength pair outweighs the weak one
    assert relationships[("@added", "@removed")].weight > relationships[("@removed", "@route")].weight


def test_compute_degrees():
    entities, relationships = _fixture()
    compute_degrees(entities, relationships)
    # @removed participates in both relationships → degree 2
    assert entities["@removed"].degree == 2
    assert entities["@added"].degree == 1
    assert entities["@route"].degree == 1
    assert relationships[("@added", "@removed")].combined_degree == 1 + 2


def test_build_entity_edges_direct_and_indirect():
    entities, relationships = _fixture()
    compute_weights(entities, relationships, total_chunks=4)
    compute_degrees(entities, relationships)
    edges = build_entity_edges(entities, relationships, max_neighbors=6)
    # direct 1-hop
    assert "@removed" in edges["@added"]
    assert "@route" in edges["@removed"]
    # indirect 2-hop: @added -> @removed -> @route
    assert "@route" in edges["@added"]
    # self never appears
    assert "@added" not in edges["@added"]


def test_pmi_zero_when_independent():
    # x,y each in 2 of 4 docs, co-occur in exactly 1 → PMI = log2(0.25/0.25)=0
    ents = {
        "x": Entity("x", chunk_ids={"d1", "d2"}),
        "y": Entity("y", chunk_ids={"d2", "d3"}),
    }
    rels = {("x", "y"): Relationship("x", "y", "", 5, chunk_ids={"d2"})}
    compute_weights(ents, rels, total_chunks=4)
    # PMI term is 0; weight comes only from the strength term (normalised to 1) → 1 + 9*0.4
    assert math.isclose(rels[("x", "y")].weight, 1.0 + 9.0 * 0.4, rel_tol=1e-6)
