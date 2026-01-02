# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
FastAPI application for APIView Copilot.
"""

import asyncio
import html
import json
import logging
import os
import threading
import time
from enum import Enum

from azure.cosmos.exceptions import CosmosResourceNotFoundError
from fastapi import Depends, FastAPI, HTTPException
from pydantic import BaseModel, Field
from semantic_kernel.exceptions.agent_exceptions import AgentInvokeException
from src._apiview_reviewer import SUPPORTED_LANGUAGES, ApiViewReview
from src._auth import AppRole, require_roles
from src._database_manager import DatabaseManager
from src._diff import create_diff_with_line_numbers
from src._mention import handle_mention_request
from src._settings import SettingsManager
from src._thread_resolution import handle_thread_resolution_request
from src._utils import get_language_pretty_name, run_prompty
from src.agent._agent import get_readonly_agent, get_readwrite_agent, invoke_agent

# How long to keep completed jobs (seconds)
JOB_RETENTION_SECONDS = 1800  # 30 minutes
db_manager = DatabaseManager.get_instance()
settings = SettingsManager()

app = FastAPI(openapi_url=None, docs_url=None, redoc_url=None)

logger = logging.getLogger("uvicorn")  # Use Uvicorn's logger


_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__)))
error_log_file = os.path.join(_PACKAGE_ROOT, "error.log")


# pylint: disable=invalid-name
class ApiReviewJobStatus(str, Enum):
    """Enumeration for API review job statuses."""

    InProgress = "InProgress"
    Success = "Success"
    Error = "Error"


class ApiReviewJobRequest(BaseModel):
    """Request model for starting an API review job."""

    language: str
    target: str
    base: str = None
    outline: str = None
    comments: list = None
    target_id: str = None


class ApiReviewJobStatusResponse(BaseModel):
    """Response model for API review job status."""

    status: ApiReviewJobStatus
    comments: list = None
    details: str = None


@app.post("/api-review/start", status_code=202)
async def submit_api_review_job(
    job_request: ApiReviewJobRequest,
    _claims=Depends(require_roles(AppRole.WRITER, AppRole.APP_WRITER)),
):
    """Submit a new API review job."""
    # Validate language
    if job_request.language not in SUPPORTED_LANGUAGES:
        raise HTTPException(status_code=400, detail=f"Unsupported language `{job_request.language}`")

    reviewer = ApiViewReview(
        language=job_request.language,
        target=job_request.target,
        base=job_request.base,
        outline=job_request.outline,
        comments=job_request.comments,
    )
    job_id = reviewer.job_id
    db_manager.review_jobs.create(job_id, data={"status": ApiReviewJobStatus.InProgress, "finished": None})

    async def run_review_job():
        try:
            loop = asyncio.get_running_loop()
            # Run the blocking review in a thread pool executor
            result = await loop.run_in_executor(None, reviewer.run)
            reviewer.close()
            # Parse comments from result
            result_json = json.loads(result.model_dump_json())
            comments = result_json.get("comments", [])

            now = time.time()
            db_manager.review_jobs.upsert(
                job_id, data={"status": ApiReviewJobStatus.Success, "comments": comments, "finished": now}
            )
        except Exception as e:
            now = time.time()
            db_manager.review_jobs.upsert(
                job_id, data={"status": ApiReviewJobStatus.Error, "details": str(e), "finished": now}
            )

    # Schedule the job in the background
    asyncio.create_task(run_review_job())
    return {"job_id": job_id}


@app.get("/api-review/{job_id}", response_model=ApiReviewJobStatusResponse)
async def get_api_review_job_status(
    job_id: str,
    _claims=Depends(require_roles(AppRole.READER, AppRole.APP_READER)),
):
    """Get the status of an API review job."""
    try:
        job = db_manager.review_jobs.get(job_id)
        return job
    except CosmosResourceNotFoundError:
        raise HTTPException(status_code=404, detail=f"Job with id {html.escape(str(job_id))} not found")


@app.get("/auth-test")
async def auth_test(
    _claims=Depends(require_roles(AppRole.READER, AppRole.APP_READER)),
):
    """Test endpoint to verify authentication is working."""
    return {"status": "ok"}


@app.get("/health-test")
async def health_check():
    """Health check endpoint to verify service is running."""
    return {"status": "ok"}


def cleanup_job_store():
    """Cleanup completed jobs from the Cosmos DB periodically."""
    while True:
        time.sleep(60)  # Run every 60 seconds
        db_manager.review_jobs.cleanup_old_jobs(JOB_RETENTION_SECONDS)


class AgentChatRequest(BaseModel):
    """Request model for agent chat interaction."""

    user_input: str
    thread_id: str = None
    messages: list = None  # Optional: for multi-turn


class AgentChatResponse(BaseModel):
    """Response model for agent chat interaction."""

    response: str
    thread_id: str
    messages: list


@app.post("/agent/chat", response_model=AgentChatResponse)
async def agent_chat(
    request: AgentChatRequest,
    _claims=Depends(require_roles(AppRole.READER, AppRole.APP_READER, AppRole.WRITER, AppRole.APP_WRITER)),
):
    """Handle chat requests to the agent."""
    logger.info("Received /agent/chat request: user_input=%s, thread_id=%s", request.user_input, request.thread_id)
    try:
        roles = _claims.get("roles", [])
        if isinstance(roles, str):
            roles = roles.split()
        token_roles = set(roles)

        is_writer = (AppRole.WRITER.value in token_roles) or (AppRole.APP_WRITER.value in token_roles)
        agent_factory = get_readwrite_agent if is_writer else get_readonly_agent

        with agent_factory() as (client, agent_id):
            response, thread_id_out, messages = await invoke_agent(
                client=client,
                agent_id=agent_id,
                user_input=request.user_input,
                thread_id=request.thread_id,
                messages=request.messages,
            )
        return AgentChatResponse(response=response, thread_id=thread_id_out, messages=messages)
    except AgentInvokeException as e:
        if "Rate limit is exceeded" in str(e):
            logger.warning("Rate limit exceeded: %s", e)
            raise HTTPException(status_code=429, detail="Rate limit exceeded. Please wait and try again.") from e
        logger.error("AgentInvokeException: %s", e)
        raise HTTPException(status_code=500, detail=f"Agent error: {e}") from e
    except Exception as e:
        logger.error("Error in /agent/chat: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error") from e


class SummarizeRequest(BaseModel):
    """Request model for summarizing API changes."""

    language: str
    target: str
    base: str = None


class SummarizeResponse(BaseModel):
    """Response model for summarizing API changes."""

    summary: str


@app.post("/api-review/summarize", response_model=SummarizeResponse)
async def summarize_api(
    request: SummarizeRequest,
    _claims=Depends(require_roles(AppRole.READER, AppRole.APP_READER)),
):
    """Summarize API changes based on the provided request."""
    if request.language not in SUPPORTED_LANGUAGES:
        raise HTTPException(status_code=400, detail=f"Unsupported language `{request.language}`")
    try:
        if request.base:
            summary_prompt_file = "summarize_diff.prompty"
            summary_content = create_diff_with_line_numbers(old=request.base, new=request.target)
        else:
            summary_prompt_file = "summarize_api.prompty"
            summary_content = request.target

        pretty_language = get_language_pretty_name(request.language)
        inputs = {"language": pretty_language, "content": summary_content}
        summary = await asyncio.to_thread(run_prompty, folder="summarize", filename=summary_prompt_file, inputs=inputs)
        return SummarizeResponse(summary=summary)
    except Exception as e:
        logger.error("Error in /api-review/summarize: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error") from e


class MentionRequest(BaseModel):
    """Request model for handling mentions in API reviews."""

    comments: list
    language: str
    package_name: str = Field(..., alias="packageName")
    code: str

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True


@app.post("/api-review/mention", response_model=AgentChatResponse)
async def handle_mention(
    request: MentionRequest,
    _claims=Depends(require_roles(AppRole.WRITER, AppRole.APP_WRITER)),
):
    """Handle mentions in API reviews."""
    logger.info(
        "Received /api-review/mention request: language=%s, package_name=%s, comments_count=%d",
        request.language,
        request.package_name,
        len(request.comments) if request.comments else 0,
    )
    try:
        pretty_language = get_language_pretty_name(request.language)
        response = handle_mention_request(
            comments=request.comments,
            language=pretty_language,
            package_name=request.package_name,
            code=request.code,
        )
        return AgentChatResponse(
            response=response, thread_id="", messages=[]  # No thread ID for this endpoint  # No messages to return
        )
    except HTTPException as e:
        logger.error("Error in /api-review/mention: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error") from e


@app.post("/api-review/resolve", response_model=AgentChatResponse)
async def handle_thread_resolution(
    request: MentionRequest,
    _claims=Depends(require_roles(AppRole.WRITER, AppRole.APP_WRITER)),
):
    """Handle thread resolution in API reviews."""
    logger.info(
        "Received /api-review/resolve request: language=%s, package_name=%s, comments_count=%d",
        request.language,
        request.package_name,
        len(request.comments) if request.comments else 0,
    )
    try:
        pretty_language = get_language_pretty_name(request.language)
        response = handle_thread_resolution_request(
            comments=request.comments,
            language=pretty_language,
            package_name=request.package_name,
            code=request.code,
        )
        return AgentChatResponse(
            response=response, thread_id="", messages=[]  # No thread ID for this endpoint  # No messages to return
        )
    except HTTPException as e:
        logger.error("Error in /api-review/resolve: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error") from e


# Start the cleanup thread when the app starts
cleanup_thread = threading.Thread(target=cleanup_job_store, daemon=True)
cleanup_thread.start()
