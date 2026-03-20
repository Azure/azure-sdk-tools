"""Tool registry for the Azure SDK QA Bot hosted agent.

Each tool method is registered automatically when decorated with ``@tool``.
The response model is inferred from the method's return type annotation.
"""

from __future__ import annotations
import importlib
import pkgutil
import sys
from pathlib import Path
from typing import get_type_hints

TOOL_REGISTRY: dict[str, type] = {}

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


# Auto-import all *_tools modules to trigger @tool registration.
_package_dir = str(Path(__file__).parent)
for _info in pkgutil.iter_modules([_package_dir]):
    if _info.name.endswith("_tools"):
        importlib.import_module(f"tools.{_info.name}")
