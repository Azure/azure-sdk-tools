"""Integration tests for Azure DevOps MCP tools.

These tests verify that ``create_azsdk_mcp_tool`` correctly creates an
MCP tool backed by the Azure SDK MCP server via stdio.

Requirements:
  - ``AZURE_APPCONFIG_ENDPOINT`` env var set (for App Configuration)
  - Azure credentials available (``DefaultAzureCredential``)
    - ``azsdk`` available on PATH
  - Network access to Azure SDK MCP server
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
from agent_framework.foundry import FoundryChatClient

import config.app_config as app_config
from config.app_config import get as cfg
from tools.azsdk_mcp_tools import create_azsdk_mcp_tool
from utils.azure_credential import get_credential


@pytest_asyncio.fixture(scope="module")
async def ai_client():
    """Initialise App Configuration and return a FoundryChatClient."""
    await app_config.init()
    return FoundryChatClient(
        project_endpoint=cfg("AI_FOUNDRY_PROJECT_ENDPOINT"),
        model=cfg("AI_FOUNDRY_AGENT_COMPLETION_MODEL"),
        credential=get_credential(),
    )


@pytest_asyncio.fixture(scope="module")
async def azsdk_mcp_tool():
    """Create the AZSDK MCP tool (requires App Configuration to be loaded)."""
    await app_config.init()
    return await create_azsdk_mcp_tool()


@pytest.mark.asyncio
async def test_azsdk_mcp_exposes_allowed_tools() -> None:
    await app_config.init()
    tool = await create_azsdk_mcp_tool()
    async with tool:
        names = {f.name for f in tool.functions}
    assert names == set(tool.allowed_tools)


@pytest.mark.asyncio
async def test_azsdk_get_release_plan_receives_work_item_id() -> None:
    await app_config.init()
    tool = await create_azsdk_mcp_tool()
    async with tool:
        # Work item 1 never exists, so the error proves the ID reached the server.
        result = await tool.call_tool("azsdk_get_release_plan", workItemId=1)
    text = result if isinstance(result, str) else " ".join(getattr(c, "text", None) or "" for c in result)
    assert "Work item 1" in text, text


@pytest.mark.asyncio
async def test_azsdk_mcp_search(ai_client, azsdk_mcp_tool) -> None:
    """Run an agent with the AZSDK MCP tool and search for doc in the azure-sdk org."""
    async with Agent(
        client=ai_client,
        name="AzsdkMcpTestAgent",
        instructions=(
            "You are a helpful assistant that can interact with Azure SDK MCP tools. "
            "Use the Azure SDK MCP tools to answer questions."
        ),
        tools=azsdk_mcp_tool,
    ) as agent:
        result = await agent.run(
            "Search for doc containing 'Test Proxy' in the 'internal' "
            "project of the azure-sdk organization."
        )
        text = result.text
        print(f"\nAgent response:\n{text}")
        assert text, "Agent returned an empty response"
