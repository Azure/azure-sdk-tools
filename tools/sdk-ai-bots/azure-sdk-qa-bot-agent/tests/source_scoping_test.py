"""Offline unit tests for knowledge-source scoping (no Azure backend required)."""

from __future__ import annotations

import sys
from pathlib import Path

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from config.tenant_config import (
    SRC_TYPESPEC_DOCS,
    SRC_WIKI_CONCEPT,
    SRC_WIKI_ENTITY,
    TenantID,
)
from tools.knowledge_tools import (
    _raw_source_names,
    _resolve_source_filters,
    _wiki_source_names,
)

_TENANT = TenantID.TYPESPEC_CHANNEL_QA_BOT.value


def test_raw_tools_drop_wiki_only_sources():
    got = _raw_source_names(_TENANT, [SRC_TYPESPEC_DOCS, SRC_WIKI_ENTITY])
    assert got == [SRC_TYPESPEC_DOCS]


def test_raw_tools_keep_wiki_sources_rather_than_emptying_the_scope():
    # An empty list would resolve to no filter at all, i.e. the whole index.
    got = _raw_source_names(_TENANT, [SRC_WIKI_ENTITY, SRC_WIKI_CONCEPT])
    assert got == [SRC_WIKI_ENTITY, SRC_WIKI_CONCEPT]


def test_raw_tools_fall_back_to_tenant_sources_without_wiki():
    got = _raw_source_names(_TENANT, None)
    assert got
    assert SRC_WIKI_ENTITY not in got and SRC_WIKI_CONCEPT not in got


def test_wiki_search_keeps_wiki_sources_under_topical_scoping():
    got = _wiki_source_names(_TENANT, [SRC_TYPESPEC_DOCS])
    assert got[0] == SRC_TYPESPEC_DOCS
    assert SRC_WIKI_ENTITY in got and SRC_WIKI_CONCEPT in got


def test_wiki_search_does_not_duplicate_explicit_wiki_sources():
    got = _wiki_source_names(_TENANT, [SRC_WIKI_ENTITY])
    assert got.count(SRC_WIKI_ENTITY) == 1
    assert SRC_WIKI_CONCEPT in got


def test_source_filter_escapes_quotes_in_model_supplied_names():
    injected = "x' or context_id eq 'secret"
    got = _resolve_source_filters([injected], _TENANT)
    # The quote is doubled, so the whole value stays inside one string literal.
    assert got[injected] == "context_id eq 'x'' or context_id eq ''secret'"


def test_unknown_source_still_produces_a_filter():
    # Dropping it would leave the caller with an empty, i.e. unscoped, filter.
    got = _resolve_source_filters(["not_a_registered_source"], _TENANT)
    assert got == {"not_a_registered_source": "context_id eq 'not_a_registered_source'"}
