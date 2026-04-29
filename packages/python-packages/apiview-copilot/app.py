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
from typing import Literal, Optional

from azure.monitor.opentelemetry import configure_azure_monitor
from azure.cosmos.exceptions import CosmosResourceNotFoundError
from fastapi import Depends, FastAPI, HTTPException
from pydantic import BaseModel, Field
from src._apiview import resolve_package
from src._apiview_reviewer import SUPPORTED_LANGUAGES, ApiViewReview
from src._auth import AppRole, require_roles
from src._database_manager import DatabaseManager
from src._diff import create_diff_with_line_numbers
from src._github_manager import GithubManager
from src._mention import handle_mention_request
from src._settings import SettingsManager
from src._thread_resolution import handle_thread_resolution_request
from src._prompt_runner import run_prompt
from src._utils import get_language_pretty_name
from src.agent._agent import get_readonly_agent, get_readwrite_agent, invoke_agent
from src.mention._github_issue_helpers import create_issue as create_github_issue

# How long to keep completed jobs (seconds)
JOB_RETENTION_SECONDS = 1800  # 30 minutes
db_manager = DatabaseManager.get_instance()
settings = SettingsManager()

# Application Insights telemetry — enabled when connection string is available
_appinsights_conn_str = os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING")
if _appinsights_conn_str:
    configure_azure_monitor(connection_string=_appinsights_conn_str)

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
    target_id: Optional[str] = Field(None, alias="targetId")

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True


class ApiReviewJobStatusResponse(BaseModel):
    """Response model for API review job status."""

    status: ApiReviewJobStatus
    comments: list = None
    details: str = None


class ApiReviewJobStartResponse(BaseModel):
    """Response model for starting an API review job."""

    job_id: str = Field(..., alias="jobId")

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True


@app.post("/api-review/start", status_code=202, response_model=ApiReviewJobStartResponse)
async def submit_api_review_job(
    job_request: ApiReviewJobRequest,
    _claims=Depends(require_roles(AppRole.WRITER, AppRole.APP_WRITER)),
):
    """Submit a new API review job."""
    # Validate language
    if job_request.language not in SUPPORTED_LANGUAGES:
        raise HTTPException(status_code=400, detail=f"Unsupported language `{job_request.language}`")

    try:
        reviewer = ApiViewReview(
            language=job_request.language,
            target=job_request.target,
            base=job_request.base,
            outline=job_request.outline,
            comments=job_request.comments,
        )
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e)) from e
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
    return ApiReviewJobStartResponse(job_id=job_id)


@app.get("/api-review/{job_id}", response_model=ApiReviewJobStatusResponse)
async def get_api_review_job_status(
    job_id: str,
    _claims=Depends(require_roles(AppRole.READER, AppRole.APP_READER)),
):
    """Get the status of an API review job."""
    try:
        job = db_manager.review_jobs.get(job_id)
        return job
    except CosmosResourceNotFoundError as exc:
        raise HTTPException(status_code=404, detail=f"Job with id {html.escape(str(job_id))} not found") from exc


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

    user_input: str = Field(..., alias="userInput")
    thread_id: Optional[str] = Field(None, alias="threadId")
    messages: list = None  # Optional: for multi-turn

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True


class AgentChatResponse(BaseModel):
    """Response model for agent chat interaction."""

    response: str
    thread_id: str = Field(..., alias="threadId")
    messages: list

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True


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
    except Exception as e:
        if "Rate limit is exceeded" in str(e):
            logger.warning("Rate limit exceeded: %s", e)
            raise HTTPException(status_code=429, detail="Rate limit exceeded. Please wait and try again.") from e
        logger.error("Error in /agent/chat: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail=f"Agent error: {e}") from e


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
        summary = await asyncio.to_thread(run_prompt, folder="summarize", filename=summary_prompt_file, inputs=inputs)
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
    source_comment_id: Optional[str] = Field(None, alias="sourceCommentId")

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
            source_comment_id=request.source_comment_id,
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
            source_comment_id=request.source_comment_id,
        )
        return AgentChatResponse(
            response=response, thread_id="", messages=[]  # No thread ID for this endpoint  # No messages to return
        )
    except HTTPException as e:
        logger.error("Error in /api-review/resolve: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error") from e


class ResolvePackageRequest(BaseModel):
    """Request model for resolving package information."""

    package_query: str = Field(..., alias="packageQuery")
    language: str
    version: Optional[str] = None
    environment: Literal["production", "staging"] = "production"

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True


class ResolvePackageResponse(BaseModel):
    """Response model for resolved package information."""

    package_name: str = Field(..., alias="packageName")
    review_id: str = Field(..., alias="reviewId")
    language: str
    version: Optional[str] = None
    revision_id: Optional[str] = Field(None, alias="revisionId")
    revision_label: Optional[str] = Field(None, alias="revisionLabel")

    class Config:
        """Configuration for Pydantic model."""

        populate_by_name = True


@app.post("/api-review/resolve-package", response_model=ResolvePackageResponse)
async def resolve_package_info(
    request: ResolvePackageRequest,
    _claims=Depends(require_roles(AppRole.READER, AppRole.APP_READER)),
):
    """Resolve package information from a package description and language."""
    logger.info(
        "Received /api-review/resolve-package request: package_query=%s, language=%s, version=%s",
        request.package_query,
        request.language,
        request.version,
    )
    try:
        result = await asyncio.to_thread(
            resolve_package,
            package_query=request.package_query,
            language=request.language,
            version=request.version,
            environment=request.environment,
        )

        if not result:
            raise HTTPException(status_code=404, detail="Package not found")

        if "error" in result:
            if result["error"] == "no_packages_for_language":
                raise HTTPException(status_code=404, detail=f"No packages found for language: {result['language']}")
            raise HTTPException(status_code=404, detail="Package not found")

        return ResolvePackageResponse(**result)
    except HTTPException:
        raise
    except Exception as e:
        logger.error("Error in /api-review/resolve-package: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error") from e


REPORT_ISSUE_REPO = "azure-sdk-tools"


def _get_report_issue_owner() -> str:
    return "Azure" if os.getenv("ENVIRONMENT_NAME") == "production" else "tjprescott"


class CommentContextRequest(BaseModel):
    """Optional context about the comment that triggered the issue report."""

    comment_id: Optional[str] = Field(None, alias="commentId")
    comment_text: Optional[str] = Field(None, alias="commentText", max_length=10000)
    code_snippet: Optional[str] = Field(None, alias="codeSnippet", max_length=10000)
    language: Optional[str] = None
    element_id: Optional[str] = Field(None, alias="elementId")
    comment_source: Optional[Literal["copilot", "apiview"]] = Field(None, alias="commentSource")

    class Config:
        populate_by_name = True


class ReportIssueRequest(BaseModel):
    """Request model for reporting an issue from APIView."""

    mode: Literal["general", "comment"]
    description: str = Field(..., min_length=1, max_length=5000)
    review_link: Optional[str] = Field(None, alias="reviewLink")
    comment_context: Optional[CommentContextRequest] = Field(None, alias="commentContext")

    class Config:
        populate_by_name = True


class ReportIssueResponse(BaseModel):
    """Response model for a reported issue."""

    issue_url: str = Field(..., alias="issueUrl")
    issue_number: int = Field(..., alias="issueNumber")

    class Config:
        populate_by_name = True


def _is_copilot_comment(request: ReportIssueRequest) -> bool:
    """True when the request is a comment-mode report against an APIView Copilot comment."""
    if request.mode != "comment" or not request.comment_context:
        return False
    return request.comment_context.comment_source == "copilot"


def _build_report_issue_title(request: ReportIssueRequest) -> str:
    """Build a fallback GitHub issue title when the LLM does not provide one."""
    if request.mode == "comment":
        source_label = "APIView Copilot" if _is_copilot_comment(request) else "APIView"
        return f"[APIView] Issue with {source_label} comment"
    snippet = request.description[:80].replace("\n", " ").strip()
    if len(request.description) > 80:
        snippet += "..."
    return f"[APIView] {snippet}"


def _get_report_issue_labels(request: ReportIssueRequest) -> list[str]:
    """Determine labels for a reported issue."""
    return ["APIView Copilot"] if _is_copilot_comment(request) else ["APIView"]


def _build_comment_context_text(ctx: CommentContextRequest) -> str:
    """Format comment context as plain text for the LLM prompt."""
    parts = []
    if ctx.comment_source:
        parts.append(f"Source: {ctx.comment_source}")
    if ctx.language:
        parts.append(f"Language: {ctx.language}")
    if ctx.comment_text:
        parts.append(f"Comment: {ctx.comment_text}")
    if ctx.code_snippet:
        parts.append(f"Code Snippet: {ctx.code_snippet}")
    if ctx.element_id:
        parts.append(f"Element ID: {ctx.element_id}")
    return "\n".join(parts)


def _generate_report_issue(request: ReportIssueRequest) -> dict:
    """Generate a GitHub issue title and body, using the LLM when possible.

    The LLM produces a concise title and optional ``next_steps`` text describing
    suggested investigation actions. The user's description is always preserved
    verbatim by ``_assemble_report_issue_body``. Falls back to a template title
    if the LLM call fails or returns an empty title.
    """
    title: str | None = None
    next_steps: str | None = None
    try:
        inputs = {
            "mode": request.mode,
            "description": request.description,
            "review_link": request.review_link or "",
            "comment_context": (
                _build_comment_context_text(request.comment_context) if request.comment_context else ""
            ),
        }
        raw = run_prompt(folder="report_issue", filename="generate_issue.prompty", inputs=inputs)
        result = json.loads(raw)
        title = (result.get("title") or "").strip() or None
        next_steps = (result.get("body") or "").strip() or None
        if not title:
            logger.warning("LLM returned empty title; falling back to template.")
    except Exception as e:
        logger.warning("LLM generation failed; falling back to template: %s", e)

    if not title:
        title = _build_report_issue_title(request)

    body = _assemble_report_issue_body(request, next_steps)
    return {"title": title, "body": body}


def _assemble_report_issue_body(request: ReportIssueRequest, next_steps: str | None) -> str:
    """Assemble the full issue body from request details and optional LLM-generated next steps."""
    sections: list[str] = []

    if request.review_link:
        sections.append(f"## Review Link\n\n{request.review_link}")

    sections.append(f"## Description\n\n{request.description}")

    if request.comment_context:
        ctx = request.comment_context
        parts = ["## Comment Context"]
        if ctx.comment_source:
            parts.append(f"**Source:** {ctx.comment_source}")
        if ctx.language:
            parts.append(f"**Language:** {ctx.language}")
        if ctx.comment_text:
            parts.append(f"**Comment:**\n> {ctx.comment_text}")
        if ctx.code_snippet:
            parts.append(f"**Code Snippet:**\n```\n{ctx.code_snippet}\n```")
        if ctx.element_id:
            parts.append(f"**Element ID:** `{ctx.element_id}`")
        sections.append("\n".join(parts))

    if next_steps:
        sections.append(f"## Suggested Next Steps\n\n{next_steps}")

    sections.append("---\n*Reported via APIView*")
    return "\n\n".join(sections)


@app.post("/api-review/report-issue", response_model=ReportIssueResponse)
async def report_issue(
    request: ReportIssueRequest,
    _claims=Depends(require_roles(AppRole.WRITER, AppRole.APP_WRITER)),
):
    """Report an issue from APIView UI. Creates a GitHub issue with context."""
    logger.info(
        "Received /api-review/report-issue request: mode=%s",
        request.mode,
    )
    try:
        issue_content = await asyncio.to_thread(_generate_report_issue, request)
        labels = _get_report_issue_labels(request)

        client = GithubManager.get_instance()
        issue = await asyncio.to_thread(
            create_github_issue,
            client,
            owner=_get_report_issue_owner(),
            repo=REPORT_ISSUE_REPO,
            title=issue_content["title"],
            body=issue_content["body"],
            workflow_tag="report-issue",
            source_tag="APIView",
            labels=labels,
        )
        return ReportIssueResponse(issue_url=issue["html_url"], issue_number=issue["number"])
    except Exception as e:
        logger.error("Error in /api-review/report-issue: %s", e, exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error") from e


# Start the cleanup thread when the app starts
cleanup_thread = threading.Thread(target=cleanup_job_store, daemon=True)
cleanup_thread.start()
