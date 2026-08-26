"""Offline unit tests for the incremental reconcile (fake blob container + LLM)."""

from __future__ import annotations

import asyncio
import json

from wiki_index.reconcile import (
    _extraction_from_json,
    _extraction_to_json,
    reconcile,
)
from wiki_index.wiki_extract import DocExtraction, ExtractedItem


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

    def __init__(self, extraction, deployment="gpt-5.6-sol"):
        self._extraction = extraction
        self.deployment = deployment

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
    ext = DocExtraction(
        "a.md",
        [
            ExtractedItem(
                name="@added",
                type="decorator",
                description="d",
                source_ref="a.md",
            )
        ],
        [
            ExtractedItem(
                name="versioning",
                description="d2",
                source_ref="a.md",
            )
        ],
    )
    j = _extraction_to_json(ext)
    back = _extraction_from_json("a.md", j)
    assert back.entities[0].name == "@added"
    assert back.concepts[0].name == "versioning"
    assert back.entities[0].source_ref == "a.md"


def test_first_run_full_build_then_noop():
    cc = _FakeContainer()
    llm = _FakeLLM(_EXTRACTION)
    corpus = _corpus()

    s1 = asyncio.run(reconcile(cc, corpus, llm, min_docs=2))
    # 2 summaries + 1 entity + 1 concept
    assert s1.total_pages == 4
    assert s1.pages_written == 4
    assert s1.summaries_regenerated == 2
    assert s1.groups_synthesized == 2
    # manifest persisted
    assert "_manifest.json" in cc.store
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    assert len(man["sources"]) == 2

    # second run, no source change → nothing rewritten, no LLM regen
    s2 = asyncio.run(reconcile(cc, corpus, llm, min_docs=2))
    assert s2.total_pages == 4
    assert s2.pages_written == 0
    assert s2.summaries_regenerated == 0
    assert s2.groups_synthesized == 0
    assert s2.pages_deleted == 0


def test_model_change_invalidates_cached_generation():
    cc = _FakeContainer()
    corpus = _corpus()
    asyncio.run(reconcile(cc, corpus, _FakeLLM(_EXTRACTION, "old-model"), min_docs=2))

    s = asyncio.run(
        reconcile(cc, corpus, _FakeLLM(_EXTRACTION, "gpt-5.6-sol"), min_docs=2)
    )

    assert s.changed_docs == 2
    assert s.summaries_regenerated == 2
    assert s.groups_synthesized == 2
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    assert man["build"]["model"] == "gpt-5.6-sol"


def test_doc_delete_soft_deletes_summary_and_shrinks_groups():
    cc = _FakeContainer()
    llm = _FakeLLM(_EXTRACTION)
    asyncio.run(reconcile(cc, _corpus(), llm, min_docs=2))

    # remove doc b → concept 'versioning' drops below min_docs and is soft-deleted
    # with b's summary; the decorator entity '@added' is kept (symbol singleton)
    s = asyncio.run(reconcile(cc, [("typespec_docs/a.md", "text a @added versioning")], llm, min_docs=2))
    assert s.deleted_docs == 1
    assert s.pages_deleted >= 2
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    # only doc a remains as a source
    assert list(man["sources"].keys()) == ["typespec_docs/a.md"]
    # removed pages are tombstoned (kept in the manifest, flagged is_deleted)
    assert any(e.get("is_deleted") == "true" for e in man["pages"].values())
    # the decorator entity page survives even with a single source
    assert any(
        e["page_type"] == "entity" and e.get("is_deleted") != "true"
        for e in man["pages"].values()
    )


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


def test_same_name_docs_across_folders_do_not_collide():
    cc = _FakeContainer()
    llm = _FakeLLM(_EXTRACTION)
    corpus = [
        ("typespec_docs/README.md", "text one @added versioning"),
        ("python_docs/README.md", "text two @added versioning"),
    ]
    asyncio.run(reconcile(cc, corpus, llm, min_docs=2))
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    assert set(man["sources"].keys()) == {"typespec_docs/README.md", "python_docs/README.md"}
    summary_refs = sorted(
        e["source_refs"][0] for e in man["pages"].values() if e["page_type"] == "summary"
    )
    assert summary_refs == ["python_docs/README.md", "typespec_docs/README.md"]


class _EmptySummaryLLM(_FakeLLM):
    """Returns an empty summary body while group synthesis still works."""

    def complete(self, system, user, max_tokens=600):
        if user.startswith("Document: "):
            return ""
        return super().complete(system, user, max_tokens=max_tokens)


def test_empty_summary_is_retried_on_the_next_run():
    cc = _FakeContainer()
    corpus = _corpus()

    # both attempts inside synthesize_summary come back empty
    s1 = asyncio.run(reconcile(cc, corpus, _EmptySummaryLLM(_EXTRACTION), min_docs=2))
    assert s1.summaries_regenerated == 0
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    # the source hash is reset so the doc is not mistaken for up to date
    assert all(e["hash"] == "" for e in man["sources"].values())

    # a healthy run over the same corpus regenerates the summaries
    s2 = asyncio.run(reconcile(cc, corpus, _FakeLLM(_EXTRACTION), min_docs=2))
    assert s2.summaries_regenerated == 2


class _InvalidExtractionLLM(_FakeLLM):
    def complete_json(self, system, user, max_tokens=900):
        return None


def test_invalid_extraction_is_retried_on_the_next_run():
    cc = _FakeContainer()
    corpus = _corpus()

    asyncio.run(reconcile(cc, corpus, _InvalidExtractionLLM(_EXTRACTION), min_docs=2))
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    assert all(e["hash"] == "" for e in man["sources"].values())

    s = asyncio.run(reconcile(cc, corpus, _FakeLLM(_EXTRACTION), min_docs=2))
    assert s.changed_docs == 2
    assert s.groups_synthesized == 2


class _GroupFailureLLM(_FakeLLM):
    def complete(self, system, user, max_tokens=600):
        if user.startswith("Name: "):
            return ""
        return super().complete(system, user, max_tokens=max_tokens)


def test_group_failure_preserves_prior_page_and_retries():
    cc = _FakeContainer()
    corpus = _corpus()
    asyncio.run(reconcile(cc, corpus, _FakeLLM(_EXTRACTION), min_docs=2))
    changed_extraction = {
        "entities": [
            {
                "name": "@added",
                "type": "decorator",
                "description": "adds a versioned member",
            }
        ],
        "concepts": [
            {
                "name": "versioning",
                "description": "api versioning rules and exceptions",
            }
        ],
    }
    changed_corpus = [(path, text + " changed") for path, text in corpus]

    failed = asyncio.run(
        reconcile(
            cc,
            changed_corpus,
            _GroupFailureLLM(changed_extraction),
            min_docs=2,
        )
    )
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    active_groups = [
        page
        for page in man["pages"].values()
        if page["page_type"] in ("entity", "concept")
        and page.get("is_deleted") != "true"
    ]
    assert failed.pages_deleted == 0
    assert len(active_groups) == 2
    assert all(page["content"] for page in active_groups)
    assert all(page["input_hash"] == "" for page in active_groups)

    retried = asyncio.run(
        reconcile(cc, changed_corpus, _FakeLLM(changed_extraction), min_docs=2)
    )
    assert retried.groups_synthesized == 2


def test_tombstoned_pages_drop_their_body():
    cc = _FakeContainer()
    llm = _FakeLLM(_EXTRACTION)
    asyncio.run(reconcile(cc, _corpus(), llm, min_docs=2))

    asyncio.run(reconcile(cc, [("typespec_docs/a.md", "text a @added versioning")], llm, min_docs=2))
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    tombstones = [e for e in man["pages"].values() if e.get("is_deleted") == "true"]
    assert tombstones
    assert all(not e.get("content") for e in tombstones)


def test_deleted_page_is_resynthesized_when_its_sources_return():
    cc = _FakeContainer()
    llm = _FakeLLM(_EXTRACTION)
    asyncio.run(reconcile(cc, _corpus(), llm, min_docs=2))
    asyncio.run(reconcile(cc, [("typespec_docs/a.md", "text a @added versioning")], llm, min_docs=2))

    s = asyncio.run(reconcile(cc, _corpus(), llm, min_docs=2))
    man = json.loads(cc.store["_manifest.json"]["data"].decode("utf-8"))
    assert s.total_pages == 4
    assert all(e.get("is_deleted") != "true" for e in man["pages"].values())
    assert all(e.get("content") for e in man["pages"].values())
