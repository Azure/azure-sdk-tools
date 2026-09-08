"""Knowledge retrieval tools for the Azure SDK QA Bot Agent."""

from __future__ import annotations

import asyncio
import logging
from enum import Enum
from pathlib import PurePosixPath
from typing import Annotated, Callable

from azure.core.exceptions import ResourceModifiedError
from azure.storage.blob.aio import BlobServiceClient
from pydantic import BaseModel

from config.app_config import get as cfg
from config.tenant_config import (
    KNOWLEDGE_SOURCE_REGISTRY,
    TenantID,
    get_knowledge_source,
    get_tenant_config,
)
from models.knowledge import Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.azure_ai_search import SearchClient, get_search_client
from utils.azure_storage import BlobContent, download_blob, upload_blob
from utils.knowledge_config import (
    KbTarget,
    get_kb_targets,
    select_kb_target,
)
from models.knowledge import KnowledgeChunk, Reference, SearchKnowledgeBaseResult
from tools import tool
from utils.azure_ai_search import (
    NON_WIKI_FILTER,
    WIKI_FILTER,
    _escape_odata,
    combine_source_filters,
    SearchClient,
    get_search_client,
)

logger = logging.getLogger(__name__)


# Expanded content beyond this limit is truncated to control context size.
_MAX_CONTENT_CHARS_PER_RESULT = 3000
# Bound the larger wiki result while retaining every ranked page and source.
_WIKI_PAGE_CONTENT_CHARS = 1800
_WIKI_SOURCE_CONTENT_CHARS = 1100

# Internal index contexts for cross-document Wiki pages. They are not
# model-selectable knowledge sources.
_WIKI_CROSS_DOCUMENT_CONTEXTS = ("wiki_entity", "wiki_concept")

# Wiki pages kept as full evidence.
_WIKI_TOP = 6
# Source chunks each kept page is routed back to, for grounded detail.
_WIKI_ROUTE_PER_PAGE = 8
_WIKI_ROUTE_MAX_REFS = 48
_WIKI_ROUTE_MAX_TOTAL = 12

# Shared by both retrieval tracks so the agent picks a strategy the same way.
_SEARCH_MODE_DESC = (
    "Search strategy to use. "
    "'quick' — dense + keyword retrieval, fast, good for straightforward "
    "factual lookups about a single feature, symbol, or process step "
    "(e.g., 'Which decorator marks an operation as long-running?'). "
    "Use 'quick' by default. "
    "'deep' — additionally runs agentic (intent-aware, multi-step) retrieval "
    "in parallel, better for complex questions that need cross-referencing "
    "multiple topics "
    "(e.g., 'How does adding a new API version interact with the SDK release "
    "and breaking-change review process?'). "
    "Use 'deep' only when the question genuinely spans multiple unrelated concepts. "
    "Default: 'quick'."
)

class KbSourceView(BaseModel):
    folder: str
    resolved: bool
    owner: str | None = None
    repo: str | None = None
    branch: str | None = None
    path: str | None = None
    scope: str | None = None
    reason: str | None = None  # populated when resolved=False


class KnowledgeSourceView(BaseModel):
    """A single knowledge source advertised for a tenant."""

    name: str
    description: str


class KnowledgeSourceCatalog(BaseModel):
    """The full set of knowledge sources configured for a tenant."""

    tenant_id: str | None = None
    sources: list[KnowledgeSourceView]

class ReadKnowledgeResult(BaseModel):
    blob_path: str
    content: str
    etag: str


class UpdateKnowledgeResult(BaseModel):
    blob_path: str
    status: str
    indexer_status: str | None = None
    error: str | None = None



class SearchMode(str, Enum):
    """Search strategy for knowledge retrieval."""

    quick = "quick"
    """Dense + keyword retrieval — fast, good for straightforward factual lookups."""

    deep = "deep"
    """Adds agentic retrieval in parallel — better for complex or multi-faceted questions."""


class ServiceType(str, Enum):
    """Azure service plane classification."""

    management_plane = "management-plane"
    data_plane = "data-plane"


class KnowledgeTools:
    """Tools for Azure SDK knowledge retrieval and search operations."""

    def __init__(
        self,
        *,
        settings: Callable[[str, str], str | None] | None = None,
        search_client: SearchClient | None = None,
        blob_client: BlobServiceClient | None = None,
    ) -> None:
        self._settings = settings or cfg
        self._search_client = search_client
        self._blob_client = blob_client

    def _get_search_client(self) -> SearchClient:
        return self._search_client or get_search_client()

    async def _download_blob(
        self, container: str, blob_path: str
    ) -> BlobContent | None:
        if self._blob_client is not None:
            return await download_blob(
                container,
                blob_path,
                include_metadata=True,
                client=self._blob_client,
            )
        return await download_blob(container, blob_path, include_metadata=True)

    async def _upload_blob(
        self, container: str, blob_path: str, data: bytes, etag: str
    ) -> None:
        if self._blob_client is not None:
            await upload_blob(
                container,
                blob_path,
                data,
                etag=etag,
                client=self._blob_client,
            )
        else:
            await upload_blob(container, blob_path, data, etag=etag)

    @tool
    async def search_knowledge_base(
        self,
        *,
        queries: Annotated[
            list[str],
            "1–3 search queries to run against the knowledge base. "
            "The knowledge base contains both documentation and historical "
            "Q&A; the index embeds the full body text, so complete natural "
            "sentences retrieve far better than stripped keyword fragments. "
            "The search combines semantic/vector and BM25 keyword retrieval. "
            "Provide the queries as a **progressive abstraction ladder** — "
            "start concrete and get more abstract with each query, so the set "
            "covers both exact-wording recall and conceptual-topic recall. "
            "QUERY 1 (REQUIRED) — the **most concrete** version: a full, "
            "standalone restatement of the user's question that KEEPS their "
            "concrete nouns (decorator names, model/property names, version "
            "numbers, error text, rule IDs, check names, config keys) verbatim. "
            "This is also the exact-term lookup path. Resolve follow-up context "
            "(replace 'it'/'this' with the real subject) and normalize obvious synonyms "
            "(e.g. 'CI failure' → 'validation failure'). "
            "QUERY 2 (optional) — **more abstract**: drop the conversation-"
            "specific sample values (their own model name, exact version "
            "strings) but keep the underlying feature/concept terms. "
            "QUERY 3 (optional) — the **most abstract / core question**: "
            "distill to the underlying concept the docs are titled by, as one "
            "short question or topic phrase. "
            "At least 1, at most 3 queries.",
        ],
        sources: Annotated[
            list[str] | None,
            "List of knowledge source **names** to search. "
            "Pick from the sources exposed by the active skill or tenant context. "
            "Example: ['typespec_docs', 'azure_api_guidelines']. "
            "GUIDANCE: When seeking prescriptive guidance (the right pattern/template to use), "
            "prioritize authoritative sources (e.g., 'typespec_azure_docs', 'azure_resource_manager_rpc') "
            "OVER historical Q&A sources (e.g., 'static_typespec_qa'). "
            "Q&A sources often discuss workarounds and edge cases; for template selection, "
            "start with the official docs. "
            "If not provided, falls back to the tenant's sources when a "
            "`tenant_id` is given, otherwise every source in the whole "
            "knowledge base.",
        ] = None,
        tenant_id: Annotated[
            str | None,
            "Optional tenant ID for the current conversation. When set, the "
            "search is scoped to that tenant's configured sources and its "
            "source-filter overrides are applied. When omitted (None) AND no "
            "explicit `sources` list is given, the search spans the ENTIRE "
            "knowledge base across every tenant — use this to check whether the "
            "required information exists anywhere in the KB.",
        ] = None,
        service_type: Annotated[
            str | None,
            "Filter results by Azure service plane. ALMOST ALWAYS use None. "
            "Derive it ONLY from an explicit, unambiguous signal, checking in "
            "this order and stopping at the first match: "
            "1) PR label — azure-rest-api-specs PR carries a 'management-plane' "
            "or 'data-plane' label. "
            "2) File path — path contains 'resource-manager' → management-plane; "
            "path contains 'data-plane' → data-plane. "
            "3) Keyword — user literally says ARM / management-plane / RPaaS / "
            "RPSaaS → management-plane; user literally says data-plane → data-plane. "
            "4) Otherwise → None (SDK release, onboarding, pipelines, reviews, "
            "general questions, anything ambiguous). "
            "When in doubt, use None.",
        ] = None,
        search_mode: Annotated[str, _SEARCH_MODE_DESC] = "quick",
    ) -> SearchKnowledgeBaseResult:
        """Search the knowledge base with one or more queries and return results with full section context.

        Each query runs vector and keyword search in parallel, plus agentic
        search in deep mode, then all results are merged and deduplicated. Use
        multiple queries to cover different facets of the user's problem —
        the original question, related concepts, and potential solutions.
        """
        # Resolve sources: explicit list wins; otherwise fall back to the
        # tenant's configured sources, or the whole registry when no tenant.
        if not sources:
            if tenant_id:
                config = get_tenant_config(TenantID(tenant_id))
                sources = [src.name for src in config.sources] if config else []
            else:
                sources = list(KNOWLEDGE_SOURCE_REGISTRY.keys())

        search_client = self._get_search_client()

        # Resolve source → OData filter using tenant config
        source_filters = _resolve_source_filters(sources, tenant_id, service_type)

        use_deep = search_mode == SearchMode.deep.value
        capped_queries = queries[:3]

        raw_chunks = await search_client.fused_search(
            capped_queries,
            source_filters,
            extra_filter=NON_WIKI_FILTER,
            use_agentic=use_deep,
        )

        logger.info(
            "Search completed: mode=%s, queries=%s, raw_chunks=%d",
            search_mode,
            capped_queries,
            len(raw_chunks),
        )

        unique_chunks = search_client.deduplicate_chunks(raw_chunks)

        top_k = search_client.top_k
        if len(unique_chunks) > top_k:
            logger.info("Capping results from %d to %d (top_k)", len(unique_chunks), top_k)
            unique_chunks = unique_chunks[:top_k]

        logger.info(
            "After deduplication + rerank: %d chunks (from %d raw)",
            len(unique_chunks),
            len(raw_chunks),
        )

        expand_tasks = [
            search_client.expand_by_hierarchy(chunk) for chunk in unique_chunks
        ]
        expanded = await asyncio.gather(*expand_tasks)

        # Log final search results.
        logger.info("=========Final Search Result=========")
        refs = [
            Reference(
                title=_build_reference_title(
                    expanded[i].title,
                    expanded[i].header1,
                    expanded[i].header2,
                    expanded[i].header3,
                ),
                source=expanded[i].source,
                blob_path=f"{expanded[i].source}/{expanded[i].title}",
                link=expanded[i].link,
                content=_truncate_content(expanded[i].content),
                score=unique_chunks[i].rerank_score,
            )
            for i in range(len(expanded))
        ]
        for i, ref in enumerate(refs):
            logger.info(
                "Result [%d] score=%.2f, source=%s, title=%s, link=%s, content_len=%d",
                i + 1,
                ref.score,
                ref.source,
                ref.title,
                ref.link,
                len(ref.content or ""),
            )
        logger.info(
            "===================================== total=%d results",
            len(refs),
        )

        return SearchKnowledgeBaseResult(results=refs)

    @tool
    async def read_knowledge(
        self,
        *,
        blob_path: Annotated[
            str,
            "Exact `blob_path` returned by `search_knowledge_base`. The path must be a Markdown document under a registered knowledge-source folder.",
        ],
    ) -> ReadKnowledgeResult:
        """Read a complete knowledge document and its version for a safe update."""
        normalized_path = _validate_blob_path(blob_path)
        blob = await self._download_blob(
            self._settings("STORAGE_KNOWLEDGE_CONTAINER", "") or "",
            normalized_path,
        )
        if blob is None:
            raise ValueError(f"Knowledge document not found: {normalized_path}")
        return ReadKnowledgeResult(
            blob_path=normalized_path,
            content=blob.data.decode("utf-8"),
            etag=blob.etag,
        )

    @tool
    async def update_knowledge(
        self,
        *,
        blob_path: Annotated[
            str,
            "Exact `blob_path` previously passed to `read_knowledge`.",
        ],
        expected_content: Annotated[
            str,
            "Exact existing text to replace. It must occur exactly once in the current document; include enough surrounding text to make it unique.",
        ],
        replacement_content: Annotated[
            str,
            "Replacement text. Use an empty string only when the grounded fix requires deleting the expected content.",
        ],
        etag: Annotated[
            str,
            "The ETag returned by `read_knowledge`; prevents overwriting a document changed since it was read.",
        ],
    ) -> UpdateKnowledgeResult:
        """Safely replace one exact passage and refresh the knowledge index."""
        normalized_path = _validate_blob_path(blob_path)
        if not expected_content:
            raise ValueError("expected_content must not be empty")

        knowledge_container = self._settings("STORAGE_KNOWLEDGE_CONTAINER", "") or ""
        blob = await self._download_blob(
            knowledge_container,
            normalized_path,
        )
        if blob is None:
            raise ValueError(f"Knowledge document not found: {normalized_path}")
        occurrence_count = blob.data.decode("utf-8").count(expected_content)
        if occurrence_count != 1:
            raise ValueError(
                "expected_content must occur exactly once; "
                f"found {occurrence_count} occurrences"
            )

        updated_content = blob.data.decode("utf-8").replace(
            expected_content,
            replacement_content,
            1,
        )
        print("##vso[task.setvariable variable=restore_required]true", flush=True)
        try:
            await self._upload_blob(
                knowledge_container,
                normalized_path,
                updated_content.encode("utf-8"),
                etag,
            )
        except ResourceModifiedError:
            return UpdateKnowledgeResult(
                blob_path=normalized_path,
                status="conflict",
                error="The knowledge document changed after it was read. Read it again before retrying.",
            )

        try:
            indexer_status = await self._get_search_client().run_indexer()
        except (RuntimeError, TimeoutError) as exc:
            logger.exception("Knowledge indexer did not complete after updating %s", normalized_path)
            return UpdateKnowledgeResult(
                blob_path=normalized_path,
                status="indexing_failed",
                indexer_status="failed",
                error=str(exc),
            )
        return UpdateKnowledgeResult(
            blob_path=normalized_path,
            status="updated",
            indexer_status=indexer_status,
        )

    @tool
    async def list_knowledge_sources(
        self,
        *,
        tenant_id: Annotated[
            str | None,
            "Optional tenant ID. When provided, returns only the sources "
            "configured for that tenant. When omitted (None), returns EVERY "
            "source registered across the whole project — use this to check "
            "whether information exists anywhere in the knowledge base.",
        ] = None,
    ) -> KnowledgeSourceCatalog:
        """List maintainable knowledge sources for a tenant, or the whole project.

        When *tenant_id* is given, returns only that tenant's sources. When
        omitted, returns every source registered across the entire project.
        Each source carries a ``name`` and ``description`` so the caller can
        reason about which source *should* cover a given question, then
        target ``search_knowledge_base`` with an explicit ``sources`` list.
        A source whose topic matches the question but returns nothing on an
        on-topic query is a strong signal of a knowledge gap in that source.
        """
        if tenant_id is None:
            sources = [
                KnowledgeSourceView(name=src.name, description=src.description)
                for src in KNOWLEDGE_SOURCE_REGISTRY.values()
            ]
            return KnowledgeSourceCatalog(tenant_id=None, sources=sources)

        try:
            config = get_tenant_config(TenantID(tenant_id))
        except ValueError:
            logger.warning("Unknown tenant_id for source listing: %s", tenant_id)
            return KnowledgeSourceCatalog(tenant_id=tenant_id, sources=[])

        sources = (
            [
                KnowledgeSourceView(name=src.name, description=src.description)
                for src in config.sources
            ]
            if config
            else []
        )
        return KnowledgeSourceCatalog(tenant_id=tenant_id, sources=sources)

    @tool
    async def resolve_kb_source(
        self,
        *,
        folder: Annotated[
            str,
            "The chunk `source` value from a knowledge-base hit — used as "
            "the join key into the upstream knowledge-config.json.",
        ],
        blob_path: Annotated[
            str | None,
            "The exact `blob_path` from the same search hit. Required when "
            "multiple repository paths use the same source folder.",
        ] = None,
    ) -> KbSourceView:
        """Resolve a KB folder to its upstream ownership metadata.

        Returns the owner/repo/branch/path where the KB content lives, to
        cite in a KB-gap issue. ``resolved=False`` when the folder is
        unmapped or an ambiguous path cannot be selected.
        """
        targets: tuple[KbTarget, ...] = ()
        try:
            targets = await get_kb_targets(folder)
        except Exception:
            logger.exception("knowledge_config lookup failed for %s", folder)

        target = select_kb_target(folder, blob_path, targets)
        if target is not None:
            return KbSourceView(
                folder=folder,
                resolved=True,
                owner=target.owner,
                repo=target.repo,
                branch=target.branch,
                path=target.path,
                scope=target.scope,
            )

        if targets:
            return KbSourceView(
                folder=folder,
                resolved=False,
                reason=(
                    "blob_path_required_for_ambiguous_folder"
                    if blob_path is None
                    else "blob_path_not_in_registered_source_path"
                ),
            )

        if folder in KNOWLEDGE_SOURCE_REGISTRY:
            return KbSourceView(
                folder=folder,
                resolved=True,
                path=folder,
                scope=folder,
            )

        return KbSourceView(
            folder=folder,
            resolved=False,
            reason="folder_unmapped_or_non_github",
        )

    @tool
    async def wiki_search(
        self,
        *,
        queries: Annotated[
            list[str],
            "1-3 queries for the curated WIKI layer: per-document SUMMARY pages, "
            "per-symbol ENTITY pages (decorators/APIs/types), per-topic CONCEPT "
            "pages. Use symbol/concept names or short topic phrases. Returns the "
            "top pages' full synthesized content PLUS the source-document chunks "
            "they were built from — enough to answer most conceptual/overview "
            "questions in one call.",
        ],
        tenant_id: Annotated[str, "The active tenant ID for the current conversation."],
        sources: Annotated[
            list[str] | None,
            "Optional list of knowledge source names to scope the search. "
            "If omitted, all sources configured for the tenant are used.",
        ] = None,
        search_mode: Annotated[str, _SEARCH_MODE_DESC] = "quick",
    ) -> SearchKnowledgeBaseResult:
        """Search wiki pages and their routed source chunks."""
        sources = _source_names(tenant_id, sources)
        search_client = get_search_client()
        source_filters = _resolve_source_filters(sources, tenant_id, None)
        wiki_page_filters = _wiki_page_filters(tenant_id, source_filters)
        capped_queries = queries[:3]

        raw = await search_client.fused_search(
            capped_queries,
            wiki_page_filters,
            extra_filter=WIKI_FILTER,
            use_agentic=search_mode == SearchMode.deep.value,
        )
        page_hits = _deduplicate_wiki_pages(search_client.deduplicate_chunks(raw))
        wiki_pages = sorted(
            page_hits, key=lambda c: c.rerank_score, reverse=True
        )[:_WIKI_TOP]
        if not wiki_pages:
            logger.info("wiki_search: no wiki pages for queries=%s", capped_queries)
            return SearchKnowledgeBaseResult(results=[])

        # Route each page to the SOURCE chunks it was built from (grounded detail).
        routed = await search_client.backfill_wiki_sources(
            wiki_pages,
            queries=capped_queries,
            per_page=_WIKI_ROUTE_PER_PAGE,
            max_refs=_WIKI_ROUTE_MAX_REFS,
            max_total=_WIKI_ROUTE_MAX_TOTAL,
            source_filter=combine_source_filters(source_filters) or None,
        )
        combined = wiki_pages + routed
        expanded = await asyncio.gather(
            *[search_client.expand_by_hierarchy(c) for c in combined]
        )
        page_count = len(wiki_pages)
        results = _refs_from_expanded(
            expanded[:page_count],
            combined[:page_count],
            max_content_chars=_WIKI_PAGE_CONTENT_CHARS,
        )
        results.extend(
            _refs_from_expanded(
                expanded[page_count:],
                combined[page_count:],
                max_content_chars=_WIKI_SOURCE_CONTENT_CHARS,
            )
        )
        logger.info(
            "wiki_search: mode=%s, %d page(s) + %d routed source(s) for queries=%s",
            search_mode, len(wiki_pages), len(routed), capped_queries,
        )
        return SearchKnowledgeBaseResult(results=results)


def _source_names(tenant_id: str, sources: list[str] | None) -> list[str]:
    """Resolve model-selectable source names for either retrieval track."""
    config = get_tenant_config(TenantID(tenant_id))
    tenant_sources = [src.name for src in config.sources] if config else []
    names = list(sources) if sources else tenant_sources
    ignored = [n for n in names if n in _WIKI_CROSS_DOCUMENT_CONTEXTS]
    if ignored:
        logger.warning("Ignoring internal Wiki contexts requested as sources: %s", ignored)
    kept = [n for n in names if n not in _WIKI_CROSS_DOCUMENT_CONTEXTS]
    if kept:
        return kept
    return tenant_sources


def _wiki_page_filters(
    tenant_id: str,
    source_filters: dict[str, str],
) -> dict[str, str]:
    """Add internal Wiki page contexts when enabled for the tenant."""
    tenant_config = get_tenant_config(TenantID(tenant_id))
    enabled = bool(
        tenant_config and tenant_config.enable_wiki_cross_document_pages
    )
    if not source_filters:
        if enabled:
            return {}
        exclusions = " and ".join(
            f"context_id ne '{context}'"
            for context in _WIKI_CROSS_DOCUMENT_CONTEXTS
        )
        return {"wiki_summaries": exclusions}

    page_filters = dict(source_filters)
    if enabled:
        page_filters.update(
            {
                context: f"context_id eq '{context}'"
                for context in _WIKI_CROSS_DOCUMENT_CONTEXTS
            }
        )
    return page_filters


def _resolve_source_filters(
    sources: list[str],
    tenant_id: str | None = None,
    service_type: str | None = None,
) -> dict[str, str]:
    """Build source-name → OData-filter mapping.

    Each source gets a base ``context_id`` filter.  Tenant-level overrides
    (when a tenant is given) and the service-type clause are layered on with
    ``and``.
    """
    source_filter_overrides: dict[str, str] = {}
    if tenant_id:
        try:
            tenant_config = get_tenant_config(TenantID(tenant_id))
        except ValueError:
            tenant_config = None
        source_filter_overrides = (
            tenant_config.source_filter if tenant_config else {}
        )

    valid_service_types = {t.value for t in ServiceType}
    service_type_filter = (
        f"(service_type eq '{service_type}' or service_type eq null)"
        if service_type and service_type in valid_service_types
        else None
    )

    source_filters: dict[str, str] = {}
    for source_name in sources:
        # ``sources`` reaches here straight from a model-authored tool call, so
        # the value is escaped before it goes into the filter.  Unregistered
        # names are kept rather than dropped: an escaped unknown name matches
        # nothing, whereas dropping every name would leave the search unfiltered.
        if not get_knowledge_source(source_name):
            logger.warning("Unknown knowledge source %r requested", source_name)
        filter_clauses = [f"context_id eq '{_escape_odata(source_name)}'"]
        if source_filter_overrides.get(source_name):
            filter_clauses.append(f"({source_filter_overrides[source_name]})")
        if service_type_filter:
            filter_clauses.append(service_type_filter)
        source_filters[source_name] = " and ".join(filter_clauses)
    return source_filters


def _validate_blob_path(blob_path: str) -> str:
    """Return a normalized knowledge blob path or reject unsafe/unmapped paths."""
    if not blob_path or "\\" in blob_path:
        raise ValueError("blob_path must use a non-empty forward-slash path")
    path = PurePosixPath(blob_path)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        raise ValueError("blob_path must be a normalized relative path")
    if len(path.parts) < 2 or path.parts[0] not in KNOWLEDGE_SOURCE_REGISTRY:
        raise ValueError("blob_path must be under a registered knowledge-source folder")
    if path.suffix.lower() not in {".md", ".mdx"}:
        raise ValueError("blob_path must identify a Markdown document")
    return path.as_posix()


def _truncate_content(
    content: str | None,
    max_chars: int = _MAX_CONTENT_CHARS_PER_RESULT,
) -> str:
    """Truncate content to *max_chars* to control context size."""
    if not content:
        return ""
    if len(content) <= max_chars:
        return content
    return content[:max_chars] + "\n... [truncated]"


def _deduplicate_wiki_pages(chunks: list[KnowledgeChunk]) -> list[KnowledgeChunk]:
    """Keep the strongest chunk hit for each synthesized wiki page."""
    best: dict[tuple[str, str, str], KnowledgeChunk] = {}
    for chunk in chunks:
        key = (chunk.page_type, chunk.source, chunk.title)
        prior = best.get(key)
        if prior is None or chunk.rerank_score > prior.rerank_score:
            best[key] = chunk
    return list(best.values())


def _build_reference_title(
    document_title: str,
    header1: str | None,
    header2: str | None,
    header3: str | None,
) -> str:
    """Build a reference title from the deepest available header path."""
    parts = [part for part in (header1, header2, header3) if part]
    return " | ".join(parts) if parts else document_title


def _refs_from_expanded(
    expanded: list[KnowledgeChunk],
    scored: list[KnowledgeChunk],
    *,
    max_content_chars: int = _MAX_CONTENT_CHARS_PER_RESULT,
) -> list[Reference]:
    """Build References from expanded chunks, taking scores from *scored*."""
    return [
        Reference(
            title=_build_reference_title(
                item.title,
                item.header1,
                item.header2,
                item.header3,
            ),
            source=item.source,
            link=item.link,
            content=_truncate_content(
                item.content,
                max_chars=max_content_chars,
            ),
            score=score.rerank_score,
        )
        for item, score in zip(expanded, scored, strict=True)
    ]
