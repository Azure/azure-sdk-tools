"""Offline unit tests for the wiki MapReduce (deterministic parts only).

    python -m pytest tests/reduce_test.py
"""

from __future__ import annotations

from azure_sdk_qa_bot_wiki_index.pages import make_slug, slugify
from azure_sdk_qa_bot_wiki_index.wiki_extract import DocExtraction, ExtractedItem
from azure_sdk_qa_bot_wiki_index.wiki_reduce import (
    _build_index_page,
    _concept_key,
    _entity_key,
    _inject_cross_links,
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
    _inject_cross_links(pages)
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
    idx = _build_index_page(pages)
    assert idx is not None
    assert idx.page_type == "index"
    assert "@added" in idx.content
    assert "API versioning" in idx.content


def test_index_page_none_when_no_cross_pages():
    assert _build_index_page([]) is None
