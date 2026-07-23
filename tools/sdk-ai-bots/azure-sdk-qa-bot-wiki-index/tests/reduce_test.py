"""Offline unit tests for the deterministic parts of the wiki reduce phase."""

from __future__ import annotations

from azure_sdk_qa_bot_wiki_index.pages import (
    PAGE_CONCEPT,
    PAGE_ENTITY,
    WikiPage,
    make_slug,
    slugify,
)
from azure_sdk_qa_bot_wiki_index.wiki_extract import DocExtraction, ExtractedItem
from azure_sdk_qa_bot_wiki_index.wiki_reduce import (
    _concept_key,
    _entity_key,
    aggregate_groups,
    build_index_page,
    inject_cross_links,
)


def _page(slug, ptype, title, refs):
    return WikiPage(slug=slug, page_type=ptype, title=title, content="x",
                    context_id="wiki_entity", source_refs=refs)


def _ext(ref, entities=(), concepts=()):
    d = DocExtraction(ref)
    for name, aliases in entities:
        d.entities.append(ExtractedItem("entity", name, "type", "desc of " + name, ref, list(aliases)))
    for name, aliases in concepts:
        d.concepts.append(ExtractedItem("concept", name, "", "desc of " + name, ref, list(aliases)))
    return d


def test_slug_and_key_normalization():
    assert slugify("@added") == slugify("@added") != slugify("@removed")
    assert make_slug("entity", "@added").startswith("entity/")
    assert _entity_key("@Added") == _entity_key("@added")
    assert _concept_key("API Versioning!") == _concept_key("api versioning")


def test_cross_links_by_shared_docs():
    pages = [
        _page("entity/added", PAGE_ENTITY, "@added", ["d1", "d2"]),
        _page("entity/removed", PAGE_ENTITY, "@removed", ["d1", "d3"]),
        _page("concept/versioning", PAGE_CONCEPT, "versioning", ["d1"]),
        _page("entity/route", PAGE_ENTITY, "@route", ["d9"]),
    ]
    inject_cross_links(pages)
    added = next(p for p in pages if p.slug == "entity/added")
    assert "entity/removed" in added.out_links
    assert "concept/versioning" in added.out_links
    # shares no docs → no links
    assert next(p for p in pages if p.slug == "entity/route").out_links == []


def test_build_index_page():
    pages = [
        _page("entity/added", PAGE_ENTITY, "@added", ["d1"]),
        _page("concept/versioning", PAGE_CONCEPT, "API versioning", ["d1"]),
    ]
    idx = build_index_page(pages)
    assert idx is not None and idx.page_type == "index"
    assert "@added" in idx.content and "API versioning" in idx.content
    assert build_index_page([]) is None


def test_alias_merges_surface_forms_across_docs():
    # concept synonyms declared as aliases collapse into one group
    concept_exts = [
        _ext("d1", concepts=[("pagination", ["paging"])]),
        _ext("d2", concepts=[("paging", ["pagination"])]),
    ]
    cgroups = [g for g in aggregate_groups(concept_exts, min_docs=2, fuzzy=False)
               if g.page_type == PAGE_CONCEPT]
    assert len(cgroups) == 1 and set(cgroups[0].source_refs) == {"d1", "d2"}
    # entity @-insensitivity via alias
    entity_exts = [
        _ext("d1", entities=[("@added", [])]),
        _ext("d2", entities=[("added", ["@added"])]),
    ]
    egroups = [g for g in aggregate_groups(entity_exts, min_docs=2, fuzzy=False)
               if g.page_type == PAGE_ENTITY]
    assert len(egroups) == 1 and set(egroups[0].source_refs) == {"d1", "d2"}


def test_min_docs_filters_single_doc_items():
    exts = [_ext("d1", concepts=[("versioning", [])])]
    assert aggregate_groups(exts, min_docs=2) == []
    assert len(aggregate_groups(exts, min_docs=1)) == 1


def test_fuzzy_merges_near_identical_concepts():
    exts = [
        _ext("d1", concepts=[("api versioning", [])]),
        _ext("d2", concepts=[("api versionings", [])]),
        _ext("d3", concepts=[("api versioning", [])]),
    ]
    no_fuzzy = [g for g in aggregate_groups(exts, min_docs=1, fuzzy=False) if g.page_type == PAGE_CONCEPT]
    with_fuzzy = [g for g in aggregate_groups(exts, min_docs=1, fuzzy=True) if g.page_type == PAGE_CONCEPT]
    assert len(with_fuzzy) < len(no_fuzzy)
