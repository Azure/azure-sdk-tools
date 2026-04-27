"""Azure DevOps pipeline tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps MCP server
via stdio (``npx @azure-devops/mcp``).  Exposes pipeline definition
lookup so the agent can help users find release / CI pipeline links.
"""

from __future__ import annotations

import logging
import os

from agent_framework import MCPStdioTool

from config.app_config import get as cfg

logger = logging.getLogger(__name__)

_DEFAULT_ADO_ORG = "azure-sdk"


async def create_ado_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure DevOps MCP server.

    Only exposes pipeline definition lookup tools to the agent.
    """
    org = cfg("ADO_ORG", _DEFAULT_ADO_ORG) or _DEFAULT_ADO_ORG
    env = {**os.environ}

    az_client_id = os.environ.get("UMI_BACKEND_CLIENT_ID")
    if az_client_id:
        env["AZURE_CLIENT_ID"] = az_client_id.strip()
        logger.info("ADO MCP tool configured with AZURE_CLIENT_ID (UMI)")
    else:
        logger.info(
            "UMI_BACKEND_CLIENT_ID not set — ADO MCP tool will use default managed identity"
        )

    logger.info("ADO MCP tool configured (org=%s)", org)

    return MCPStdioTool(
        name="ado-mcp-tools",
        command="npx",
        args=["-y", "@azure-devops/mcp", org, "-d", "core", "pipelines", "-a", "env"],
        env=env,
        load_prompts=False,
        description=(
            "Azure DevOps stdio MCP server tools for pipeline lookup. "
            "Use this tool to find release and CI pipeline definitions "
            "by name (e.g. 'go - armtestbase', 'python - azure-mgmt-testbase') "
            "and get pipeline links for users."
        ),
    )
