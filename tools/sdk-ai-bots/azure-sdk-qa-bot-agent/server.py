"""Azure SDK QA Bot — Backend Server.

The backend server that the Teams App communicates with.
Calls the hosted Chat Agent via the Azure AI Foundry SDK (azure-ai-projects)
and handles feedback through a local workflow.
"""

import asyncio
import contextlib
import logging
import os
import sys
import time
from contextvars import ContextVar
from contextlib import asynccontextmanager
from uuid import uuid4
from dotenv import load_dotenv

load_dotenv(override=False)

from fastapi import FastAPI, Request
from models.chat import ChatRequest, ChatResponse
from models.conversation import ConversationMessage, SaveConversationMessageResponse
from models.feedback import FeedbackRequest, FeedbackResponse
from models.intention import IntentionRequest, IntentionResponse
from models.knowledge import GraphQueryRequest, GraphSearchResult, Reference
from models.knowledge_retrieve import KnowledgeRetrieveResponse, KnowledgeRetrieveRequest
from services.chat_service import ChatService
from services.conversation_service import ConversationService
from services.feedback_service import FeedbackService
from services.intention_service import IntentionService
from services.knowledge_service import KnowledgeService
from services.thread_memory_service import ThreadMemoryService
from utils.azure_ai_foundry import close_clients
from utils.azure_cosmosdb import close_cosmos_client
from _version import VERSION
from utils.azure_credential import close_credential
from utils.azure_storage import close_storage_client
from utils.azure_monitor import (
    configure_metrics,
    record_chat_request,
    record_chat_duration,
)
from utils.background_tasks import BackgroundTaskTracker
import config.app_config as app_config
from config.tenant_config import TenantID
import uvicorn

_request_id_ctx_var: ContextVar[str] = ContextVar("request_id", default="system")

# Snippet length cap mirrored from the chat-agent tool that used to do
# this dedupe locally. Keeps the over-the-wire shape identical to the
# pre-A-route implementation so the agent's prompt stays bounded.
_GRAPH_SNIPPET_MAX_CHARS = 1200


class _RequestIdFilter(logging.Filter):
    def filter(self, record: logging.LogRecord) -> bool:
        record.request_id = _request_id_ctx_var.get() or "system"
        return True


def _configure_logging() -> None:
    """Configure process-wide logging for backend debug and local runs."""
    formatter = logging.Formatter(
        "%(asctime)s %(levelname)s [RequestID: %(request_id)s] %(name)s: %(message)s"
    )
    request_id_filter = _RequestIdFilter()

    root = logging.getLogger()
    if not root.handlers:
        handler = logging.StreamHandler(sys.stdout)
        handler.setFormatter(formatter)
        handler.addFilter(request_id_filter)
        root.addHandler(handler)
        root.setLevel(logging.INFO)
    else:
        root.setLevel(logging.INFO)
        for handler in root.handlers:
            handler.setFormatter(formatter)
            handler.addFilter(request_id_filter)

    for logger_name in ("uvicorn", "uvicorn.error", "uvicorn.access"):
        uvicorn_logger = logging.getLogger(logger_name)
        for handler in uvicorn_logger.handlers:
            handler.setFormatter(formatter)
            handler.addFilter(request_id_filter)

    # Suppress noisy Azure SDK HTTP / credential / telemetry loggers
    for noisy in (
        "azure.core.pipeline.policies.http_logging_policy",
        "azure.cosmos",
        "azure.monitor.opentelemetry",
        "azure.monitor.opentelemetry.exporter",
        "azure.monitor.opentelemetry.exporter.export",
        "azure.monitor.opentelemetry.exporter.export._base",
    ):
        logging.getLogger(noisy).setLevel(logging.WARNING)


_configure_logging()
logger = logging.getLogger(__name__)
configure_metrics()


@asynccontextmanager
async def lifespan(application: FastAPI):
    """Startup / shutdown lifecycle for the FastAPI app."""
    logger.info("Backend server starting up")
    await app_config.init()

    # Pre-warm GraphRAG: download parquets + bulk-fetch community
    # embeddings off the request path so the first user query doesn't
    # pay a ~30-60s cold-load tax. Runs as a background task and
    # tolerates failure (queries will fall back to lazy load).
    async def _warm_graph():
        try:
            from utils.knowledge_graph import get_knowledge_graph_service

            service = get_knowledge_graph_service()
            if not service.enabled:
                return
            logger.info("Pre-warming GraphRAG knowledge base at startup")
            status = await service.reload()
            if not status.get("loaded"):
                logger.error(
                    "GraphRAG pre-warm did not fully load the engine: %s",
                    status,
                )
            else:
                logger.info(
                    "GraphRAG pre-warm complete: %s",
                    {k: status.get(k) for k in ("version", "row_counts")},
                )
        except Exception:
            # Log at ERROR (not WARNING) so production alerting picks
            # this up — a failed pre-warm previously left the service
            # in a half-loaded state where every query silently returned
            # an empty answer. The fix in reload() now rolls back, so
            # a subsequent query will hit the lazy load path and retry.
            logger.error(
                "GraphRAG pre-warm failed; first query will trigger lazy load",
                exc_info=True,
            )

    warm_task = asyncio.create_task(_warm_graph())

    # Daily poll: check ``latest.json`` for a new GraphRAG snapshot and
    # hot-swap it in when the build_id changes. Interval is configurable
    # via GRAPH_RELOAD_POLL_SECONDS (default 24h); set to <= 0 to disable
    # polling.
    async def _poll_graph_manifest():
        try:
            interval = float(os.environ.get("GRAPH_RELOAD_POLL_SECONDS", "86400"))
        except (TypeError, ValueError):
            interval = 86400.0
        if interval <= 0:
            logger.info("GraphRAG manifest polling disabled (interval <= 0)")
            return
        from utils.knowledge_graph import get_knowledge_graph_service

        service = get_knowledge_graph_service()
        if not service.enabled:
            return
        while True:
            try:
                await asyncio.sleep(interval)
                await service.reload_if_changed()
            except asyncio.CancelledError:
                raise
            except Exception:
                logger.exception("GraphRAG scheduled manifest poll failed")

    poll_task = asyncio.create_task(_poll_graph_manifest())

    try:
        yield
    finally:
        for task in (warm_task, poll_task):
            task.cancel()
            with contextlib.suppress(asyncio.CancelledError, Exception):
                await task
        # Cleanup SDK clients on shutdown
        logger.info("Backend server shutting down")
        await BackgroundTaskTracker.instance().shutdown()
        await close_clients()
        await close_cosmos_client()
        await close_storage_client()
        await close_credential()


app = FastAPI(title="Azure SDK QA Bot Backend", version=VERSION, lifespan=lifespan)


@app.get("/ping")
async def ping():
    """Health check endpoint used by App Service and the deploy pipeline."""
    return {"status": "ok", "version": VERSION}


@app.middleware("http")
async def request_id_middleware(request: Request, call_next):
    request_id = str(uuid4())
    token = _request_id_ctx_var.set(request_id)
    try:
        response = await call_next(request)
    finally:
        _request_id_ctx_var.reset(token)
    response.headers["x-request-id"] = request_id
    return response


_chat_service = ChatService()
_conversation_service = ConversationService()
_feedback_service = FeedbackService()
_intention_service = IntentionService()
_knowledge_service = KnowledgeService()
_thread_memory_service = ThreadMemoryService()


@app.post(
    "/completion", response_model=ChatResponse
)  # backwards compatibility for old endpoint
@app.post("/agent/chat", response_model=ChatResponse)
async def handle_chat(req: ChatRequest):
    """Process a chat request through the chat service."""
    # backwards azure-sdk-qa-bot tenant ID
    if req.tenant_id == TenantID.AZURE_SDK_QA_BOT:
        req.tenant_id = TenantID.TYPESPEC_CHANNEL_QA_BOT
    tenant = req.tenant_id.value
    logger.info(
        "Chat request: tenant=%s, conversation=%s, user=%s, message=%s",
        tenant,
        req.conversation_id,
        req.message.user_name or req.message.user_id,
        req.message.content[:200],
    )
    record_chat_request(tenant)
    start = time.perf_counter()
    try:
        resp = await _chat_service.chat(req)
        elapsed = time.perf_counter() - start
        record_chat_duration(tenant, elapsed, success=True)
        logger.info(
            "Chat response: %s",
            resp.model_dump_json(exclude={"full_context"}),
        )
        return resp
    except Exception:
        elapsed = time.perf_counter() - start
        record_chat_duration(tenant, elapsed, success=False)
        logger.error(
            "Chat failed: tenant=%s, conversation=%s, elapsed=%.2fs",
            tenant,
            req.conversation_id,
            elapsed,
            exc_info=True,
        )
        raise


@app.post(
    "/feedback", response_model=FeedbackResponse
)  # backwards compatibility for old endpoint
@app.post("/agent/feedback", response_model=FeedbackResponse)
async def handle_feedback(req: FeedbackRequest):
    """Process user feedback through the feedback workflow."""
    logger.info(
        "Feedback request: tenant=%s, link=%s, reaction=%s",
        req.tenant_id,
        req.link,
        req.reaction,
    )
    return await _feedback_service.process(req)


@app.post("/message/intention", response_model=IntentionResponse)
async def handle_intention(req: IntentionRequest):
    """Classify whether the bot should auto-reply to a message."""
    logger.info(
        "Intention request: conversation=%s, message=%s",
        req.conversation_id,
        req.message.content[:200],
    )
    return await _intention_service.classify(req)


@app.post("/conversation/save", response_model=SaveConversationMessageResponse)
async def save_conversation(req: ConversationMessage):
    """Save a conversation message and trigger background tenant memory update."""
    logger.info(
        "Save conversation request: tenant=%s, conversation=%s, message=%s",
        req.tenant_id,
        req.conversation_id,
        req.content[:200],
    )
    await _conversation_service.save_conversation(req)
    # Fire-and-forget background task to feed the thread to tenant memory
    BackgroundTaskTracker.instance().track(
        asyncio.create_task(_update_thread_memory(req))
    )
    return SaveConversationMessageResponse()

@app.post("/knowledge/retrieve", response_model=KnowledgeRetrieveResponse)
async def retrieve_knowledge(req: KnowledgeRetrieveRequest):
    """Retrieve knowledge for a request using search_knowledge_base tool."""
    logger.info(
        "Retrieve knowledge request: tenant=%s, message=%s",
        req.tenant_id,
        req.query[:200],
    )
    try:
        resp = await _knowledge_service.retrieve(req)
        logger.info(
            "Knowledge retrieval completed: tenant=%s, knowledge=%d",
            req.tenant_id,
            len(resp.knowledge_list) if resp.knowledge_list else 0,
        )
        return resp
    except Exception:
        logger.error(
            "Knowledge retrieval failed: tenant=%s",
            req.tenant_id,
            exc_info=True,
        )
        raise


async def _update_thread_memory(message: ConversationMessage) -> None:
    """Background task: query full thread and update tenant memory store."""
    try:
        thread_messages = await _conversation_service.get_thread_messages(message)
        await _thread_memory_service.process_thread_update(message, thread_messages)
    except Exception:
        logger.warning(
            "Background thread memory update failed for message=%s",
            message.id,
            exc_info=True,
        )


# --------------------------------------------------------------------------- #
# GraphRAG endpoints (all under /graph)
# --------------------------------------------------------------------------- #
# Authentication is delegated entirely to App Service EasyAuth (Entra ID)
# at the ingress. The expected caller is the chat-agent's Foundry-assigned
# Managed Identity, listed in the App Registration's allowed identities.
# Snapshot reloads are driven by the backend's own daily manifest poll
# (see lifespan ``_poll_graph_manifest``), so no admin reload endpoint is
# exposed.


# --------------------------------------------------------------------------- #
# Graph-query endpoint (called by chat_agent's search_knowledge_graph)
# --------------------------------------------------------------------------- #
# The chat agent runs in a fresh Foundry sandbox per session — every cold
# sandbox would otherwise pay ~40s to download parquets + preload community
# embeddings before serving the first graph query. Instead the chat agent
# POSTs here; the backend server's lifespan pre-warms the
# KnowledgeGraphService once at startup and re-uses it for the lifetime of
# the pod, so each call resolves in ~1-2s (one embedding + one AI Search
# ANN + DataFrame joins).


@app.post("/graph/query", response_model=GraphSearchResult)
async def graph_query(req: GraphQueryRequest) -> GraphSearchResult:
    """Run a GraphRAG Local-Search retrieval and return references.

    The body's ``query`` is run through the warm
    :class:`KnowledgeGraphService`. When ``tenant_id`` is present and
    maps to a known :class:`TenantConfig` with at least one
    :class:`KnowledgeSource`, retrieval is restricted to entities whose
    source documents belong to that tenant's ``KnowledgeSource`` set —
    mirroring how ``search_knowledge_base`` scopes its AI Search query.
    An unknown / empty ``tenant_id`` falls back to unscoped retrieval
    so legacy callers (and tenants without a source list) keep working.
    Returns an empty ``references`` list when the service is disabled,
    retrieval fails, or no matches are found — never raises 5xx for
    query-side failures so the chat agent can degrade gracefully.
    """
    from config.tenant_config import TenantID, get_tenant_config
    from utils.knowledge_graph import (
        get_knowledge_graph_service,
        parse_title_filter_terms,
    )

    normalised_query = (req.query or "").strip()
    if not normalised_query:
        return GraphSearchResult(references=[], query="")

    service = get_knowledge_graph_service()
    if not service.enabled:
        return GraphSearchResult(references=[], query=normalised_query)

    # Resolve the tenant to the same two-layer filter the KB tool applies:
    #   * source-folder layer — ``KnowledgeSource.name`` matches the
    #     ``raw_data.source_folder`` the sync project writes for every
    #     document (the KB tool's ``context_id eq '...'`` clause).
    #   * file (source_path) layer — per-source title filters from
    #     ``tenant_config.source_filter`` (the KB tool's
    #     ``search.ismatch(...,'title')`` clauses), translated into
    #     case-insensitive terms matched against each document's
    #     source_path.
    allowed_source_folders: set[str] | None = None
    source_path_filters: dict[str, list[str]] | None = None
    tenant_id_raw = (req.tenant_id or "").strip()
    if tenant_id_raw:
        try:
            tenant_enum = TenantID(tenant_id_raw)
        except ValueError:
            logger.warning(
                "Graph query: unknown tenant_id %r — running unscoped retrieval",
                tenant_id_raw,
            )
            tenant_enum = None
        if tenant_enum is not None:
            tenant_config = get_tenant_config(tenant_enum)
            if tenant_config and tenant_config.sources:
                allowed_source_folders = {
                    src.name for src in tenant_config.sources if src.name
                }
                source_path_filters = {
                    name: terms
                    for name, odata in tenant_config.source_filter.items()
                    if (terms := parse_title_filter_terms(odata))
                } or None

    try:
        sources: list[Reference] | None = await service.search_graph(
            normalised_query,
            allowed_source_folders=allowed_source_folders,
            source_path_filters=source_path_filters,
        )
    except Exception:
        logger.exception("Graph query failed for %r", normalised_query)
        return GraphSearchResult(references=[], query=normalised_query)

    if sources is None:
        return GraphSearchResult(references=[], query=normalised_query)

    # Dedupe by title; keep first occurrence (highest-ranked). Truncate
    # each snippet so the agent's prompt stays bounded.
    merged_refs: dict[str, Reference] = {}
    for src in sources:
        content = (src.content or "")[:_GRAPH_SNIPPET_MAX_CHARS]
        if src.content and len(src.content) > _GRAPH_SNIPPET_MAX_CHARS:
            content = content + "\n... [truncated]"
        ref = Reference(
            title=src.title,
            link=src.link,
            content=content,
            source=src.source or "graphrag",
        )
        merged_refs.setdefault(ref.title or ref.link, ref)

    return GraphSearchResult(
        references=list(merged_refs.values()),
        query=normalised_query,
    )


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8089)
