"""Tool registry for the Azure SDK QA Bot hosted agent.

Each tool method is registered automatically when decorated with ``@tool``.
The response model is inferred from the method's return type annotation.
"""

from __future__ import annotations
import importlib
import logging
import pkgutil
import sys
from pathlib import Path
from typing import get_type_hints

logger = logging.getLogger(__name__)

TOOL_REGISTRY: dict[str, type] = {}

# Maximum characters for a single MCP tool output text content item.
_MCP_MAX_OUTPUT_CHARS = 8000


def tool(fn):
    """Mark a method as a hosted-agent tool and register it.

    The response model is inferred from the method's return type annotation.
    """
    module = sys.modules.get(fn.__module__, None)
    globalns = getattr(module, "__dict__", None)
    try:
        hints = get_type_hints(fn, globalns=globalns)
    except Exception:
        hints = {}
    response_model = hints.get("return")
    if response_model is not None:
        TOOL_REGISTRY[fn.__name__] = response_model
    return fn


def truncating_mcp_parser(result):
    """Parse MCP tool results and truncate oversized text content.

    Returns a ``str`` so the agent framework treats it as a single text
    result.  This keeps MCP tool outputs from inflating the context window.
    """
    from mcp import types as mcp_types

    parts: list[str] = []
    for item in result.content:
        if isinstance(item, mcp_types.TextContent):
            text = item.text or ""
            if len(text) > _MCP_MAX_OUTPUT_CHARS:
                logger.info(
                    "Truncating MCP text content from %d to %d chars",
                    len(text),
                    _MCP_MAX_OUTPUT_CHARS,
                )
                text = text[:_MCP_MAX_OUTPUT_CHARS] + "\n... [truncated]"
            parts.append(text)
    return "\n".join(parts) if parts else "null"


# Auto-import all *_tools modules to trigger @tool registration.
_package_dir = str(Path(__file__).parent)
for _info in pkgutil.iter_modules([_package_dir]):
    if _info.name.endswith("_tools"):
        importlib.import_module(f"tools.{_info.name}")
