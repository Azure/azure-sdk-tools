"""Offline unit tests for blob storage rendering (no Azure connectivity)."""

from __future__ import annotations

from azure_sdk_qa_bot_wiki_index.pages import PAGE_ENTITY, PAGE_SUMMARY, WikiPage
from azure_sdk_qa_bot_wiki_index.storage import (
    _ascii,
    _index_title,
    content_hash,
    render_markdown,
)


def test_ascii_strips_non_ascii():
    assert _ascii("caf\u00e9 @added") == "caf @added"
    assert _ascii("") == ""


def test_content_hash_stable():
    assert content_hash("abc") == content_hash("abc")
    assert content_hash("abc") != content_hash("abd")


def test_index_title_summary_uses_orig():
    summ = WikiPage("summary/x", PAGE_SUMMARY, "Foo (knowledge)", "b", "typespec_docs",
                    source_refs=["a#b.md"], orig_title="a#b.md")
    assert _index_title(summ) == "a#b.md"
    ent = WikiPage("entity/added", PAGE_ENTITY, "@added", "b", "wiki_entity")
    assert _index_title(ent) == "@added"


def test_render_markdown_with_related():
    pages = {
        "entity/added": "@added",
        "concept/versioning": "API versioning",
    }
    p = WikiPage("entity/added", PAGE_ENTITY, "@added", "Marks a member added.",
                 "wiki_entity", out_links=["concept/versioning", "entity/missing"])
    md = render_markdown(p, pages)
    assert md.startswith("# @added")
    assert "Marks a member added." in md
    assert "## Related" in md
    assert "- API versioning" in md
    # dead link (missing title) is dropped
    assert "entity/missing" not in md


def test_render_markdown_no_related_section_when_empty():
    p = WikiPage("summary/x", PAGE_SUMMARY, "Foo", "body", "typespec_docs")
    md = render_markdown(p, {})
    assert "## Related" not in md
