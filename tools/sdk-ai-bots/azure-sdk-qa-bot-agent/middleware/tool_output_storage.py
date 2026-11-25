"""Store large opaque tool outputs before they enter model context."""

from __future__ import annotations

import json
import logging
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
from tools.github_mcp_tools import _is_github_mcp_tool, _redact_untrusted_authors

logger = logging.getLogger(__name__)

_OPAQUE_RESULT_OFFLOAD_CHARS = 20_000
_LARGE_RESULT_PREVIEW_CHARS = 2_000
_MAX_FILE_ACCESS_OUTPUT_CHARS = 20_000
_MAX_GREP_LINE_CHARS = 500
_SKIP_TOOL_PREFIXES = ("file_access_",)
_SKIP_TOOLS = {"load_skill", "read_skill_resource", "run_skill_script"}
_FILE_ACCESS_NAMESPACE = "~access-"


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


def _session_path(session_id: str, file_name: str) -> str:
    segment = _storage_key_segment(
        session_id,
        encoded_prefix=_FILE_ACCESS_NAMESPACE,
    )
    return f"{_FILE_ACCESS_NAMESPACE}/{segment}/{file_name}"


def _format_for_storage(text: str) -> str:
    """Pretty-print JSON payloads so line-based file_access tools stay readable."""
    stripped = text.lstrip()
    if stripped.startswith(("{", "[")):
        try:
            return json.dumps(json.loads(text), indent=2, ensure_ascii=False)
        except (ValueError, TypeError):
            pass
    return text


def _bound_grep_results(results: list[object]) -> list[object]:
    """Keep bounded head+tail matches so large logs don't exceed session-state limits."""
    bounded: list[object] = []
    remaining = _MAX_FILE_ACCESS_OUTPUT_CHARS
    for item in results:
        if not isinstance(item, dict) or remaining <= 0:
            continue
        raw_matches = item.get("matching_lines")
        if not isinstance(raw_matches, list):
            serialized = json.dumps(item, ensure_ascii=False, default=str)
            if len(serialized) <= remaining:
                bounded.append(item)
                remaining -= len(serialized)
            continue

        selected_head: list[dict[str, object]] = []
        selected_tail: list[dict[str, object]] = []
        left, right = 0, len(raw_matches) - 1
        take_head = True
        while left <= right and remaining > 0:
            was_head = take_head
            idx = left if was_head else right
            if was_head:
                left += 1
            else:
                right -= 1
            take_head = not take_head
            match = raw_matches[idx]
            if not isinstance(match, dict):
                continue
            line_text = str(match.get("line", ""))
            clipped = {**match, "line": line_text[:_MAX_GREP_LINE_CHARS]}
            cost = len(json.dumps(clipped, ensure_ascii=False, default=str)) + 2
            if cost > remaining and (selected_head or selected_tail):
                break
            remaining = max(0, remaining - cost)
            if was_head:
                selected_head.append(clipped)
            else:
                selected_tail.append(clipped)

        selected = [*selected_head, *reversed(selected_tail)]
        omitted = len(raw_matches) - len(selected)
        entry: dict[str, object] = {
            "file_name": item.get("file_name", ""),
            "snippet": str(item.get("snippet", ""))[:_MAX_GREP_LINE_CHARS],
            "matching_lines": selected,
        }
        if omitted > 0:
            entry["omitted_matches"] = omitted
        bounded.append(entry)
    return bounded


def _bound_file_access_result(name: str, result: object) -> object:
    if name == "file_access_grep" and isinstance(result, list):
        return _bound_grep_results(result)
    text = _get_text(result)
    if text is not None and len(text) > _MAX_FILE_ACCESS_OUTPUT_CHARS:
        return (
            f"{text[:_MAX_FILE_ACCESS_OUTPUT_CHARS]}\n"
            f"[Output truncated at {_MAX_FILE_ACCESS_OUTPUT_CHARS:,} characters. "
            "Use `file_access_read_lines` with a smaller range or `file_access_grep` "
            "with a narrower pattern.]"
        )
    return result


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
        if name.startswith(_SKIP_TOOL_PREFIXES):
            context.result = _bound_file_access_result(name, context.result)
            return
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
        await self.memory_store.write(
            _session_path(context.session.session_id, file_name),
            stored_text,
        )
        logger.info(
            "Offloaded opaque tool result: tool=%s file=%s chars=%d preview_chars=%d",
            name,
            file_name,
            len(result),
            _LARGE_RESULT_PREVIEW_CHARS,
        )
        context.result = (
            f"Large tool result stored as `{file_name}` "
            f"({len(result):,} characters). Use `file_access_grep` (glob_pattern=`{file_name}`) "
            "with specific identifiers to search it, and `file_access_read_lines` for "
            "surrounding lines; do not call `file_access_read` on the full file.\n\n"
            f"Preview:\n{result[:_LARGE_RESULT_PREVIEW_CHARS]}"
        )
