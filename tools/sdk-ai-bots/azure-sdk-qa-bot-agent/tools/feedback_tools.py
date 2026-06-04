"""Hosted-agent tools for the Feedback Agent.

Implements the six tools called out in
``docs/feedback-agent-design.md`` §4. Each tool is registered via the
``@tool`` decorator from ``tools/__init__.py``.
"""

from __future__ import annotations

import logging
import re
from typing import Annotated, Any

import httpx
from pydantic import BaseModel, Field

from config.app_config import get as cfg
from models.conversation import ConversationType
from services.conversation_service import ConversationService
from tools import tool
from tools.github_mcp_tools import _get_github_token
from utils.azure_monitor_query import query_spans_by_response_id
from utils.knowledge_config import KbTarget, get_kb_target

logger = logging.getLogger(__name__)

# Caps to keep tool output token-bounded.
_MAX_TRACE_SPANS = 80
_MAX_TRACE_FIELD_CHARS = 400
_MAX_MESSAGES = 100
_MAX_MESSAGE_CHARS = 1500
_MAX_ISSUE_BODY_CHARS = 60_000  # GitHub max is 65_536


# ---------------------------------------------------------------------------
# Response models
# ---------------------------------------------------------------------------


class TraceCall(BaseModel):
    timestamp: str
    table: str
    name: str
    type: str
    duration_ms: float | None = None
    success: bool | None = None
    result_code: str | None = None
    data: str | None = None
    attributes: dict[str, Any] = Field(default_factory=dict)


class ChatTraceView(BaseModel):
    response_id: str
    found: bool
    span_count: int
    truncated: bool = False
    error: str | None = None
    calls: list[TraceCall] = Field(default_factory=list)


class FeedbackMessage(BaseModel):
    id: str
    role: str
    sender_name: str
    content: str
    created_at: str


class ConversationView(BaseModel):
    conversation_id: str
    conversation_type: str
    found: bool
    message_count: int
    truncated: bool = False
    messages: list[FeedbackMessage] = Field(default_factory=list)


class KbTargetView(BaseModel):
    folder: str
    resolved: bool
    fallback: bool = False
    owner: str | None = None
    repo: str | None = None
    branch: str | None = None
    path: str | None = None
    scope: str | None = None
    reason: str | None = None  # populated when resolved=False


class CreatedIssue(BaseModel):
    created: bool
    issue_url: str | None = None
    issue_number: int | None = None
    error: str | None = None


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _truncate(s: str | None, limit: int) -> str | None:
    if s is None:
        return None
    if len(s) <= limit:
        return s
    return s[:limit] + " …[truncated]"


# Coarse PII/identifier redaction applied to issue bodies.
_EMAIL_RE = re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")
_AAD_OID_RE = re.compile(
    r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b"
)
_UPN_RE = re.compile(r"\b[A-Za-z0-9._-]+#EXT#@[A-Za-z0-9.-]+\b")


def _redact(body: str) -> str:
    body = _EMAIL_RE.sub("[redacted-email]", body)
    body = _UPN_RE.sub("[redacted-upn]", body)
    body = _AAD_OID_RE.sub("[redacted-id]", body)
    return body


def _resolve_conversation_type(value: str) -> ConversationType:
    try:
        return ConversationType(value)
    except ValueError as exc:
        valid = ", ".join(t.value for t in ConversationType)
        raise ValueError(
            f"Unknown conversation_type '{value}'. Valid values: {valid}"
        ) from exc


# ---------------------------------------------------------------------------
# FeedbackTools
# ---------------------------------------------------------------------------


class FeedbackTools:
    """Tools surfaced to the hosted feedback agent.

    Note: ``search_knowledge_base`` and ``web_fetch`` are reused unchanged
    from the existing tool modules; bind them on the agent ``tools`` list
    rather than wrapping them here.
    """

    def __init__(
        self,
        *,
        conversation_service: ConversationService | None = None,
    ) -> None:
        self._conversations = conversation_service or ConversationService()

    @tool
    async def fetch_chat_trace(
        self,
        *,
        response_id: Annotated[
            str,
            "The bot response id (parsed from the assistant ConversationMessage "
            "id, format `bot-{response_id}`). Used to look up matching App "
            "Insights spans emitted by the chat agent.",
        ],
    ) -> ChatTraceView:
        """Fetch normalized chat-agent spans for a given ``response_id``.

        Returns an ordered list of tool calls / requests / log records.
        On App Insights ingestion lag or empty results, ``found=False`` and
        the agent should return a structured ``trace_unavailable`` outcome.
        """
        if not response_id:
            return ChatTraceView(
                response_id="",
                found=False,
                span_count=0,
                error="response_id is empty",
            )
        try:
            spans = await query_spans_by_response_id(response_id)
        except Exception as exc:  # defensive — query_spans already handles
            logger.exception("fetch_chat_trace failed for %s", response_id)
            return ChatTraceView(
                response_id=response_id,
                found=False,
                span_count=0,
                error=f"query_failed: {exc}",
            )

        if not spans:
            return ChatTraceView(
                response_id=response_id,
                found=False,
                span_count=0,
                error="no_spans_found",
            )

        truncated = len(spans) > _MAX_TRACE_SPANS
        clipped = spans[:_MAX_TRACE_SPANS]
        calls = [
            TraceCall(
                timestamp=s.timestamp,
                table=s.table,
                name=s.name,
                type=s.type,
                duration_ms=s.duration_ms,
                success=s.success,
                result_code=s.result_code,
                data=_truncate(s.data, _MAX_TRACE_FIELD_CHARS),
                attributes=s.custom_dimensions,
            )
            for s in clipped
        ]
        return ChatTraceView(
            response_id=response_id,
            found=True,
            span_count=len(spans),
            truncated=truncated,
            calls=calls,
        )

    @tool
    async def fetch_conversation(
        self,
        *,
        conversation_id: Annotated[str, "Customer conversation id."],
        conversation_type: Annotated[
            str,
            "Customer conversation type (e.g. 'teams_channel').",
        ],
    ) -> ConversationView:
        """Return all messages in a conversation, ordered by created_at."""
        try:
            ctype = _resolve_conversation_type(conversation_type)
        except ValueError:
            return ConversationView(
                conversation_id=conversation_id,
                conversation_type=conversation_type,
                found=False,
                message_count=0,
                messages=[],
            )

        items = await self._conversations.get_messages_by_conversation(
            conversation_id=conversation_id,
            conversation_type=ctype,
        )

        truncated = len(items) > _MAX_MESSAGES
        clipped = items[:_MAX_MESSAGES]
        messages = [
            FeedbackMessage(
                id=m.id,
                role=m.sender_role.value,
                sender_name=m.sender_name,
                content=_truncate(m.content, _MAX_MESSAGE_CHARS) or "",
                created_at=m.created_at.isoformat() if m.created_at else "",
            )
            for m in clipped
        ]
        return ConversationView(
            conversation_id=conversation_id,
            conversation_type=conversation_type,
            found=bool(items),
            message_count=len(items),
            truncated=truncated,
            messages=messages,
        )

    @tool
    async def resolve_kb_target(
        self,
        *,
        folder: Annotated[
            str,
            "The chunk `source` value from a knowledge-base hit — used as "
            "the join key into the upstream knowledge-config.json.",
        ],
    ) -> KbTargetView:
        """Map a KB folder to its GitHub issue target.

        Falls back to ``FEEDBACK_DEFAULT_KB_GITHUB_REPO`` when the folder
        is unknown or maps to a non-GitHub repository (ADO, SSH, wiki).
        """
        target: KbTarget | None = None
        try:
            target = await get_kb_target(folder)
        except Exception:
            logger.exception("knowledge_config lookup failed for %s", folder)

        if target is not None:
            return KbTargetView(
                folder=folder,
                resolved=True,
                owner=target.owner,
                repo=target.repo,
                branch=target.branch,
                path=target.path,
                scope=target.scope,
            )

        fallback = (cfg("FEEDBACK_DEFAULT_KB_GITHUB_REPO", "") or "").strip()
        if fallback and "/" in fallback:
            owner, repo = fallback.split("/", 1)
            return KbTargetView(
                folder=folder,
                resolved=True,
                fallback=True,
                owner=owner,
                repo=repo,
                branch="main",
                path="",
                scope=folder,
                reason="folder_unmapped_or_non_github",
            )

        return KbTargetView(
            folder=folder,
            resolved=False,
            reason=(
                "folder_unmapped_or_non_github; "
                "FEEDBACK_DEFAULT_KB_GITHUB_REPO not configured"
            ),
        )

    @tool
    async def create_kb_gap_issue(
        self,
        *,
        owner: Annotated[str, "GitHub repository owner (org or user)."],
        repo: Annotated[str, "GitHub repository name."],
        title: Annotated[str, "Issue title (<= 256 chars recommended)."],
        body: Annotated[str, "Issue body (Markdown). Will be PII-redacted."],
        labels: Annotated[
            list[str] | None,
            "Issue labels. Typically `['kb-gap', 'tenant:<id>', "
            "'classification:<class>']`.",
        ] = None,
    ) -> CreatedIssue:
        """Create a GitHub issue in the target KB repo.

        Uses the same GitHub App installation token as the chat agent's
        GitHub MCP tool (``_get_github_token``); the App must have
        ``issues:write`` on the target repo.
        """
        if not owner or not repo:
            return CreatedIssue(created=False, error="owner/repo are required")

        try:
            token, _ = await _get_github_token()
        except Exception as exc:
            logger.exception("Failed to acquire GitHub token for issue create")
            return CreatedIssue(created=False, error=f"token_acquire_failed: {exc}")

        safe_body = _truncate(_redact(body), _MAX_ISSUE_BODY_CHARS)
        payload: dict[str, Any] = {"title": title, "body": safe_body}
        if labels:
            payload["labels"] = labels

        url = f"https://api.github.com/repos/{owner}/{repo}/issues"
        headers = {
            "Accept": "application/vnd.github+json",
            "Authorization": f"Bearer {token}",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "azure-sdk-qa-bot-feedback-agent",
        }
        try:
            async with httpx.AsyncClient(timeout=15) as client:
                resp = await client.post(url, headers=headers, json=payload)
        except Exception as exc:
            logger.exception("GitHub issue create request failed")
            return CreatedIssue(created=False, error=f"http_error: {exc}")

        if resp.status_code >= 400:
            return CreatedIssue(
                created=False,
                error=f"github_{resp.status_code}: {resp.text[:300]}",
            )

        data = resp.json()
        return CreatedIssue(
            created=True,
            issue_url=data.get("html_url"),
            issue_number=data.get("number"),
        )


__all__ = [
    "FeedbackTools",
    "ChatTraceView",
    "ConversationView",
    "KbTargetView",
    "CreatedIssue",
]
