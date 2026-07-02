"""Critical-path tests for GraphRAG tenant filtering (folder + file level).

Covers the reverse-index build, the OData → terms translation, and the
per-query entity-id resolution that adds the file (source_path) filter
layer on top of the source-folder layer.
"""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import pandas as pd  # noqa: E402

from utils.knowledge_graph import parse_title_filter_terms  # noqa: E402
from utils.knowledge_graph.filtering import (  # noqa: E402
    build_entity_index,
    resolve_allowed_entity_ids,
)


def _make_dfs() -> dict[str, pd.DataFrame]:
    documents = pd.DataFrame(
        {
            "id": ["doc1", "doc2", "doc3"],
            "raw_data": [
                {"source_folder": "python_docs", "source_path": "python_docs/setup_python.md"},
                {"source_folder": "python_docs", "source_path": "python_docs/general_guide.md"},
                {"source_folder": "typespec_docs", "source_path": "typespec_docs/versioning.md"},
            ],
        }
    )
    text_units = pd.DataFrame(
        {"id": ["tu1", "tu2", "tu3"], "document_id": ["doc1", "doc2", "doc3"]}
    )
    entities = pd.DataFrame(
        {
            "id": ["e1", "e2", "e3", "e4"],
            "text_unit_ids": [["tu1"], ["tu2"], ["tu3"], ["tu1", "tu3"]],
        }
    )
    return {"documents": documents, "text_units": text_units, "entities": entities}


def test_parse_title_filter_terms_extracts_ismatch_literals():
    odata = "search.ismatch('typespec-python', 'title') or search.ismatch('generate*', 'title')"
    assert parse_title_filter_terms(odata) == ["typespec-python", "generate"]
    assert parse_title_filter_terms("") == []
    assert parse_title_filter_terms("context_id eq 'python_docs'") == []


def test_build_entity_index_maps_folders_to_entities_and_paths():
    index = build_entity_index(_make_dfs())
    assert set(index) == {"python_docs", "typespec_docs"}
    assert set(index["python_docs"]) == {"e1", "e2", "e4"}
    assert index["python_docs"]["e1"] == frozenset({"python_docs/setup_python.md"})
    # e4 spans both folders.
    assert "e4" in index["typespec_docs"]


def test_build_entity_index_empty_without_raw_data():
    dfs = _make_dfs()
    dfs["documents"] = dfs["documents"].drop(columns=["raw_data"])
    assert build_entity_index(dfs) == {}


def test_resolve_folder_level_only():
    index = build_entity_index(_make_dfs())
    ids = resolve_allowed_entity_ids(index, {"python_docs"}, None)
    assert ids == frozenset({"e1", "e2", "e4"})


def test_resolve_applies_file_level_filter():
    index = build_entity_index(_make_dfs())
    # Only documents whose source_path contains 'setup' survive → e1, e4.
    ids = resolve_allowed_entity_ids(
        index, {"python_docs"}, {"python_docs": ["setup"]}
    )
    assert ids == frozenset({"e1", "e4"})


def test_resolve_returns_none_for_unknown_folder():
    index = build_entity_index(_make_dfs())
    assert resolve_allowed_entity_ids(index, {"nonexistent"}, None) is None


def test_resolve_returns_none_without_folders():
    index = build_entity_index(_make_dfs())
    assert resolve_allowed_entity_ids(index, None, None) is None
    assert resolve_allowed_entity_ids(index, set(), None) is None
