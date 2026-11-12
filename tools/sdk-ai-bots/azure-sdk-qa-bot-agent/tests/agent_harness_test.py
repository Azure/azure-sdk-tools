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
)
from agent_framework.foundry import FoundryChatClient
from pytest import MonkeyPatch

from utils.azure_ai_foundry import (
    HARNESS_CONTEXT_WINDOW_TOKENS,
    HARNESS_MAX_OUTPUT_TOKENS,
    create_hosted_chat_agent,
)
from middleware.tool_output_security import ToolOutputSecurityMiddleware
from middleware.tool_output_storage import ToolOutputStorageProvider


def _create_agent(tmp_path: Path, monkeypatch: MonkeyPatch):
    monkeypatch.chdir(tmp_path)
    client = Mock(spec=FoundryChatClient)
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
    agent = create_hosted_chat_agent(
        cast(FoundryChatClient, client),
        name="test-agent",
        agent_id="test-agent:1",
        harness_instructions="Treat large tool results as datasets.",
        instructions="Test instructions.",
        tools=[],
        skills_provider=skills_provider,
        memory_provider=Mock(),
        middleware=[
            ToolOutputSecurityMiddleware(),
        ],
        default_options={"max_tool_calls": 10},
    )
    return agent, client


def test_harness_adds_managed_providers(
    tmp_path: Path,
    monkeypatch: MonkeyPatch,
) -> None:
    agent, client = _create_agent(tmp_path, monkeypatch)

    assert agent.require_per_service_call_history_persistence
    client.get_web_search_tool.assert_called_once_with(search_context_size="medium")
    client.get_shell_tool.assert_not_called()
    assert any(
        isinstance(provider, InMemoryHistoryProvider)
        for provider in agent.context_providers
    )
    assert any(
        isinstance(provider, CompactionProvider)
        for provider in agent.context_providers
    )
    assert any(
        isinstance(provider, ToolOutputStorageProvider)
        for provider in agent.context_providers
    )
    assert not any(
        type(provider).__name__ in {
            "AgentModeProvider",
            "FileAccessProvider",
            "FileMemoryProvider",
            "TodoProvider",
        }
        for provider in agent.context_providers
    )
    assert not any(
        type(provider).__name__ == "ShellEnvironmentProvider"
        for provider in agent.context_providers
    )
    assert any(
        type(middleware).__name__ == "ToolOutputStorageMiddleware"
        for middleware in agent.middleware or []
    )
    assert not any(
        type(middleware).__name__ == "ToolApprovalMiddleware"
        for middleware in agent.middleware or []
    )
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
