"""Offline unit tests for the wiki pipeline helpers.

No Azure/LLM connectivity required.

    python -m pytest tests/wiki_test.py
"""

from __future__ import annotations

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
