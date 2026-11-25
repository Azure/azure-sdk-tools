"""Shared Agent Framework harness defaults for hosted agents."""

from pathlib import Path
from typing import Any

from agent_framework import (
    AgentFileStore,  # pyright: ignore[reportAttributeAccessIssue]
    FileSystemAgentFileStore,  # pyright: ignore[reportAttributeAccessIssue]
    InMemoryHistoryProvider,
)

# Conservative operating budgets independent of the configured model's maximums.
HARNESS_CONTEXT_WINDOW_TOKENS = 256_000
HARNESS_MAX_OUTPUT_TOKENS = 16_384


def create_file_store() -> AgentFileStore:
    """Create the container-local file store for offloaded tool results."""
    return FileSystemAgentFileStore(Path.cwd() / "agent-file-memory")


def get_harness_defaults(
    file_access_store: AgentFileStore | None = None,
) -> dict[str, Any]:
    """Return shared create_harness_agent keyword arguments for hosted agents."""
    return {
        "max_context_window_tokens": HARNESS_CONTEXT_WINDOW_TOKENS,
        "max_output_tokens": HARNESS_MAX_OUTPUT_TOKENS,
        # FoundryChatClient stores conversation history server-side (store=True);
        # avoid duplicating full transcripts inside Foundry's 1 MB session state.
        "history_provider": InMemoryHistoryProvider(
            load_messages=False,
            store_inputs=False,
            store_outputs=False,
        ),
        "disable_todo": True,
        "disable_mode": True,
        "disable_file_memory": True,
        "file_access_store": file_access_store,
        "file_access_session_scoped": True,
        "file_access_disable_write_tools": True,
        "file_access_disable_readonly_tool_approval": True,
        "disable_web_search": True,
        "disable_tool_auto_approval": True,
    }
