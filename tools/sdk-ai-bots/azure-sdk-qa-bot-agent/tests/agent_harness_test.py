"""Tests for the hosted Agent Framework harness integration."""

from __future__ import annotations

from pathlib import Path
from typing import cast
from unittest.mock import Mock

from agent_framework import (
    CompactionProvider,
    ContextWindowCompactionStrategy,
    InMemoryHistoryProvider,
    InlineSkill,
    SkillsProvider,
    SkillFrontmatter,
    create_harness_agent,
)
from agent_framework.foundry import FoundryChatClient
from pytest import MonkeyPatch

from middleware.tool_output_security import ToolOutputSecurityMiddleware
from middleware.tool_output_storage import (
    OffloadedFilesProvider,
    ShellFilesMiddleware,
    ToolOutputStorageMiddleware,
)
from utils.agent_harness import (
    HARNESS_CONTEXT_WINDOW_TOKENS,
    HARNESS_MAX_OUTPUT_TOKENS,
    get_harness_defaults,
)


def _create_agent(tmp_path: Path, monkeypatch: MonkeyPatch):
    monkeypatch.chdir(tmp_path)
    client = Mock(spec=FoundryChatClient)
    client.get_shell_tool.return_value = {
        "type": "shell",
        "environment": {"type": "container_auto"},
    }
    openai_client = Mock()
    skills_provider = SkillsProvider(
        [
            InlineSkill(
                frontmatter=SkillFrontmatter(
                    name="test-skill",
                    description="Test skill.",
                ),
                instructions="Use the test skill.",
            )
        ]
    )
    agent = create_harness_agent(  # pyright: ignore[reportCallIssue]
        cast(FoundryChatClient, client),
        name="test-agent",
        id="test-agent:1",
        harness_instructions="Treat large tool results as datasets.",
        agent_instructions="Test instructions.",
        tools=[client.get_shell_tool(environment={"type": "container_auto"})],
        skills_provider=skills_provider,
        context_providers=[Mock(), OffloadedFilesProvider()],
        middleware=[
            ToolOutputSecurityMiddleware(),
            ToolOutputStorageMiddleware(openai_client),
            ShellFilesMiddleware(),
        ],
        default_options={"max_tool_calls": 10},
        **get_harness_defaults(),
    )
    return agent, client


def test_harness_adds_managed_providers(
    tmp_path: Path,
    monkeypatch: MonkeyPatch,
) -> None:
    agent, client = _create_agent(tmp_path, monkeypatch)

    assert agent.require_per_service_call_history_persistence
    client.get_shell_tool.assert_called_once_with(
        environment={"type": "container_auto"}
    )
    history_provider = next(
        (
            provider
            for provider in agent.context_providers
            if isinstance(provider, InMemoryHistoryProvider)
        ),
        None,
    )
    assert history_provider is not None
    assert history_provider.load_messages is False
    assert history_provider.store_inputs is False
    assert history_provider.store_outputs is False
    assert any(
        isinstance(provider, CompactionProvider)
        for provider in agent.context_providers
    )
    assert any(
        isinstance(provider, OffloadedFilesProvider)
        for provider in agent.context_providers
    )
    assert not any(
        type(provider).__name__ in {
            "AgentModeProvider",
            "FileAccessProvider",
            "FileMemoryProvider",
            "ShellEnvironmentProvider",
            "TodoProvider",
        }
        for provider in agent.context_providers
    )
    middleware_names = [type(m).__name__ for m in agent.middleware or []]
    assert "ToolOutputSecurityMiddleware" in middleware_names
    assert "ToolOutputStorageMiddleware" in middleware_names
    assert "ShellFilesMiddleware" in middleware_names
    assert middleware_names.index("ToolOutputSecurityMiddleware") < middleware_names.index(
        "ToolOutputStorageMiddleware"
    )
    assert "ToolApprovalMiddleware" not in middleware_names
    assert isinstance(agent.compaction_strategy, ContextWindowCompactionStrategy)
    assert (
        agent.compaction_strategy.max_context_window_tokens
        == HARNESS_CONTEXT_WINDOW_TOKENS
    )
    assert agent.compaction_strategy.max_output_tokens == HARNESS_MAX_OUTPUT_TOKENS
    assert agent.default_options["max_tokens"] == HARNESS_MAX_OUTPUT_TOKENS
    instructions = agent.default_options["instructions"]
    assert "Treat large tool results as datasets." in instructions
    assert "Test instructions." in instructions


def test_harness_supports_agents_without_skills_or_memory(
    tmp_path: Path,
    monkeypatch: MonkeyPatch,
) -> None:
    monkeypatch.chdir(tmp_path)
    client = Mock(spec=FoundryChatClient)

    agent = create_harness_agent(  # pyright: ignore[reportCallIssue]
        cast(FoundryChatClient, client),
        name="test-agent",
        id="test-agent:1",
        harness_instructions="Use bounded external results.",
        agent_instructions="Complete the job.",
        tools=[],
        default_options={"max_tool_calls": 10},
        **get_harness_defaults(),
    )

    assert not any(
        type(provider).__name__ == "FileAccessProvider"
        for provider in agent.context_providers
    )
