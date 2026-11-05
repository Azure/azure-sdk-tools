"""Tests for the hosted Agent Framework harness integration."""

from __future__ import annotations

from pathlib import Path
from typing import cast
from unittest.mock import Mock

import pytest
from agent_framework import (
    AgentModeProvider,
    InMemoryHistoryProvider,
    InlineSkill,
    SkillsProvider,
    SkillFrontmatter,
    TodoProvider,
)
from agent_framework.foundry import FoundryChatClient
from pytest import MonkeyPatch

from utils.azure_ai_foundry import create_hosted_chat_agent
from middleware.tool_output_security import ToolOutputSecurityMiddleware
from middleware.tool_output_storage import ToolOutputStorageMiddleware


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
            ToolOutputStorageMiddleware(),
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
    client.get_web_search_tool.assert_called_once_with()
    client.get_shell_tool.assert_called_once()
    assert any(
        isinstance(provider, InMemoryHistoryProvider)
        for provider in agent.context_providers
    )
    assert any(
        isinstance(provider, TodoProvider) for provider in agent.context_providers
    )
    mode_provider = next(
        provider
        for provider in agent.context_providers
        if isinstance(provider, AgentModeProvider)
    )
    assert mode_provider.default_mode == "execute"
    assert any(
        type(provider).__name__ == "FileMemoryProvider"
        for provider in agent.context_providers
    )
    file_access_provider = next(
        provider
        for provider in agent.context_providers
        if type(provider).__name__ == "FileAccessProvider"
    )
    assert getattr(file_access_provider, "disable_readonly_tool_approval")
    assert getattr(file_access_provider, "disable_write_tool_approval")
    assert any(
        type(provider).__name__ == "ShellEnvironmentProvider"
        for provider in agent.context_providers
    )
    assert any(
        type(middleware).__name__ == "ToolApprovalMiddleware"
        for middleware in agent.middleware or []
    )
    instructions = agent.default_options["instructions"]
    assert "Treat large tool results as datasets." in instructions
    assert "Test instructions." in instructions


@pytest.mark.asyncio
async def test_harness_shell_executes_in_workspace(
    tmp_path: Path,
    monkeypatch: MonkeyPatch,
) -> None:
    _, client = _create_agent(tmp_path, monkeypatch)
    shell_function = client.get_shell_tool.call_args.kwargs["func"]

    assert shell_function.approval_mode == "never_require"
    result = await shell_function.func("echo harness-shell")

    assert "harness-shell" in result
    assert (tmp_path / "agent-file-access").is_dir()
