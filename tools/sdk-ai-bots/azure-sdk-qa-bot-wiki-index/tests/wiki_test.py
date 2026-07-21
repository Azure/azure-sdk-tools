"""Offline unit tests for the wiki pipeline (Phase 0: per-doc summary pages).

No Azure/LLM connectivity required.

    python -m pytest tests/wiki_test.py
"""

from __future__ import annotations

from azure_sdk_qa_bot_wiki_index.documents import make_summary_doc
from azure_sdk_qa_bot_wiki_index.reader import rel_title, source_folder
from azure_sdk_qa_bot_wiki_index.wiki import _doc_title


def test_source_folder_and_rel_title():
    sp = "typespec_docs/getting-started#basics#06-versioning.mdx"
    assert source_folder(sp) == "typespec_docs"
    assert rel_title(sp) == "getting-started#basics#06-versioning.mdx"
    assert source_folder("readme.md") == ""
    assert rel_title("readme.md") == "readme.md"


def test_doc_title():
    assert _doc_title("getting-started#basics#06-versioning.mdx") == "06-versioning"
    assert _doc_title("readme.md") == "readme"


def test_summary_doc_mapping():
    d = make_summary_doc(
        "typespec_docs", "getting-started#06-versioning.mdx", "06-versioning", "body"
    ).to_index_doc()
    assert d["chunk_id"].startswith("wiki-summary-")
    assert not d["chunk_id"].startswith("_")
    assert d["title"] == "getting-started#06-versioning.mdx"
    assert d["header_1"] == "06-versioning (knowledge)"
    assert d["context_id"] == "typespec_docs"
    assert d["page_type"] == "summary"
    assert d["chunk_refs"] == ["getting-started#06-versioning.mdx"]
    assert d["parent_id"] == "" and d["header_2"] == "" and d["header_3"] == ""
    assert "text_vector" not in d


def test_summary_key_is_stable():
    a = make_summary_doc("f", "x#y.md", "y", "body one")
    b = make_summary_doc("f", "x#y.md", "y", "different body")
    assert a.chunk_id == b.chunk_id
