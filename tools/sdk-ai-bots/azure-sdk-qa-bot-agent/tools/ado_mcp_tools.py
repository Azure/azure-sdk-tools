"""Pipeline analysis tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps MCP server
via stdio (``npx @azure-devops/mcp``), authenticated with an Azure AD
token passed through the ``ADO_MCP_AUTH_TOKEN`` environment variable.
"""

from __future__ import annotations

import logging
import os

from agent_framework import MCPStdioTool

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_DEFAULT_ADO_ORG = "azure-sdk"

# Azure DevOps resource ID for token acquisition.
_ADO_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"


async def create_ado_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the ADO MCP server via npx.

    Acquires an Azure AD token and sets it as the
    ``ADO_MCP_AUTH_TOKEN`` environment variable so the ADO MCP server
    authenticates via the ``--authentication envvar`` mode.

    The ADO org can be overridden via the ``ADO_MCP_ORG`` app-config key.
    """
    org = cfg("ADO_MCP_ORG", _DEFAULT_ADO_ORG)

    # Acquire token and pass it to the subprocess via env.
    credential = get_credential()
    token = await credential.get_token(_ADO_SCOPE)

    logger.info("ADO MCP tool configured (org=%s, auth=envvar)", org)

    return MCPStdioTool(
        name="ado-mcp-tools",
        command="npx",
        args=["--no-install", "@azure-devops/mcp", org, "--authentication", "envvar", "--domains", "pipelines", "search"],
        env={**os.environ, "ADO_MCP_AUTH_TOKEN": token.token},
        load_prompts=False,
        description=(
            "Azure DevOps Pipelines MCP server for the azure-sdk organization. "
            "Use this tool to list pipeline runs, get build status, "
            "download build logs, and analyze pipeline failures."
        ),
    )