# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument

"""
Tests for agent creation and retrieval logic in _agent.py.
"""

import sys
from unittest.mock import MagicMock, patch

import pytest

# Mock azure.ai.agents before importing _agent
sys.modules["azure.ai.agents"] = MagicMock()
sys.modules["azure.ai.agents.models"] = MagicMock()

from src.agent._agent import _get_agents_endpoint, _get_or_create_agent


class MockAgent:
    """Mock agent object returned by AgentsClient."""

    def __init__(self, agent_id: str, name: str):
        self.id = agent_id
        self.name = name


class MockSettingsManager:
    """Mock SettingsManager for testing."""

    def __init__(self, settings: dict):
        self._settings = settings

    def get(self, key: str):
        return self._settings.get(key)


class TestGetAgentsEndpoint:
    """Tests for _get_agents_endpoint function."""

    def test_constructs_endpoint_correctly(self):
        """Test that endpoint is constructed from FOUNDRY_ENDPOINT and FOUNDRY_PROJECT."""
        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager(
                {
                    "FOUNDRY_ENDPOINT": "https://myaccount.services.ai.azure.com",
                    "FOUNDRY_PROJECT": "myproject",
                }
            )
            mock_settings_cls.return_value = mock_settings

            result = _get_agents_endpoint()

            assert result == "https://myaccount.services.ai.azure.com/api/projects/myproject"

    def test_strips_trailing_slash_from_endpoint(self):
        """Test that trailing slash is stripped from FOUNDRY_ENDPOINT."""
        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager(
                {
                    "FOUNDRY_ENDPOINT": "https://myaccount.services.ai.azure.com/",
                    "FOUNDRY_PROJECT": "myproject",
                }
            )
            mock_settings_cls.return_value = mock_settings

            result = _get_agents_endpoint()

            assert result == "https://myaccount.services.ai.azure.com/api/projects/myproject"

    def test_raises_when_foundry_endpoint_missing(self):
        """Test that ValueError is raised when FOUNDRY_ENDPOINT is not configured."""
        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager(
                {
                    "FOUNDRY_ENDPOINT": None,
                    "FOUNDRY_PROJECT": "myproject",
                }
            )
            mock_settings_cls.return_value = mock_settings

            with pytest.raises(ValueError, match="FOUNDRY_ENDPOINT not configured"):
                _get_agents_endpoint()

    def test_raises_when_foundry_project_missing(self):
        """Test that ValueError is raised when FOUNDRY_PROJECT is not configured."""
        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager(
                {
                    "FOUNDRY_ENDPOINT": "https://myaccount.services.ai.azure.com",
                    "FOUNDRY_PROJECT": None,
                }
            )
            mock_settings_cls.return_value = mock_settings

            with pytest.raises(ValueError, match="FOUNDRY_PROJECT not configured"):
                _get_agents_endpoint()


class TestGetOrCreateAgent:
    """Tests for _get_or_create_agent function."""

    @pytest.fixture
    def mock_client(self):
        """Create a mock AgentsClient."""
        return MagicMock()

    @pytest.fixture
    def mock_toolset(self):
        """Create a mock ToolSet."""
        return MagicMock()

    def test_returns_existing_agent_when_found_by_name(self, mock_client, mock_toolset):
        """Test that existing agent is returned when found by name."""
        existing_agent = MockAgent("agent-123", "My Agent")
        mock_client.list_agents.return_value = [existing_agent]

        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager({"FOUNDRY_KERNEL_MODEL": "gpt-4"})
            mock_settings_cls.return_value = mock_settings

            result = _get_or_create_agent(
                client=mock_client,
                name="My Agent",
                description="Test agent",
                instructions="Do stuff",
                toolset=mock_toolset,
            )

            assert result == "agent-123"
            mock_client.create_agent.assert_not_called()

    def test_creates_agent_when_no_existing_agent_found(self, mock_client, mock_toolset):
        """Test that new agent is created when no existing agent matches."""
        mock_client.list_agents.return_value = []
        mock_client.create_agent.return_value = MockAgent("new-agent-456", "My Agent")

        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager({"FOUNDRY_KERNEL_MODEL": "gpt-4"})
            mock_settings_cls.return_value = mock_settings

            result = _get_or_create_agent(
                client=mock_client,
                name="My Agent",
                description="Test agent",
                instructions="Do stuff",
                toolset=mock_toolset,
            )

            assert result == "new-agent-456"
            mock_client.create_agent.assert_called_once_with(
                name="My Agent",
                description="Test agent",
                model="gpt-4",
                instructions="Do stuff",
                toolset=mock_toolset,
            )

    def test_creates_agent_when_name_does_not_match(self, mock_client, mock_toolset):
        """Test that new agent is created when existing agents have different names."""
        other_agent = MockAgent("other-agent", "Different Agent")
        mock_client.list_agents.return_value = [other_agent]
        mock_client.create_agent.return_value = MockAgent("new-agent-789", "My Agent")

        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager({"FOUNDRY_KERNEL_MODEL": "gpt-4"})
            mock_settings_cls.return_value = mock_settings

            result = _get_or_create_agent(
                client=mock_client,
                name="My Agent",
                description="Test agent",
                instructions="Do stuff",
                toolset=mock_toolset,
            )

            assert result == "new-agent-789"
            mock_client.create_agent.assert_called_once()

    def test_creates_agent_when_list_fails(self, mock_client, mock_toolset):
        """Test that agent is created when listing agents fails."""
        mock_client.list_agents.side_effect = Exception("Network error")
        mock_client.create_agent.return_value = MockAgent("fallback-agent", "My Agent")

        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager({"FOUNDRY_KERNEL_MODEL": "gpt-4"})
            mock_settings_cls.return_value = mock_settings

            result = _get_or_create_agent(
                client=mock_client,
                name="My Agent",
                description="Test agent",
                instructions="Do stuff",
                toolset=mock_toolset,
            )

            assert result == "fallback-agent"
            mock_client.create_agent.assert_called_once()

    def test_raises_when_foundry_kernel_model_missing(self, mock_client, mock_toolset):
        """Test that ValueError is raised when FOUNDRY_KERNEL_MODEL is not configured."""
        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager({"FOUNDRY_KERNEL_MODEL": None})
            mock_settings_cls.return_value = mock_settings

            with pytest.raises(ValueError, match="FOUNDRY_KERNEL_MODEL not configured"):
                _get_or_create_agent(
                    client=mock_client,
                    name="My Agent",
                    description="Test agent",
                    instructions="Do stuff",
                    toolset=mock_toolset,
                )

    def test_raises_runtime_error_on_timeout(self, mock_client, mock_toolset):
        """Test that RuntimeError with timeout message is raised on timeout."""
        mock_client.list_agents.return_value = []
        mock_client.create_agent.side_effect = Exception("Request timed out")

        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager({"FOUNDRY_KERNEL_MODEL": "gpt-4"})
            mock_settings_cls.return_value = mock_settings

            with pytest.raises(RuntimeError, match="Azure Agents service timed out"):
                _get_or_create_agent(
                    client=mock_client,
                    name="My Agent",
                    description="Test agent",
                    instructions="Do stuff",
                    toolset=mock_toolset,
                )

    def test_raises_runtime_error_on_create_failure(self, mock_client, mock_toolset):
        """Test that RuntimeError is raised when agent creation fails."""
        mock_client.list_agents.return_value = []
        mock_client.create_agent.side_effect = Exception("Invalid model deployment")

        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager({"FOUNDRY_KERNEL_MODEL": "gpt-4"})
            mock_settings_cls.return_value = mock_settings

            with pytest.raises(RuntimeError, match="Failed to create agent: Invalid model deployment"):
                _get_or_create_agent(
                    client=mock_client,
                    name="My Agent",
                    description="Test agent",
                    instructions="Do stuff",
                    toolset=mock_toolset,
                )

    def test_finds_agent_among_multiple(self, mock_client, mock_toolset):
        """Test that correct agent is found among multiple existing agents."""
        agents = [
            MockAgent("agent-1", "Agent One"),
            MockAgent("agent-2", "My Agent"),
            MockAgent("agent-3", "Agent Three"),
        ]
        mock_client.list_agents.return_value = agents

        with patch("src.agent._agent.SettingsManager") as mock_settings_cls:
            mock_settings = MockSettingsManager({"FOUNDRY_KERNEL_MODEL": "gpt-4"})
            mock_settings_cls.return_value = mock_settings

            result = _get_or_create_agent(
                client=mock_client,
                name="My Agent",
                description="Test agent",
                instructions="Do stuff",
                toolset=mock_toolset,
            )

            assert result == "agent-2"
            mock_client.create_agent.assert_not_called()
