"""Store large opaque tool outputs before they enter model context."""

from __future__ import annotations

import logging
import re
from collections.abc import Awaitable, Callable
from typing import Any
from uuid import uuid4

from agent_framework import (
    AgentSession,
    AgentFileStore,
    Content,
    ContextProvider,
    FileStoreEntry,
    FunctionInvocationContext,
    FunctionMiddleware,
    SessionContext,
    tool,
)
from agent_framework._filesystem import _storage_key_segment  # pyright: ignore[reportPrivateUsage]

from tools import TOOL_REGISTRY
from tools.github_mcp_tools import _is_github_mcp_tool, _redact_untrusted_authors

logger = logging.getLogger(__name__)

_OPAQUE_RESULT_OFFLOAD_CHARS = 20_000
_LARGE_RESULT_PREVIEW_CHARS = 2_000
_MAX_READ_CHARS = 20_000
_MAX_GREP_MATCHES = 50
_MAX_GREP_OUTPUT_CHARS = 20_000
_MAX_GREP_LINE_CHARS = 2_000
_SKIP_TOOL_PREFIXES = ("tool_result_",)
_SKIP_TOOLS = {"load_skill", "read_skill_resource", "run_skill_script"}
_ENCODED_SCOPE_PREFIX = "~scope-"
_RESULT_FILE_PATTERN = re.compile(r"tool-result-[A-Za-z0-9._-]+\.txt")
_PROVIDER_INSTRUCTIONS = (
    "Large external tool results may be stored outside the conversation. "
    "Only when a tool response gives a `tool-result-*.txt` file name, start with "
    "`tool_result_grep` using specific identifiers or error text, then use "
    "`tool_result_read` only for the relevant line range. Do not read the entire file."
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


def _session_scope(session_id: str) -> str:
    return _storage_key_segment(
        session_id,
        encoded_prefix=_ENCODED_SCOPE_PREFIX,
    )


def _validate_file_name(file_name: str) -> str | None:
    return file_name if _RESULT_FILE_PATTERN.fullmatch(file_name) else None


class ToolOutputStorageProvider(ContextProvider):
    """Expose bounded read-only access to offloaded tool results."""

    def __init__(self, store: AgentFileStore) -> None:
        super().__init__("tool_output_storage")
        self.store = store

    async def before_run(
        self,
        *,
        agent: Any,
        session: AgentSession,
        context: SessionContext,
        state: dict[str, Any],
    ) -> None:
        del agent
        session_id = context.session_id or session.session_id
        if not session_id:
            raise ValueError("ToolOutputStorageProvider requires a session id.")
        session_scope = _session_scope(session_id)
        state["session_scope"] = session_scope

        @tool(name="tool_result_read", approval_mode="never_require")
        async def tool_result_read(
            file_name: str,
            start_line: int = 1,
            end_line: int | None = None,
        ) -> str:
            """Read a bounded line range from an offloaded tool-result file."""
            normalized = _validate_file_name(file_name)
            if normalized is None:
                return f"Invalid tool-result file name: '{file_name}'."
            if start_line < 1 or (end_line is not None and end_line < start_line):
                return "Invalid line range."

            content = await self.store.read(f"{session_scope}/{normalized}")
            if content is None:
                return f"Tool-result file '{file_name}' not found."

            lines = content.splitlines(keepends=True)
            selected = "".join(lines[start_line - 1 : end_line])
            logger.info(
                "Reading offloaded tool result: file=%s lines=%d-%s selected_chars=%d",
                file_name,
                start_line,
                end_line or "end",
                len(selected),
            )
            if len(selected) <= _MAX_READ_CHARS:
                return selected
            return (
                f"{selected[:_MAX_READ_CHARS]}\n"
                f"[Output truncated at {_MAX_READ_CHARS:,} characters. Read a smaller line range.]"
            )

        @tool(name="tool_result_grep", approval_mode="never_require")
        async def tool_result_grep(
            file_name: str,
            regex_pattern: str,
            max_matches: int = 20,
        ) -> list[dict[str, Any]] | str:
            """Search one offloaded tool-result file with a case-insensitive regular expression."""
            normalized = _validate_file_name(file_name)
            if normalized is None:
                return f"Invalid tool-result file name: '{file_name}'."
            if len(regex_pattern) > 256:
                return "The regex pattern must be 256 characters or fewer."
            if not 1 <= max_matches <= _MAX_GREP_MATCHES:
                return f"max_matches must be between 1 and {_MAX_GREP_MATCHES}."

            try:
                results = await self.store.search(
                    session_scope,
                    regex_pattern,
                    normalized,
                    recursive=False,
                )
            except ValueError as exc:
                return f"Could not search tool-result file: {exc}"
            matches: list[dict[str, Any]] = []
            remaining_matches = max_matches
            remaining_chars = _MAX_GREP_OUTPUT_CHARS
            for result in results:
                matching_lines = []
                for match in result.matching_lines:
                    if remaining_matches == 0 or remaining_chars == 0:
                        break
                    line = match.line[: min(_MAX_GREP_LINE_CHARS, remaining_chars)]
                    matching_lines.append(
                        {"line_number": match.line_number, "line": line}
                    )
                    remaining_matches -= 1
                    remaining_chars -= len(line)
                if matching_lines:
                    snippet = result.snippet[: min(_MAX_GREP_LINE_CHARS, remaining_chars)]
                    remaining_chars -= len(snippet)
                    matches.append(
                        {
                            "file_name": result.file_name,
                            "snippet": snippet,
                            "matching_lines": matching_lines,
                        }
                    )
                if remaining_matches == 0 or remaining_chars == 0:
                    break
            logger.info(
                "Searched offloaded tool result: file=%s matches=%d",
                file_name,
                len(matches),
            )
            return matches

        context.extend_instructions(self.source_id, [_PROVIDER_INSTRUCTIONS])
        context.extend_tools(
            self.source_id,
            [tool_result_read, tool_result_grep],
        )

    async def after_run(
        self,
        *,
        agent: Any,
        session: AgentSession,
        context: SessionContext,
        state: dict[str, Any],
    ) -> None:
        del agent, session, context
        session_scope = state.get("session_scope")
        if not isinstance(session_scope, str):
            return
        for entry in await self.store.list_children(session_scope):
            if entry.type == FileStoreEntry.FILE and _validate_file_name(entry.name):
                await self.store.delete(f"{session_scope}/{entry.name}")
        await self.store.delete_directory(session_scope)


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

        if _is_github_mcp_tool(name):
            result = _redact_untrusted_authors(result)
        context.result = result
        if len(result) <= _OPAQUE_RESULT_OFFLOAD_CHARS or context.session is None:
            return

        file_name = f"tool-result-{_safe_name(name)}-{uuid4().hex}.txt"
        session_scope = _session_scope(context.session.session_id)
        await self.memory_store.write(f"{session_scope}/{file_name}", result)
        logger.info(
            "Offloaded opaque tool result: tool=%s file=%s chars=%d preview_chars=%d",
            name,
            file_name,
            len(result),
            _LARGE_RESULT_PREVIEW_CHARS,
        )
        context.result = (
            f"Large tool result stored as `{file_name}` "
            f"({len(result):,} characters). Use `tool_result_grep` to search it "
            "and `tool_result_read` to read a relevant line range.\n\n"
            f"Preview:\n{result[:_LARGE_RESULT_PREVIEW_CHARS]}"
        )
