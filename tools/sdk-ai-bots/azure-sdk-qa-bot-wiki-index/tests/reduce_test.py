"""Offline unit tests for the deterministic parts of the wiki reduce phase."""

from __future__ import annotations

from wiki_index.pages import (
    PAGE_CONCEPT,
    PAGE_ENTITY,
    make_slug,
    slugify,
)
from wiki_index.wiki_extract import DocExtraction, ExtractedItem
from wiki_index.wiki_reduce import (
    _concept_key,
    _entity_key,
    aggregate_groups,
)


def _ext(ref, entities=(), concepts=()):
    d = DocExtraction(ref)
    for name, aliases in entities:
        d.entities.append(
            ExtractedItem(
                name=name,
                type="type",
                description="desc of " + name,
                source_ref=ref,
                aliases=list(aliases),
            )
        )
    for name, aliases in concepts:
        d.concepts.append(
            ExtractedItem(
                name=name,
                description="desc of " + name,
                source_ref=ref,
                aliases=list(aliases),
            )
        )
    return d


def test_slug_and_key_normalization():
    assert slugify("@added") == slugify("@added") != slugify("@removed")
    assert make_slug("entity", "@added").startswith("entity/")
    assert _entity_key("@Added") == _entity_key("@added")
    assert _concept_key("API Versioning!") == _concept_key("api versioning")


def test_alias_merges_surface_forms_across_docs():
    # concept synonyms declared as aliases collapse into one group
    concept_exts = [
        _ext("d1", concepts=[("pagination", ["paging"])]),
        _ext("d2", concepts=[("paging", ["pagination"])]),
    ]
    cgroups = [
        g
        for g in aggregate_groups(concept_exts, min_docs=2, fuzzy=False)
        if g.page_type == PAGE_CONCEPT
    ]
    assert len(cgroups) == 1 and set(cgroups[0].source_refs) == {"d1", "d2"}
    # entity @-insensitivity via alias
    entity_exts = [
        _ext("d1", entities=[("@added", [])]),
        _ext("d2", entities=[("added", ["@added"])]),
    ]
    egroups = [
        g
        for g in aggregate_groups(entity_exts, min_docs=2, fuzzy=False)
        if g.page_type == PAGE_ENTITY
    ]
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
    no_fuzzy = [
        g
        for g in aggregate_groups(exts, min_docs=1, fuzzy=False)
        if g.page_type == PAGE_CONCEPT
    ]
    with_fuzzy = [
        g
        for g in aggregate_groups(exts, min_docs=1, fuzzy=True)
        if g.page_type == PAGE_CONCEPT
    ]
    assert len(with_fuzzy) < len(no_fuzzy)
