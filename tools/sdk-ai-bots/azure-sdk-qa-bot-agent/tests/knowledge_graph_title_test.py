"""Critical-path tests for GraphRAG reference-title derivation."""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.knowledge_graph import (  # noqa: E402
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
