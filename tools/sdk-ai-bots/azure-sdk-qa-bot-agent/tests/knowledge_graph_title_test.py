"""Critical-path tests for GraphRAG reference-title derivation."""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.knowledge_graph import (  # noqa: E402
    _collect_community_reports,
    _extract_chunk_header_path,
    _source_path_to_rel_title,
)


def test_nested_header_path():
    text = (
        "# API Versioning\n"
        "intro\n"
        "## Adding a Stable Version\n"
        "details\n"
        "### Step 1\n"
        "do this\n"
    )
    assert (
        _extract_chunk_header_path(text)
        == "API Versioning | Adding a Stable Version | Step 1"
    )


def test_source_path_to_rel_title_strips_folder():
    assert (
        _source_path_to_rel_title("typespec_docs/sub#file.md", "typespec_docs")
        == "sub#file.md"
    )


def test_collect_community_reports_extracts_caps_and_ignores_text_units():
    import pandas as pd

    reports = pd.DataFrame(
        [
            {"id": "1", "title": "Community A", "content": "summary a"},
            {"id": "2", "title": "Community B", "content": "summary b"},
            {"id": "3", "title": "Community C", "content": "summary c"},
        ]
    )
    # text-unit "sources" table has a `text` column -> must be ignored.
    text_units = pd.DataFrame([{"id": "t1", "text": "verbatim chunk"}])
    ctx = {"reports": reports, "sources": text_units}

    out = _collect_community_reports(ctx, limit=2)

    assert out == [("Community A", "summary a"), ("Community B", "summary b")]
