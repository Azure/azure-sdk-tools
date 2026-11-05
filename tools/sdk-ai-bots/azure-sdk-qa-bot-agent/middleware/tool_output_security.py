"""Tool-output security middleware (prompt-injection defence).

Spotlights untrusted tool output as *data, not instructions* by wrapping it in
a labelled, JSON-escaped delimiter. Native ``@tool`` functions and trusted
harness tools are left unchanged.
"""

from __future__ import annotations

import json
from collections.abc import Awaitable, Callable

from agent_framework import FunctionInvocationContext, FunctionMiddleware

from tools import TOOL_REGISTRY

_SPOTLIGHT_OPEN = "<untrusted_tool_output>"
_SPOTLIGHT_CLOSE = "</untrusted_tool_output>"
_TRUSTED_HARNESS_TOOLS = {
    "file_memory_delete",
    "file_memory_grep",
    "file_memory_ls",
    "file_memory_read",
    "file_memory_replace",
    "file_memory_replace_lines",
    "file_memory_write",
    "mode_get",
    "mode_set",
    "todos_add",
    "todos_complete",
    "todos_get_all",
    "todos_get_remaining",
    "todos_remove",
}


def spotlight(text: str) -> str:
    """Wrap opaque tool output as a labelled, JSON-escaped untrusted block."""
    escaped = json.dumps(text, ensure_ascii=False).replace("/", "\\/")
    return f"{_SPOTLIGHT_OPEN}\n{escaped}\n{_SPOTLIGHT_CLOSE}"


class ToolOutputSecurityMiddleware(FunctionMiddleware):
    """Spotlight opaque tool results as untrusted data."""

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        await call_next()

        if (
            context.function.name in TOOL_REGISTRY
            or context.function.name in _TRUSTED_HARNESS_TOOLS
        ):
            return

        result = context.result
        if isinstance(result, str) and result:
            context.result = spotlight(result)
