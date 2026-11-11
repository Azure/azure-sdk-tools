"""Tool-output security middleware (prompt-injection defence).

Spotlights untrusted tool output as *data, not instructions* by wrapping it in
a labelled, JSON-escaped delimiter. Native ``@tool`` functions and trusted
harness tools are left unchanged.
"""

from __future__ import annotations

import json
from collections.abc import Awaitable, Callable
from copy import copy

from agent_framework import Content, FunctionInvocationContext, FunctionMiddleware

from tools import TOOL_REGISTRY

_SPOTLIGHT_OPEN = "<untrusted_tool_output>"
_SPOTLIGHT_CLOSE = "</untrusted_tool_output>"
_TRUSTED_HARNESS_TOOLS = {
    "load_skill",
    "read_skill_resource",
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
        elif isinstance(result, list):
            protected_result = []
            for item in result:
                if isinstance(item, Content) and item.type == "text" and item.text:
                    protected_item = copy(item)
                    protected_item.text = spotlight(item.text)
                    protected_result.append(protected_item)
                elif isinstance(item, Content):
                    protected_result.append(item)
                else:
                    protected_result.append(
                        spotlight(json.dumps(item, ensure_ascii=False, default=str))
                    )
            context.result = protected_result
        elif isinstance(result, dict):
            context.result = spotlight(
                json.dumps(result, ensure_ascii=False, default=str)
            )
