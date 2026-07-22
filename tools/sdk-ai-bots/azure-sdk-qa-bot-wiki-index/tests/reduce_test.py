"""Offline unit tests for the wiki MapReduce (deterministic parts only).

    python -m pytest tests/reduce_test.py
"""

from __future__ import annotations

from azure_sdk_qa_bot_wiki_index.pages import make_slug, slugify
from azure_sdk_qa_bot_wiki_index.wiki_extract import DocExtraction, ExtractedItem
from azure_sdk_qa_bot_wiki_index.wiki_reduce import (
    aggregate_groups,
    build_index_page,
    _concept_key,
    _entity_key,
    inject_cross_links,
)
from azure_sdk_qa_bot_wiki_index.pages import PAGE_CONCEPT, PAGE_ENTITY, WikiPage


def test_slugify_stable_and_distinct():
    a = slugify("@added")
    b = slugify("@added")
    c = slugify("@removed")
    assert a == b and a != c
    assert make_slug("entity", "@added").startswith("entity/")


def test_grouping_keys_normalize():
    assert _entity_key("@Added") == _entity_key("@added")
    assert _concept_key("API Versioning!") == _concept_key("api versioning")


def _page(slug, ptype, title, refs):
    return WikiPage(slug=slug, page_type=ptype, title=title, content="x",
                    context_id="wiki_entity", source_refs=refs)


def test_cross_links_by_shared_docs():
    pages = [
        _page("entity/added", PAGE_ENTITY, "@added", ["d1", "d2"]),
        _page("entity/removed", PAGE_ENTITY, "@removed", ["d1", "d3"]),
        _page("concept/versioning", PAGE_CONCEPT, "versioning", ["d1"]),
        _page("entity/route", PAGE_ENTITY, "@route", ["d9"]),
    ]
    inject_cross_links(pages)
    added = next(p for p in pages if p.slug == "entity/added")
    # shares d1 with removed and versioning
    assert "entity/removed" in added.out_links
    assert "concept/versioning" in added.out_links
    # @route shares no docs → no links
    route = next(p for p in pages if p.slug == "entity/route")
    assert route.out_links == []


def test_index_page_lists_entities_and_concepts():
    pages = [
        _page("entity/added", PAGE_ENTITY, "@added", ["d1"]),
        _page("concept/versioning", PAGE_CONCEPT, "API versioning", ["d1"]),
    ]
    idx = build_index_page(pages)
    assert idx is not None
    assert idx.page_type == "index"
    assert "@added" in idx.content
    assert "API versioning" in idx.content


def test_index_page_none_when_no_cross_pages():
    assert build_index_page([]) is None


def _ext(ref, entities=(), concepts=()):
    d = DocExtraction(ref)
    for name, aliases in entities:
        d.entities.append(ExtractedItem("entity", name, "type", "desc of " + name, ref, list(aliases)))
    for name, aliases in concepts:
        d.concepts.append(ExtractedItem("concept", name, "", "desc of " + name, ref, list(aliases)))
    return d


def test_alias_merges_synonyms_across_docs():
    # doc1 calls it "pagination", doc2 calls it "paging" but lists it as an alias
    exts = [
        _ext("d1", concepts=[("pagination", ["paging"])]),
        _ext("d2", concepts=[("paging", ["pagination"])]),
    ]
    groups = aggregate_groups(exts, min_docs=2, fuzzy=False)
    concept_groups = [g for g in groups if g.page_type == PAGE_CONCEPT]
    # the two surface forms collapse into ONE group spanning both docs
    assert len(concept_groups) == 1
    assert set(concept_groups[0].source_refs) == {"d1", "d2"}


def test_entity_at_sign_and_alias_merge():
    exts = [
        _ext("d1", entities=[("@added", [])]),
        _ext("d2", entities=[("added", ["@added"])]),
    ]
    groups = aggregate_groups(exts, min_docs=2, fuzzy=False)
    ent = [g for g in groups if g.page_type == PAGE_ENTITY]
    assert len(ent) == 1 and set(ent[0].source_refs) == {"d1", "d2"}


def test_min_docs_filters_single_doc_items():
    exts = [_ext("d1", concepts=[("versioning", [])])]
    assert aggregate_groups(exts, min_docs=2) == []
    assert len(aggregate_groups(exts, min_docs=1)) == 1


def test_fuzzy_merges_near_identical_concepts():
    # distinct surface forms, no shared alias, but near-identical keys
    exts = [
        _ext("d1", concepts=[("api versioning", [])]),
        _ext("d2", concepts=[("api versionings", [])]),
        _ext("d3", concepts=[("api versioning", [])]),
    ]
    no_fuzzy = aggregate_groups(exts, min_docs=1, fuzzy=False)
    with_fuzzy = aggregate_groups(exts, min_docs=1, fuzzy=True)
    assert len([g for g in with_fuzzy if g.page_type == PAGE_CONCEPT]) < len(
        [g for g in no_fuzzy if g.page_type == PAGE_CONCEPT]
    )
