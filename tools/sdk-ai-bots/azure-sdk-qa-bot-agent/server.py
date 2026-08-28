"""Azure SDK QA Bot — Backend Server.

The backend server that the Teams App communicates with.
Calls the hosted Chat Agent via the Azure AI Foundry SDK (azure-ai-projects)
and handles feedback through a local workflow.
"""

import asyncio
import logging
import sys
import time
from contextvars import ContextVar
from contextlib import asynccontextmanager
from datetime import datetime
from pathlib import Path
from uuid import uuid4
from dotenv import load_dotenv

load_dotenv(override=False)

from fastapi import FastAPI, HTTPException, Query, Request
from fastapi.responses import FileResponse
from models.chat import ChatRequest, ChatResponse
from models.conversation import ConversationMessage, SaveConversationMessageResponse
from models.feedback import FeedbackRequest, FeedbackResponse
from models.intention import IntentionRequest, IntentionResponse
from models.knowledge_retrieve import KnowledgeRetrieveResponse, KnowledgeRetrieveRequest
from models.qa_dashboard import (
    FeedbackStatusFilter,
    QADashboardDetail,
    QARecordPage,
)
from models.qa_record import QAStatus
from services.chat_service import ChatService
from services.conversation_service import ConversationService
from services.feedback_service import FeedbackService
from services.intention_service import IntentionService
from services.knowledge_service import KnowledgeService
from services.qa_dashboard_service import QADashboardService
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
from utils.dashboard_identity import DashboardIdentity, get_dashboard_identity
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
    yield
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
_qa_dashboard_service = QADashboardService()
_thread_memory_service = ThreadMemoryService()
_QA_DASHBOARD_PATH = Path(__file__).parent / "static" / "qa_records_dashboard.html"


@app.get("/dashboard/qa-records", response_class=FileResponse)
async def qa_records_dashboard() -> FileResponse:
    """Serve the read-only QA record dashboard."""
    return FileResponse(_QA_DASHBOARD_PATH)


@app.get("/api/dashboard/me", response_model=DashboardIdentity)
async def get_dashboard_user(request: Request) -> DashboardIdentity:
    """Return display identity supplied by EasyAuth, if present."""
    return get_dashboard_identity(request.headers.get("x-ms-client-principal"))


@app.get("/api/dashboard/qa-records", response_model=QARecordPage)
async def list_dashboard_qa_records(
    page: int = Query(default=1, ge=1),
    tenant_id: str | None = Query(default=None, max_length=200),
    channel_id: str | None = Query(default=None, max_length=200),
    qa_status: QAStatus | None = None,
    feedback_status: FeedbackStatusFilter | None = None,
    updated_from: datetime | None = None,
    updated_to: datetime | None = None,
    conversation_id: str | None = Query(default=None, max_length=500),
) -> QARecordPage:
    """Return one filtered, server-paginated page of QA records."""
    try:
        return await _qa_dashboard_service.list_records(
            page=page,
            tenant_id=tenant_id,
            channel_id=channel_id,
            qa_status=qa_status,
            feedback_status=feedback_status,
            updated_from=updated_from,
            updated_to=updated_to,
            conversation_id=conversation_id,
        )
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc


@app.get("/api/dashboard/qa-record-details", response_model=QADashboardDetail)
async def get_dashboard_qa_record_details(
    record_id: str = Query(min_length=1, max_length=1000),
    tenant_id: str = Query(min_length=1, max_length=200),
) -> QADashboardDetail:
    """Return the complete conversation and evolution timeline."""
    detail = await _qa_dashboard_service.get_record_detail(
        record_id=record_id,
        tenant_id=tenant_id,
    )
    if detail is None:
        raise HTTPException(status_code=404, detail="QA record not found")
    return detail


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


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8089)
