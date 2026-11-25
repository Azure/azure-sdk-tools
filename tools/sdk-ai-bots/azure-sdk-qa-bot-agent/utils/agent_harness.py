"""Agent Framework harness construction for hosted agents."""

from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Any, cast

from agent_framework import (
    Agent,
    AgentFileStore,  # pyright: ignore[reportAttributeAccessIssue]
    ContextProvider,
    FileSystemAgentFileStore,  # pyright: ignore[reportAttributeAccessIssue]
    MiddlewareTypes,
    SkillsProvider,
    ToolTypes,
    create_harness_agent,
)
from agent_framework.foundry import FoundryChatClient
from agent_framework.openai import OpenAIChatOptions

# Conservative operating budgets independent of the configured model's maximums.
HARNESS_CONTEXT_WINDOW_TOKENS = 256_000
HARNESS_MAX_OUTPUT_TOKENS = 16_384


def create_file_store() -> AgentFileStore:
    """Create the container-local file store for offloaded tool results."""
    return FileSystemAgentFileStore(Path.cwd() / "agent-file-memory")


def create_hosted_agent(
    client: FoundryChatClient,
    *,
    name: str,
    agent_id: str,
    harness_instructions: str,
    instructions: str,
    tools: ToolTypes
    | Callable[..., Any]
    | Sequence[ToolTypes | Callable[..., Any]],
    skills_provider: SkillsProvider | None = None,
    file_access_store: AgentFileStore | None = None,
    context_providers: Sequence[ContextProvider] = (),
    middleware: Sequence[MiddlewareTypes] = (),
    default_options: Mapping[str, Any],
) -> Agent[OpenAIChatOptions]:
    """Create a hosted agent with shared harness safeguards."""
    resolved_options = cast(OpenAIChatOptions, dict(default_options))

    return create_harness_agent(  # pyright: ignore[reportCallIssue]
        client,
        name=name,
        id=agent_id,
        harness_instructions=harness_instructions,
        agent_instructions=instructions,
        tools=tools,
        max_context_window_tokens=HARNESS_CONTEXT_WINDOW_TOKENS,
        max_output_tokens=HARNESS_MAX_OUTPUT_TOKENS,
        disable_todo=True,
        disable_mode=True,
        disable_file_memory=True,
        file_access_store=file_access_store,
        file_access_session_scoped=True,
        file_access_disable_write_tools=True,
        file_access_disable_readonly_tool_approval=True,
        disable_web_search=True,
        disable_tool_auto_approval=True,
        skills_provider=skills_provider,
        context_providers=[*context_providers],
        middleware=middleware,
        default_options=resolved_options,
    )
