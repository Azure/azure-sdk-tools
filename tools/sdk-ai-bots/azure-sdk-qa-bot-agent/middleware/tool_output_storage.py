"""Offload large opaque tool outputs to Foundry Files for sandboxed shell inspection."""

from __future__ import annotations

import io
import json
import logging
import re
from collections.abc import Awaitable, Callable
from contextvars import ContextVar, Token
from typing import Any
from uuid import uuid4

from agent_framework import (
    AgentSession,
    ChatContext,
    ChatMiddleware,
    Content,
    ContextProvider,
    FunctionInvocationContext,
    FunctionMiddleware,
    SessionContext,
)

from tools import TOOL_REGISTRY
from tools.github_mcp_tools import _is_github_mcp_tool, _redact_untrusted_authors

logger = logging.getLogger(__name__)

_OPAQUE_RESULT_OFFLOAD_CHARS = 20_000
_LARGE_RESULT_PREVIEW_CHARS = 2_000
_MAX_SESSION_FILES = 20
_SESSION_FILES_STATE_KEY = "offloaded_file_ids"
_SKIP_TOOLS = {"load_skill", "read_skill_resource", "run_skill_script"}

_session_file_ids: ContextVar[list[str] | None] = ContextVar(
    "offloaded_file_ids",
    default=None,
)


def _safe_name(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]", "_", value)


def _get_text(result: object) -> str | None:
    if isinstance(result, str):
        return result
    if isinstance(result, list) and all(
        isinstance(item, Content) and item.type == "text" for item in result
    ):
        return "\n".join(item.text or "" for item in result)
    return None


def _format_for_storage(text: str) -> str:
    """Pretty-print JSON so line-based shell tools can search it cleanly."""
    stripped = text.lstrip()
    if stripped.startswith(("{", "[")):
        try:
            return json.dumps(json.loads(text), indent=2, ensure_ascii=False)
        except (ValueError, TypeError):
            pass
    return text


class OffloadedFilesProvider(ContextProvider):
    """Bind per-session offloaded file IDs across turns and concurrent requests."""

    source_id = "offloaded_files"

    def __init__(self) -> None:
        super().__init__(source_id=self.source_id)
        self._tokens: ContextVar[Token[list[str] | None] | None] = ContextVar(
            "offloaded_file_ids_token",
            default=None,
        )

    async def before_run(
        self,
        *,
        agent: Any,
        session: AgentSession,
        context: SessionContext,
        state: dict[str, Any],
    ) -> None:
        file_ids = state.setdefault("file_ids", [])
        if not isinstance(file_ids, list):
            file_ids = []
            state["file_ids"] = file_ids
        self._tokens.set(_session_file_ids.set(file_ids))

    async def after_run(
        self,
        *,
        agent: Any,
        session: AgentSession,
        context: SessionContext,
        state: dict[str, Any],
    ) -> None:
        token = self._tokens.get()
        if token is not None:
            _session_file_ids.reset(token)
            self._tokens.set(None)


class ShellFilesMiddleware(ChatMiddleware):
    """Attach session offloaded file_ids to the hosted container_auto shell tool."""

    async def process(
        self,
        context: ChatContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        file_ids = _session_file_ids.get()
        tools = context.options.get("tools")
        if file_ids and isinstance(tools, list):
            bounded_ids = list(file_ids[-_MAX_SESSION_FILES:])
            updated_tools = []
            for tool in tools:
                if isinstance(tool, dict) and tool.get("type") == "shell":
                    env = dict(tool.get("environment") or {"type": "container_auto"})
                    env["file_ids"] = bounded_ids
                    updated_tools.append({**tool, "environment": env})
                else:
                    updated_tools.append(tool)
            context.options["tools"] = updated_tools
        await call_next()


class ToolOutputStorageMiddleware(FunctionMiddleware):
    """Upload large opaque tool results to Foundry Files for shell inspection."""

    def __init__(self, openai_client: Any) -> None:
        self.openai_client = openai_client

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        await call_next()

        name = context.function.name
        if name in TOOL_REGISTRY or name in _SKIP_TOOLS:
            return

        result = _get_text(context.result)
        if not result:
            return

        if _is_github_mcp_tool(name):
            result = _redact_untrusted_authors(result)
        context.result = result
        if len(result) <= _OPAQUE_RESULT_OFFLOAD_CHARS or context.session is None:
            return

        file_name = f"tool-result-{_safe_name(name)}-{uuid4().hex}.txt"
        stored_text = _format_for_storage(result)
        try:
            uploaded = await self.openai_client.files.create(
                file=(file_name, io.BytesIO(stored_text.encode("utf-8"))),
                purpose="assistants",
            )
        except Exception:
            logger.warning(
                "Failed to upload large tool result (%s, %d chars); returning preview",
                name,
                len(result),
                exc_info=True,
            )
            context.result = (
                f"Large tool result ({len(result):,} characters) could not be uploaded; "
                f"showing preview:\n\n{result[:_LARGE_RESULT_PREVIEW_CHARS]}"
            )
            return

        active_ids = _session_file_ids.get()
        if active_ids is not None:
            active_ids.append(uploaded.id)
            if len(active_ids) > _MAX_SESSION_FILES:
                del active_ids[:-_MAX_SESSION_FILES]
        else:
            session_ids = context.session.state.setdefault(_SESSION_FILES_STATE_KEY, [])
            if isinstance(session_ids, list):
                session_ids.append(uploaded.id)
                if len(session_ids) > _MAX_SESSION_FILES:
                    del session_ids[:-_MAX_SESSION_FILES]

        logger.info(
            "Uploaded large tool result: tool=%s file=%s file_id=%s chars=%d",
            name,
            file_name,
            uploaded.id,
            len(result),
        )
        context.result = (
            f"Large tool result stored as `{file_name}` ({len(result):,} characters) "
            "in your shell sandbox (`/mnt/data/`). Use the `shell` tool (`grep`, "
            "`head`, `tail`, `sed`, `python3`) to search or inspect specific sections "
            "instead of printing the whole file.\n\n"
            f"Preview:\n{result[:_LARGE_RESULT_PREVIEW_CHARS]}"
        )
