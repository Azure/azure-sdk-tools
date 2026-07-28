"""Construct GraphRAG's LocalSearch context builder once per snapshot load.

We drive ``LocalSearchMixedContext.build_context`` directly — the
context-building half of GraphRAG's Local Search — to get raw
graph-grounded snippets without paying the ~20s LLM completion cost. The
builder is stateless across calls, so it is built once per (re)load and
shared by concurrent queries. Community-report embeddings are bulk-fetched
once here too, eliminating thousands of serial per-query AI Search
round-trips.
"""

from __future__ import annotations

import asyncio
import logging
import time
from typing import Any

from config.app_config import get as cfg
from utils.knowledge_graph.filtering import wrap_entity_store

logger = logging.getLogger(__name__)


async def preload_report_embeddings(config: Any) -> dict[str, list[float]]:
    """Bulk-fetch every community-report embedding into an in-memory cache.

    The LocalSearch context builder needs each ``CommunityReport`` to carry
    a ``full_content_embedding``. GraphRAG's default
    ``read_indexer_report_embeddings`` issues one ``search_by_id`` per
    report (thousands of serial round-trips per query). Instead we paginate
    the ``community_full_content`` index once and return
    ``{report_id: embedding}``; :func:`build_context_builder` applies these
    directly. Returns an empty dict on any failure (the builder falls back
    to the slow per-report lookup).
    """
    if config is None:
        return {}

    try:
        from graphrag.config.embeddings import community_full_content_embedding
        from graphrag.utils.api import get_embedding_store
    except ImportError:
        logger.warning(
            "Could not import graphrag vector store factory; "
            "community embedding preload disabled."
        )
        return {}

    def _fetch_all() -> dict[str, list[float]]:
        store = get_embedding_store(
            config=config.vector_store,
            embedding_name=community_full_content_embedding,
        )
        id_field = store.id_field
        vector_field = store.vector_field

        # ``SearchItemPaged`` already iterates every match via continuation
        # tokens, so ``results_per_page`` only tunes the page size to reduce
        # round-trips. Older/newer azure-search-documents releases disagree on
        # whether ``search()`` accepts ``results_per_page`` — some forward the
        # unknown kwarg all the way down to ``Session.request()`` and raise
        # ``TypeError`` *lazily during iteration*. So collect inside a helper and
        # retry with default paging if the page-size kwarg is rejected.
        def _collect(**extra: object) -> dict[str, list[float]]:
            results = store.db_connection.search(
                search_text="*",
                select=[id_field, vector_field],
                **extra,
            )
            out: dict[str, list[float]] = {}
            for result in results:
                doc_id = result.get(id_field)
                vector = result.get(vector_field)
                if doc_id and vector is not None:
                    out[str(doc_id)] = list(vector)
            return out

        try:
            return _collect(results_per_page=1000)
        except TypeError:
            logger.warning(
                "azure-search 'search()' rejected results_per_page; "
                "retrying community-embedding preload with default paging."
            )
            return _collect()

    start = time.monotonic()
    try:
        cache = await asyncio.to_thread(_fetch_all)
    except Exception:
        logger.warning(
            "Failed to preload community-report embeddings; search_graph will "
            "fall back to per-query fetch (slow).",
            exc_info=True,
        )
        return {}

    logger.info(
        "Preloaded %d community-report embeddings in %.2fs "
        "(eliminates per-query AI Search round-trips)",
        len(cache),
        time.monotonic() - start,
    )
    return cache


def build_context_builder(
    config: Any,
    dfs: "dict[str, Any]",
    community_level: int,
    report_embeddings_cache: dict[str, list[float]],
) -> "tuple[Any, dict[str, Any]]":
    """Build the LocalSearch context builder and its ``build_context`` params.

    Mirrors ``graphrag.api.query.local_search`` up to but not including the
    LLM completion call. We call ``get_local_search_engine`` to reuse the
    factory's wiring (embedder, tokenizer, vector store, params) and keep
    only its ``context_builder`` + ``context_builder_params`` — the
    ``LocalSearch`` wrapper and its chat_model are discarded.

    The returned builder's entity vector store is wrapped for tenant
    filtering (see :mod:`filtering`). Returns ``(context_builder, params)``.
    """
    from graphrag.config.embeddings import entity_description_embedding
    from graphrag.query.factory import get_local_search_engine
    from graphrag.query.indexer_adapters import (
        read_indexer_entities,
        read_indexer_relationships,
        read_indexer_reports,
        read_indexer_text_units,
    )
    from graphrag.utils.api import get_embedding_store

    start = time.monotonic()

    description_embedding_store = get_embedding_store(
        config=config.vector_store,
        embedding_name=entity_description_embedding,
    )

    entities_ = read_indexer_entities(
        dfs["entities"], dfs["communities"], community_level
    )
    reports = read_indexer_reports(
        dfs["community_reports"], dfs["communities"], community_level
    )
    text_units_ = read_indexer_text_units(dfs["text_units"])
    relationships_ = read_indexer_relationships(dfs["relationships"])

    _apply_report_embeddings(config, reports, report_embeddings_cache)

    engine = get_local_search_engine(
        config=config,
        reports=reports,
        text_units=text_units_,
        entities=entities_,
        relationships=relationships_,
        covariates={},
        description_embedding_store=description_embedding_store,
        response_type="multiple paragraphs",  # unused (no LLM call)
        system_prompt=None,
        callbacks=None,
    )
    context_builder = engine.context_builder
    context_params = dict(engine.context_builder_params or {})

    # Optimization: surface community-report synthesis (step 1.A). Community
    # reports are GraphRAG's cross-document summaries — the one artifact the KB
    # cannot produce — so we spend part of the context budget on them and lift
    # them into the returned references (see extraction.py). The remainder goes
    # to source text units. Overridable via GRAPH_* App Configuration keys.
    context_params["community_prop"] = float(cfg("GRAPH_LS_COMMUNITY_PROP", "0.25"))
    context_params["text_unit_prop"] = float(cfg("GRAPH_LS_TEXT_UNIT_PROP", "0.6"))
    context_params["max_context_tokens"] = int(
        cfg("GRAPH_LS_MAX_CONTEXT_TOKENS", "16000")
    )
    wrap_entity_store(context_builder)

    logger.info(
        "Built LocalSearch context builder in %.2fs "
        "(entities=%d, reports=%d, text_units=%d, relationships=%d, "
        "text_unit_prop=%.2f, community_prop=%.2f, max_context_tokens=%d)",
        time.monotonic() - start,
        len(entities_),
        len(reports),
        len(text_units_),
        len(relationships_),
        context_params["text_unit_prop"],
        context_params["community_prop"],
        context_params["max_context_tokens"],
    )
    return context_builder, context_params


def _apply_report_embeddings(
    config: Any, reports: Any, cache: dict[str, list[float]]
) -> None:
    """Attach cached embeddings to reports, or fall back to per-report fetch."""
    if cache:
        hits = 0
        for report in reports:
            vec = cache.get(str(report.id))
            report.full_content_embedding = vec
            if vec is not None:
                hits += 1
        logger.info(
            "Applied %d/%d community-report embeddings from cache",
            hits,
            len(reports),
        )
        return

    # Preload failed — fall back to graphrag's slow per-report lookup so the
    # builder still functions.
    from graphrag.config.embeddings import community_full_content_embedding
    from graphrag.query.indexer_adapters import read_indexer_report_embeddings
    from graphrag.utils.api import get_embedding_store

    fallback_store = get_embedding_store(
        config=config.vector_store,
        embedding_name=community_full_content_embedding,
    )
    read_indexer_report_embeddings(reports, fallback_store)
