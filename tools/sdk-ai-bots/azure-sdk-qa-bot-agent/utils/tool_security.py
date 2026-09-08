"""Centralized tool-output security middleware (prompt-injection defence).

Spotlights untrusted MCP tool output as *data, not instructions* by wrapping
it in a labelled, JSON-escaped delimiter. Runs uniformly for every MCP tool
through the agent framework ``FunctionMiddleware`` pipeline, layering on top of
per-tool parsers (e.g. the GitHub author-redaction parser) and the platform
content-safety guardrail.

Native ``@tool`` functions are intentionally skipped: their structured results
are decoded server-side via :data:`tools.TOOL_REGISTRY` response models, and
wrapping them would break ``model_validate_json`` decoding. MCP tools, by
contrast, return opaque strings sourced from external systems (GitHub, Azure
DevOps) — exactly the untrusted surface this middleware hardens.
"""

from __future__ import annotations

import json
import logging
from collections.abc import Awaitable, Callable

from agent_framework import FunctionInvocationContext, FunctionMiddleware

from tools import TOOL_REGISTRY

logger = logging.getLogger(__name__)

# Label marking embedded tool output as untrusted data (spotlighting defence
# against indirect prompt injection). The agent instructions tell the model to
# treat anything inside these tags as read-only reference data.
_SPOTLIGHT_OPEN = "<untrusted_tool_output>"
_SPOTLIGHT_CLOSE = "</untrusted_tool_output>"


def spotlight(text: str) -> str:
    """Wrap opaque tool output as a labelled, JSON-escaped untrusted block."""
    escaped = json.dumps(text, ensure_ascii=False).replace("/", "\\/")
    return f"{_SPOTLIGHT_OPEN}\n{escaped}\n{_SPOTLIGHT_CLOSE}"


class ToolOutputSecurityMiddleware(FunctionMiddleware):
    """Spotlight every MCP tool result as untrusted data.

    Tool-agnostic: no hardcoded tool names. Applies to every MCP tool and
    skips native structured tools to preserve their server-side decoding
    contract (see module docstring).
    """

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        await call_next()

        # Native @tool functions carry a TOOL_REGISTRY response model that the
        # server decodes with model_validate_json — leave their structured
        # output intact. Only opaque MCP string output is spotlighted.
        if context.function.name in TOOL_REGISTRY:
            return

        result = context.result
        if isinstance(result, str) and result:
            context.result = spotlight(result)
