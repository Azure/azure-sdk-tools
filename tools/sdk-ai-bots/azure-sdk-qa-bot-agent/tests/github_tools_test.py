"""Integration tests for GitHub MCP tools.

These tests verify that ``create_github_mcp_tool`` correctly creates an
MCP tool backed by the GitHub Copilot MCP server.

Requirements:
  - ``GITHUB_MCP_CONNECTION_ID`` env var (Foundry project connection for GitHub)
  - ``AZURE_APPCONFIG_ENDPOINT`` env var set (for App Configuration)
  - Azure credentials available (``DefaultAzureCredential``)
  - Network access to the AI Foundry endpoint and GitHub API
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

import pytest
import pytest_asyncio

# Ensure the project root is on sys.path so ``config``, ``tools``, etc. resolve.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from dotenv import load_dotenv

load_dotenv()

from agent_framework import Agent
from agent_framework_azure_ai import AzureAIClient
from azure.identity.aio import DefaultAzureCredential

import config.app_config as app_config
from tools.github_mcp_tools import create_github_mcp_tool

@pytest_asyncio.fixture(scope="module")
async def ai_client():
    """Initialise App Configuration and return an AzureAIClient (new Foundry)."""
    await app_config.init()
    from config.app_config import get as cfg

    return AzureAIClient(
        project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
        model_deployment_name=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL"),
        credential=DefaultAzureCredential(),
    )

@pytest.mark.asyncio
@pytest.mark.usefixtures("_require_github_connection")
async def test_agent_with_github_mcp_tool(ai_client) -> None:
    """Run a simple agent with the GitHub MCP tool and ask about a repo."""
    github_mcp_tool = await create_github_mcp_tool(ai_client)

    async with Agent(
        client=ai_client,
        name="GitHubTestAgent",
        instructions=(
            "You are a helpful assistant that can interact with GitHub. "
            "Use the GitHub tools to answer questions about repositories."
        ),
        tools=github_mcp_tool,
    ) as agent:
        result = await agent.run(
            "What is the description of the Azure/azure-sdk-tools repository on GitHub?"
        )
        text = result.text
        print(f"\nAgent response:\n{text}")
        assert text, "Agent returned an empty response"
