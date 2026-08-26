"""Offline tests for excluding generated Wiki pages from legacy search."""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.azure_ai_search import (
    NON_WIKI_FILTER,
    _build_hierarchy_filter,
    _combine_source_filters,
)


def test_source_filters_exclude_wiki_pages() -> None:
    result = _combine_source_filters(
        {
            "source-a": "context_id eq 'source-a'",
            "source-b": "context_id eq 'source-b'",
        }
    )

    assert result == (
        "((context_id eq 'source-a') or (context_id eq 'source-b')) "
        f"and {NON_WIKI_FILTER}"
    )


def test_empty_source_filters_still_exclude_wiki_pages() -> None:
    assert _combine_source_filters({}) == NON_WIKI_FILTER


def test_hierarchy_filter_excludes_wiki_pages() -> None:
    result = _build_hierarchy_filter(
        title="doc.md",
        context_id="source-a",
        header1="Overview",
        header2="",
        header3="",
    )

    assert NON_WIKI_FILTER in result
