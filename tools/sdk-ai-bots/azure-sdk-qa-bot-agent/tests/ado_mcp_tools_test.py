"""Integration tests for Azure DevOps MCP tools.

These tests verify that ``create_ado_mcp_tool`` correctly creates an
MCP tool backed by the Azure DevOps MCP server via stdio.

Requirements:
  - ``AZURE_APPCONFIG_ENDPOINT`` env var set (for App Configuration)
  - Azure credentials available (``DefaultAzureCredential``)
  - ``npx`` available on PATH
  - Network access to Azure DevOps (dev.azure.com/azure-sdk)
"""

from __future__ import annotations

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
from config.app_config import get as cfg
from tools.ado_mcp_tools import create_ado_mcp_tool


@pytest_asyncio.fixture(scope="module")
async def ai_client():
    """Initialise App Configuration and return an AzureAIClient."""
    await app_config.init()
    return AzureAIClient(
        project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
        model_deployment_name=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL"),
        credential=DefaultAzureCredential(),
    )


@pytest_asyncio.fixture(scope="module")
async def ado_mcp_tool():
    """Create the ADO MCP tool (requires App Configuration to be loaded)."""
    await app_config.init()
    return await create_ado_mcp_tool()


@pytest.mark.asyncio
async def test_ado_mcp_search(ai_client, ado_mcp_tool) -> None:
    """Run an agent with the ADO MCP tool and search for doc in the azure-sdk org."""
    async with Agent(
        client=ai_client,
        name="AdoMcpTestAgent",
        instructions=(
            "You are a helpful assistant that can interact with Azure DevOps. "
            "Use the Azure DevOps tools to answer questions."
        ),
        tools=ado_mcp_tool,
    ) as agent:
        result = await agent.run(
            "Search for doc containing 'Test Proxy' in the 'internal' "
            "project of the azure-sdk organization."
        )
        text = result.text
        print(f"\nAgent response:\n{text}")
        assert text, "Agent returned an empty response"
