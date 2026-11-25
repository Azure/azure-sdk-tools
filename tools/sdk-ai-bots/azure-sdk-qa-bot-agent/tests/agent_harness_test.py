"""Tests for the hosted Agent Framework harness integration."""

from __future__ import annotations

from pathlib import Path
from typing import cast
from unittest.mock import Mock

from agent_framework import (
    CompactionProvider,
    ContextWindowCompactionStrategy,
    FileAccessProvider,
    InMemoryAgentFileStore,
    InMemoryHistoryProvider,
    InlineSkill,
    SkillsProvider,
    SkillFrontmatter,
    create_harness_agent,
)
from agent_framework.foundry import FoundryChatClient
from pytest import MonkeyPatch

from middleware.tool_output_security import ToolOutputSecurityMiddleware
from middleware.tool_output_storage import ToolOutputStorageMiddleware
from utils.agent_harness import (
    HARNESS_CONTEXT_WINDOW_TOKENS,
    HARNESS_MAX_OUTPUT_TOKENS,
    get_harness_defaults,
)


def _create_agent(tmp_path: Path, monkeypatch: MonkeyPatch):
    monkeypatch.chdir(tmp_path)
    client = Mock(spec=FoundryChatClient)
    file_store = InMemoryAgentFileStore()
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
        tools=[],
        skills_provider=skills_provider,
        context_providers=[Mock()],
        middleware=[
            ToolOutputSecurityMiddleware(),
            ToolOutputStorageMiddleware(file_store),
        ],
        default_options={"max_tool_calls": 10},
        **get_harness_defaults(file_store),
    )
    return agent, client


def test_harness_adds_managed_providers(
    tmp_path: Path,
    monkeypatch: MonkeyPatch,
) -> None:
    agent, client = _create_agent(tmp_path, monkeypatch)

    assert agent.require_per_service_call_history_persistence
    client.get_shell_tool.assert_not_called()
    assert any(
        isinstance(provider, InMemoryHistoryProvider)
        for provider in agent.context_providers
    )
    assert any(
        isinstance(provider, CompactionProvider)
        for provider in agent.context_providers
    )
    file_access = next(
        (
            provider
            for provider in agent.context_providers
            if isinstance(provider, FileAccessProvider)
        ),
        None,
    )
    assert file_access is not None
    assert file_access.session_scoped is True
    assert file_access.disable_write_tools is True
    assert file_access.disable_readonly_tool_approval is True
    assert not any(
        type(provider).__name__ in {
            "AgentModeProvider",
            "FileMemoryProvider",
            "ShellEnvironmentProvider",
            "TodoProvider",
        }
        for provider in agent.context_providers
    )
    middleware_names = [type(m).__name__ for m in agent.middleware or []]
    assert "ToolOutputSecurityMiddleware" in middleware_names
    assert "ToolOutputStorageMiddleware" in middleware_names
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
        isinstance(provider, FileAccessProvider)
        for provider in agent.context_providers
    )
