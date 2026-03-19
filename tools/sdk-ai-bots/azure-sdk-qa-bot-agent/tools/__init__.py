"""Tool registry for the Azure SDK QA Bot hosted agent.

Each tool method is registered automatically when decorated with ``@tool``.
The response model is inferred from the method's return type annotation.
"""

from __future__ import annotations

import logging
import sys
from typing import get_type_hints

logger = logging.getLogger(__name__)

TOOL_REGISTRY: dict[str, type] = {}


def tool(fn):
    """Mark a method as a hosted-agent tool and register it.

    The response model is inferred from the method's return type annotation.
    """
    # Resolve the caller's module globals so that get_type_hints can
    # evaluate forward references from ``from __future__ import annotations``.
    module = sys.modules.get(fn.__module__, None)
    globalns = getattr(module, "__dict__", None)
    try:
        hints = get_type_hints(fn, globalns=globalns)
    except Exception:
        hints = {}
    response_model = hints.get("return")
    if response_model is not None:
        TOOL_REGISTRY[fn.__name__] = response_model
    else:
        logger.warning(
            "Tool %s has no return type annotation, skipping registration",
            fn.__qualname__,
        )
    return fn


__all__ = ["TOOL_REGISTRY", "tool"]
