"""Offline unit tests for knowledge-source scoping (no Azure backend required)."""

from __future__ import annotations

from pytest import MonkeyPatch

from config.tenant_config import (
    SRC_TYPESPEC_DOCS,
    TenantConfig,
    TenantID,
    get_knowledge_source,
    get_tenant_config,
)
from tools.knowledge_tools import (
    _resolve_source_filters,
    _source_names,
    _wiki_page_filters,
)
from utils.azure_ai_search import NON_WIKI_FILTER, combine_source_filters

_TENANT = TenantID.TYPESPEC_CHANNEL_QA_BOT.value
_WIKI_ENTITY = "wiki_entity"
_WIKI_CONCEPT = "wiki_concept"


def test_internal_wiki_contexts_are_not_registered_sources():
    assert get_knowledge_source(_WIKI_ENTITY) is None
    assert get_knowledge_source(_WIKI_CONCEPT) is None


def test_source_names_drop_internal_wiki_contexts():
    got = _source_names(_TENANT, [SRC_TYPESPEC_DOCS, _WIKI_ENTITY])
    assert got == [SRC_TYPESPEC_DOCS]


def test_internal_only_source_request_falls_back_to_tenant_sources():
    got = _source_names(_TENANT, [_WIKI_ENTITY, _WIKI_CONCEPT])
    assert SRC_TYPESPEC_DOCS in got
    assert _WIKI_ENTITY not in got and _WIKI_CONCEPT not in got


def test_source_names_fall_back_to_tenant_sources():
    got = _source_names(_TENANT, None)
    assert got
    assert _WIKI_ENTITY not in got and _WIKI_CONCEPT not in got


def test_cross_document_wiki_pages_are_enabled_by_default():
    assert TenantConfig().enable_wiki_cross_document_pages


def test_typespec_tenant_uses_default_cross_document_wiki_setting():
    config = get_tenant_config(TenantID.TYPESPEC_CHANNEL_QA_BOT)
    assert config is not None
    assert config.enable_wiki_cross_document_pages


def test_wiki_page_filters_add_internal_contexts_for_enabled_tenant():
    source_filters = {SRC_TYPESPEC_DOCS: "context_id eq 'typespec_docs'"}
    got = _wiki_page_filters(_TENANT, source_filters)
    assert got[SRC_TYPESPEC_DOCS] == source_filters[SRC_TYPESPEC_DOCS]
    assert got[_WIKI_ENTITY] == "context_id eq 'wiki_entity'"
    assert got[_WIKI_CONCEPT] == "context_id eq 'wiki_concept'"


def test_wiki_page_filters_keep_only_summaries_when_explicitly_disabled(
    monkeypatch: MonkeyPatch,
):
    monkeypatch.setattr(
        "tools.knowledge_tools.get_tenant_config",
        lambda _: TenantConfig(enable_wiki_cross_document_pages=False),
    )
    got = _wiki_page_filters(TenantID.API_SPEC_REVIEW_BOT.value, {})
    assert "context_id ne 'wiki_entity'" in got["wiki_summaries"]
    assert "context_id ne 'wiki_concept'" in got["wiki_summaries"]


def test_wiki_page_filters_do_not_add_internal_contexts_when_explicitly_disabled(
    monkeypatch: MonkeyPatch,
):
    monkeypatch.setattr(
        "tools.knowledge_tools.get_tenant_config",
        lambda _: TenantConfig(enable_wiki_cross_document_pages=False),
    )
    source_filters = {SRC_TYPESPEC_DOCS: "context_id eq 'typespec_docs'"}
    got = _wiki_page_filters(TenantID.API_SPEC_REVIEW_BOT.value, source_filters)
    assert got == source_filters


def test_general_tenant_keeps_unscoped_wiki_access():
    assert _wiki_page_filters(TenantID.GENERAL_QA_BOT.value, {}) == {}


def test_source_filter_escapes_quotes_in_model_supplied_names():
    injected = "x' or context_id eq 'secret"
    got = _resolve_source_filters([injected], _TENANT)
    # The quote is doubled, so the whole value stays inside one string literal.
    assert got[injected] == "context_id eq 'x'' or context_id eq ''secret'"


def test_unknown_source_still_produces_a_filter():
    # Dropping it would leave the caller with an empty, i.e. unscoped, filter.
    got = _resolve_source_filters(["not_a_registered_source"], _TENANT)
    assert got == {"not_a_registered_source": "context_id eq 'not_a_registered_source'"}


def test_combined_source_filters_keep_or_clauses_grouped():
    got = combine_source_filters(
        {
            "a": "context_id eq 'a'",
            "b": "context_id eq 'b'",
        },
        NON_WIKI_FILTER,
    )
    assert got == (
        f"((context_id eq 'a') or (context_id eq 'b')) and {NON_WIKI_FILTER}"
    )
