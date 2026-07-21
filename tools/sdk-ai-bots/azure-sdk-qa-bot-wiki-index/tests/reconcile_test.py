"""Offline unit tests for the incremental reconcile (fake blob container + LLM)."""

from __future__ import annotations

import asyncio
import json

from azure_sdk_qa_bot_wiki_index.reconcile import (
    _extraction_from_json,
    _extraction_to_json,
    _page_from_manifest,
    reconcile,
)
from azure_sdk_qa_bot_wiki_index.wiki_extract import DocExtraction, ExtractedItem


# --------------------------------------------------------------------------- #
# fakes
# --------------------------------------------------------------------------- #
class _FakeBlob:
    def __init__(self, store, path):
        self._store = store
        self._path = path

    async def exists(self):
        return self._path in self._store

    async def upload_blob(self, data, overwrite=True, metadata=None, content_type=None):
        self._store[self._path] = {"data": bytes(data), "metadata": dict(metadata or {})}

    async def download_blob(self):
        blob = self

        class _DL:
            async def readall(self_inner):
                return blob._store[blob._path]["data"]

        return _DL()

    async def get_blob_properties(self):
        class _P:
            metadata = self._store[self._path]["metadata"]

        return _P()

    async def set_blob_metadata(self, metadata):
        self._store[self._path]["metadata"] = dict(metadata)


class _FakeContainer:
    def __init__(self):
        self.store: dict[str, dict] = {}

    def get_blob_client(self, path):
        return _FakeBlob(self.store, path)


class _FakeLLM:
    """Deterministic stand-in: summary echoes title; extraction/synth are canned."""

    def __init__(self, extraction):
        self._extraction = extraction

    def complete(self, system, user, max_tokens=600):
        return f"BODY for {user.splitlines()[0]}"

    def complete_json(self, system, user, max_tokens=900):
        return self._extraction


def _corpus():
    return [
        ("typespec_docs/a.md", "text a @added versioning"),
        ("typespec_docs/b.md", "text b @added versioning"),
    ]


_EXTRACTION = {
    "entities": [{"name": "@added", "type": "decorator", "description": "adds a member"}],
    "concepts": [{"name": "versioning", "description": "api versioning"}],
}


def test_extraction_roundtrip():
    ext = DocExtraction("a.md", [ExtractedItem("entity", "@added", "decorator", "d", "a.md")],
                        [ExtractedItem("concept", "versioning", "", "d2", "a.md")])
    j = _extraction_to_json(ext)
    back = _extraction_from_json("a.md", j)
    assert back.entities[0].name == "@added"
    assert back.concepts[0].name == "versioning"
    assert back.entities[0].source_ref == "a.md"


def test_page_from_manifest():
    entry = {"slug": "entity/added", "page_type": "entity", "title": "@added",
             "content": "b", "context_id": "wiki_entity", "source_refs": ["a.md"],
             "out_links": ["concept/v"], "orig_title": ""}
    p = _page_from_manifest(entry)
    assert p.slug == "entity/added" and p.source_refs == ["a.md"]


def test_first_run_full_build_then_noop():
    cc = _FakeContainer()
    llm = _FakeLLM(_EXTRACTION)
    corpus = _corpus()

    s1 = asyncio.run(reconcile(cc, corpus, llm, min_docs=2))
    # 2 summaries + 1 entity + 1 concept + 1 index
    assert s1.total_pages == 5
    assert s1.pages_written == 5
    assert s1.summaries_regenerated == 2
    assert s1.groups_synthesized == 2
    # manifest persisted
    assert "_manifest.json" in cc.store
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    assert len(man["sources"]) == 2

    # second run, no source change → nothing rewritten, no LLM regen
    s2 = asyncio.run(reconcile(cc, corpus, llm, min_docs=2))
    assert s2.total_pages == 5
    assert s2.pages_written == 0
    assert s2.summaries_regenerated == 0
    assert s2.groups_synthesized == 0
    assert s2.pages_deleted == 0


def test_doc_delete_soft_deletes_summary_and_shrinks_groups():
    cc = _FakeContainer()
    llm = _FakeLLM(_EXTRACTION)
    asyncio.run(reconcile(cc, _corpus(), llm, min_docs=2))

    # remove doc b → entity/concept now have only 1 source (< min_docs) → gone;
    # b's summary soft-deleted
    s = asyncio.run(reconcile(cc, [("typespec_docs/a.md", "text a @added versioning")], llm, min_docs=2))
    assert s.deleted_docs == 1
    # summary/b + entity + concept soft-deleted (index may drop too)
    assert s.pages_deleted >= 3
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    # only doc a remains as a source
    assert list(man["sources"].keys()) == ["a.md"]


def test_doc_change_regenerates_summary_only():
    cc = _FakeContainer()
    llm = _FakeLLM(_EXTRACTION)
    corpus = _corpus()
    asyncio.run(reconcile(cc, corpus, llm, min_docs=2))

    # change doc a's content (same entities) → its summary regenerates; groups
    # unchanged (same source set) → no group synth
    changed = [("typespec_docs/a.md", "text a CHANGED @added versioning"),
               ("typespec_docs/b.md", "text b @added versioning")]
    s = asyncio.run(reconcile(cc, changed, llm, min_docs=2))
    assert s.changed_docs == 1
    assert s.summaries_regenerated == 1
    assert s.groups_synthesized == 0
