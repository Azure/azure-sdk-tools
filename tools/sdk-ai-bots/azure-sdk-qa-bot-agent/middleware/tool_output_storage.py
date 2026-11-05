"""Store large opaque tool outputs before they enter model context."""

from __future__ import annotations

import re
from collections.abc import Awaitable, Callable
from pathlib import Path
from uuid import uuid4

from agent_framework import Content, FunctionInvocationContext, FunctionMiddleware

from tools import TOOL_REGISTRY

_LARGE_RESULT_CHARS = 20_000
_LARGE_RESULT_PREVIEW_CHARS = 2_000
_SKIP_TOOL_PREFIXES = ("file_memory_", "mode_", "todos_")
_SKIP_TOOLS = {"load_skill", "read_skill_resource", "run_skill_script"}


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

    def __init__(self, tool_result_root: Path | None = None) -> None:
        self.tool_result_root = tool_result_root or (
            Path.cwd() / "agent-file-access" / "tool-results"
        )

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

        session_dir = self.tool_result_root / _safe_name(context.session.session_id)
        session_dir.mkdir(parents=True, exist_ok=True)
        result_path = session_dir / f"{_safe_name(name)}-{uuid4().hex}.txt"
        result_path.write_text(result, encoding="utf-8")
        relative_path = result_path.relative_to(
            self.tool_result_root.parent
        ).as_posix()
        context.result = (
            f"Large tool result stored at `{relative_path}` "
            f"({len(result):,} characters). Use `file_access_grep` and "
            "`file_access_read_lines` to inspect relevant sections.\n\n"
            f"Preview:\n{result[:_LARGE_RESULT_PREVIEW_CHARS]}"
        )
