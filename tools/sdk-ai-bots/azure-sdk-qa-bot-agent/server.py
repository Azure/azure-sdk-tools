"""Azure SDK QA Bot — Backend Server.

The backend server that the Teams App communicates with.
Calls the hosted Chat Agent via the Azure AI Foundry SDK (azure-ai-projects)
and handles feedback through a local workflow.
"""

import asyncio
import contextlib
import logging
import sys
import time
from contextvars import ContextVar
from contextlib import asynccontextmanager
from uuid import uuid4
from dotenv import load_dotenv

load_dotenv(override=False)

from fastapi import FastAPI, Header, HTTPException, Request
from models.chat import ChatRequest, ChatResponse
from models.conversation import ConversationMessage, SaveConversationMessageResponse
from models.feedback import FeedbackRequest, FeedbackResponse
from models.intention import IntentionRequest, IntentionResponse
from services.chat_service import ChatService
from services.conversation_service import ConversationService
from services.feedback_service import FeedbackService
from services.intention_service import IntentionService
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
            await service.reload()
            status = service.get_status()
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

    try:
        yield
    finally:
        warm_task.cancel()
        with contextlib.suppress(asyncio.CancelledError, Exception):
            await warm_task
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
# GraphRAG admin endpoints
# --------------------------------------------------------------------------- #
# These let the knowledge-graph-sync project tell the bot "I've just
# published a fresh build — please reload from blob" without restarting
# the pod. See utils/knowledge_graph.py for the atomic swap mechanics.
#
# Auth: a shared secret header (``X-Admin-Token``) is checked against the
# ``GRAPHRAG_ADMIN_TOKEN`` value loaded from App Configuration.
# The sync job reads the same secret and includes it in the POST.


def _verify_admin_token(token: str | None) -> None:
    """Raise 401/503 if the supplied admin token doesn't match the
    configured ``GRAPHRAG_ADMIN_TOKEN``.

    Treats an empty configured secret as "endpoint disabled" (returns
    503) — never authorise unauthenticated reloads even by accident.
    """
    expected = app_config.get("GRAPHRAG_ADMIN_TOKEN", "")
    if not expected:
        raise HTTPException(
            status_code=503,
            detail="GraphRAG admin endpoint disabled (GRAPHRAG_ADMIN_TOKEN not configured)",
        )
    if not token or token != expected:
        raise HTTPException(status_code=401, detail="invalid admin token")


@app.post("/admin/graphrag/reload")
async def admin_graphrag_reload(
    x_admin_token: str | None = Header(default=None, alias="X-Admin-Token"),
):
    """Atomically reload the GraphRAG parquets from the configured blob source.

    In-flight Local Search queries keep their captured DataFrame snapshot and
    finish against the old data; subsequent queries see the new data.
    On failure the prior build remains active.
    """
    _verify_admin_token(x_admin_token)
    from utils.knowledge_graph import get_knowledge_graph_service

    service = get_knowledge_graph_service()
    if not service.enabled:
        raise HTTPException(
            status_code=409, detail="GraphRAG service is disabled (no source configured)"
        )
    try:
        status = await service.reload()
    except Exception as exc:
        logger.exception("GraphRAG reload failed")
        raise HTTPException(status_code=500, detail=f"reload failed: {exc}") from exc
    logger.info("GraphRAG reload succeeded: %s", status.get("version"))
    return status


@app.get("/admin/graphrag/status")
async def admin_graphrag_status(
    x_admin_token: str | None = Header(default=None, alias="X-Admin-Token"),
):
    """Return the currently-loaded GraphRAG build metadata."""
    _verify_admin_token(x_admin_token)
    from utils.knowledge_graph import get_knowledge_graph_service

    return get_knowledge_graph_service().get_status()


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8089)
