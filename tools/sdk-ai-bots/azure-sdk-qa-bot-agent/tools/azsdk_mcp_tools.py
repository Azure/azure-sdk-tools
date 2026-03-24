"""Pipeline analysis tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure SDK MCP server
via stdio (``azsdk mcp``).
"""

from __future__ import annotations

import logging
import os

from agent_framework import MCPStdioTool

from config.app_config import get as cfg

logger = logging.getLogger(__name__)

_DEFAULT_AZSDK_ORG = "azure-sdk"


async def create_azsdk_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure SDK MCP server.

    The AZSDK org value is retained for prompt context and compatibility.
    """
    org = _DEFAULT_AZSDK_ORG
    env = {**os.environ}

    az_client_id = (
        os.environ.get("UMI_BACKEND_CLIENT_ID").strip()
    )
    if az_client_id:
        env["AZURE_CLIENT_ID"] = az_client_id
        logger.info("Azure SDK MCP tool configured with AZURE_CLIENT_ID")
    else:
        logger.warning("UMI_BACKEND_CLIENT_ID is not set")

    logger.info("Azure SDK MCP tool configured (org=%s)", org)

    return MCPStdioTool(
        name="azsdk-mcp-tools",
        command="azsdk",
        args=["mcp"],
        env=env,
        load_prompts=False,
        description=(
            "Azure SDK MCP server tools for Azure Pipelines analysis. "
            "Use this tool to analyze pipeline failures, get pipeline status, "
            "and retrieve pipeline LLM artifacts."
        ),
    )