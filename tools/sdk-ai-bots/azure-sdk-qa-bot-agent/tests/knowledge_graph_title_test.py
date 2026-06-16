"""Unit tests for GraphRAG reference-title derivation.

Covers ``_extract_chunk_header_path``, the helper that recovers a
section-level ``h1 | h2 | h3`` title from a text-unit chunk so graph
references read like the KB tool's header-based titles instead of a bare
file path.
"""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.knowledge_graph import (  # noqa: E402
    _extract_chunk_header_path,
    _strip_source_prefix,
)


def test_single_header():
    text = "# Versioning\n\nSome body text about versioning."
    assert _extract_chunk_header_path(text) == "Versioning"


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


def test_depth_capped_at_three():
    text = (
        "# H1\n## H2\n### H3\n#### H4\n##### H5\n"
    )
    # H4/H5 exceed _MAX_HEADER_DEPTH and are ignored.
    assert _extract_chunk_header_path(text) == "H1 | H2 | H3"


def test_deeper_sibling_cleared_by_shallower_heading():
    # A later shallow heading must drop a stale deeper sibling so the
    # path reflects the *current* section, not a mix of old + new.
    text = (
        "# Guide\n"
        "## Section A\n"
        "### Detail A1\n"
        "## Section B\n"
        "body of B\n"
    )
    assert _extract_chunk_header_path(text) == "Guide | Section B"


def test_headers_inside_code_fence_ignored():
    text = (
        "# Real Header\n"
        "```bash\n"
        "# this is a shell comment, not a header\n"
        "## neither is this\n"
        "```\n"
        "body\n"
    )
    assert _extract_chunk_header_path(text) == "Real Header"


def test_tilde_code_fence_ignored():
    text = (
        "# Real Header\n"
        "~~~\n"
        "# fake header in tilde fence\n"
        "~~~\n"
    )
    assert _extract_chunk_header_path(text) == "Real Header"


def test_closed_atx_header_trailing_hashes_stripped():
    text = "## Heading With Closing Hashes ##\n\nbody\n"
    assert _extract_chunk_header_path(text) == "Heading With Closing Hashes"


def test_no_header_returns_empty():
    text = "Just a paragraph that starts mid-section with no heading.\n"
    assert _extract_chunk_header_path(text) == ""


def test_empty_text_returns_empty():
    assert _extract_chunk_header_path("") == ""
    assert _extract_chunk_header_path(None) == ""  # type: ignore[arg-type]


def test_hash_without_space_is_not_header():
    # ``#tag`` (no space) is not a valid ATX header.
    text = "#nothing\n\n# Real\n"
    assert _extract_chunk_header_path(text) == "Real"


def test_strip_source_prefix_removes_folder():
    # Full encoded title -> source-relative encoded title.
    assert (
        _strip_source_prefix("typespec_docs#sub#file.md", "typespec_docs")
        == "sub#file.md"
    )


def test_strip_source_prefix_root_level_file():
    # A file directly under the source folder (no further nesting).
    assert (
        _strip_source_prefix("python_docs#README.md", "python_docs") == "README.md"
    )


def test_strip_source_prefix_empty_folder_is_noop():
    assert _strip_source_prefix("file.md", "") == "file.md"


def test_strip_source_prefix_graphrag_fallback_is_noop():
    # Unattributed rows use the synthetic "graphrag" source name, which
    # is never a real title prefix -> no-op.
    assert (
        _strip_source_prefix("typespec_docs#sub#file.md", "graphrag")
        == "typespec_docs#sub#file.md"
    )


def test_strip_source_prefix_no_match_is_noop():
    assert (
        _strip_source_prefix("other_docs#file.md", "typespec_docs")
        == "other_docs#file.md"
    )


def test_strip_source_prefix_empty_title():
    assert _strip_source_prefix("", "typespec_docs") == ""


def test_strip_source_prefix_does_not_strip_partial_match():
    # ``typespec`` must not strip from ``typespec_docs#...`` — the prefix
    # match requires the exact ``{folder}#`` boundary.
    assert (
        _strip_source_prefix("typespec_docs#file.md", "typespec")
        == "typespec_docs#file.md"
    )
