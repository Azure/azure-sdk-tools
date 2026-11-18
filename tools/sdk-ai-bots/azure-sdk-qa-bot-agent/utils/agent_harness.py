"""Agent Framework harness construction for hosted agents."""

from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Any, cast

from agent_framework import (
    Agent,
    ContextProvider,
    FileSystemAgentFileStore,  # pyright: ignore[reportAttributeAccessIssue]
    MiddlewareTypes,
    SkillsProvider,
    ToolTypes,
    create_harness_agent,
)
from agent_framework.foundry import FoundryChatClient
from agent_framework.openai import OpenAIChatOptions

from middleware.tool_output_storage import (
    ToolOutputStorageMiddleware,
    ToolOutputStorageProvider,
)

# Conservative operating budgets independent of the configured model's maximums.
HARNESS_CONTEXT_WINDOW_TOKENS = 256_000
HARNESS_MAX_OUTPUT_TOKENS = 16_384


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
    context_providers: Sequence[ContextProvider] = (),
    middleware: Sequence[MiddlewareTypes],
    default_options: Mapping[str, Any],
) -> Agent[OpenAIChatOptions]:
    """Create a hosted agent with shared harness safeguards."""
    resolved_options = cast(OpenAIChatOptions, dict(default_options))
    memory_store = FileSystemAgentFileStore(Path.cwd() / "agent-file-memory")

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
        disable_web_search=True,
        disable_tool_auto_approval=True,
        skills_provider=skills_provider,
        context_providers=[
            *context_providers,
            ToolOutputStorageProvider(memory_store),
        ],
        middleware=[*middleware, ToolOutputStorageMiddleware(memory_store)],
        default_options=resolved_options,
    )
