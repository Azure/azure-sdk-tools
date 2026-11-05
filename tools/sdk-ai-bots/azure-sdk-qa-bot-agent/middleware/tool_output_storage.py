"""Store large opaque tool outputs before they enter model context."""

from __future__ import annotations

import re
from collections.abc import Awaitable, Callable
from uuid import uuid4

from agent_framework import (
    AgentFileStore,
    Content,
    FunctionInvocationContext,
    FunctionMiddleware,
)
from agent_framework._filesystem import _storage_key_segment  # pyright: ignore[reportPrivateUsage]

from tools import TOOL_REGISTRY

_LARGE_RESULT_CHARS = 20_000
_LARGE_RESULT_PREVIEW_CHARS = 2_000
_SKIP_TOOL_PREFIXES = ("file_memory_", "mode_", "todos_")
_SKIP_TOOLS = {"load_skill", "read_skill_resource", "run_skill_script"}
_ENCODED_SCOPE_PREFIX = "~scope-"


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


class ToolOutputStorageMiddleware(FunctionMiddleware):
    """Replace large opaque results with a searchable file reference."""

    def __init__(self, memory_store: AgentFileStore) -> None:
        self.memory_store = memory_store

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        await call_next()

        name = context.function.name
        if (
            name in TOOL_REGISTRY
            or name in _SKIP_TOOLS
            or name.startswith(_SKIP_TOOL_PREFIXES)
        ):
            return

        result = _get_text(context.result)
        if not result:
            return

        context.result = result
        if len(result) <= _LARGE_RESULT_CHARS or context.session is None:
            return

        file_name = f"tool-result-{_safe_name(name)}-{uuid4().hex}.txt"
        session_scope = _storage_key_segment(
            context.session.session_id,
            encoded_prefix=_ENCODED_SCOPE_PREFIX,
        )
        await self.memory_store.write(f"{session_scope}/{file_name}", result)
        context.result = (
            f"Large tool result stored in session memory as `{file_name}` "
            f"({len(result):,} characters). Use `file_memory_grep` to search it "
            "and `file_memory_read` to read it.\n\n"
            f"Preview:\n{result[:_LARGE_RESULT_PREVIEW_CHARS]}"
        )
