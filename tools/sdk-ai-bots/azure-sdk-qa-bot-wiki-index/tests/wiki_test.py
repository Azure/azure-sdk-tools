"""Offline unit tests for the wiki pipeline helpers.

No Azure/LLM connectivity required.

    python -m pytest tests/wiki_test.py
"""

from __future__ import annotations

import asyncio

from wiki_index.pages import PAGE_ENTITY, PAGE_SUMMARY, WikiPage
from wiki_index.reader import (
    read_blob_container,
    rel_title,
    source_folder,
)
from wiki_index.storage import _ascii, index_title
from wiki_index.wiki import doc_title


class _FakeBlobItem:
    def __init__(self, name, metadata):
        self.name = name
        self.metadata = metadata


class _FakeContainer:
    """Stands in for a blob container; mirrors the soft-delete metadata contract."""

    def __init__(self, blobs):
        self._blobs = blobs

    def list_blobs(self, include=None):
        blobs = self._blobs
        want_metadata = bool(include) and "metadata" in include

        class _Iter:
            def __aiter__(self_inner):
                self_inner._it = iter(blobs)
                return self_inner

            async def __anext__(self_inner):
                try:
                    name, metadata, _ = next(self_inner._it)
                except StopIteration:
                    raise StopAsyncIteration
                return _FakeBlobItem(name, metadata if want_metadata else None)

        return _Iter()

    async def download_blob(self, name):
        body = next(text for n, _, text in self._blobs if n == name)

        class _DL:
            async def readall(self_inner):
                return body.encode("utf-8")

        return _DL()


def test_read_blob_container_skips_tombstoned_non_markdown_and_empty():
    cc = _FakeContainer(
        [
            ("typespec_docs/a.md", {}, "text a"),
            ("typespec_docs/b.md", {"IsDeleted": "true"}, "text b"),
            ("typespec_docs/c.mdx", {"IsDeleted": "false"}, "text c"),
            ("typespec_docs/d.png", {}, "binary"),
            ("typespec_docs/e.md", {}, ""),
            ("typespec_docs/f.mdx", {}, " \n\t"),
        ]
    )
    assert asyncio.run(read_blob_container(cc)) == [
        ("typespec_docs/a.md", "text a"),
        ("typespec_docs/c.mdx", "text c"),
    ]


def test_source_folder_and_rel_title():
    sp = "typespec_docs/getting-started#basics#06-versioning.mdx"
    assert source_folder(sp) == "typespec_docs"
    assert rel_title(sp) == "getting-started#basics#06-versioning.mdx"
    assert source_folder("readme.md") == ""
    assert rel_title("readme.md") == "readme.md"


def test_doc_title():
    assert doc_title("getting-started#basics#06-versioning.mdx") == "06-versioning"
    assert doc_title("readme.md") == "readme"


def test_blob_metadata_helpers():
    assert _ascii("caf\u00e9 @added") == "caf @added"

    summary = WikiPage(
        "summary/x",
        PAGE_SUMMARY,
        "Foo (knowledge)",
        "body",
        "typespec_docs",
        source_refs=["a#b.md"],
        orig_title="a#b.md",
    )
    entity = WikiPage("entity/added", PAGE_ENTITY, "@added", "body", "wiki_entity")

    assert index_title(summary) == "a#b.md"
    assert index_title(entity) == "@added"
