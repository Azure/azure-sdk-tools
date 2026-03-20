"""Pipeline analysis tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps remote MCP
server to inspect pipelines, retrieve build logs, and analyze failures.
"""

from __future__ import annotations

import logging

import httpx
from agent_framework import MCPStreamableHTTPTool

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

# Default ADO MCP endpoint for the azure-sdk org.
_DEFAULT_ADO_MCP_URL = "https://mcp.dev.azure.com/azure-sdk"

# Azure DevOps resource ID for token acquisition.
_ADO_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"


class _AzureDevOpsAuth(httpx.Auth):
    """httpx auth handler that injects an Azure AD Bearer token for ADO."""

    def __init__(self) -> None:
        self._credential = get_credential()

    async def async_auth_flow(self, request: httpx.Request):
        token = await self._credential.get_token(_ADO_SCOPE)
        request.headers["Authorization"] = f"Bearer {token.token}"
        yield request


def create_ado_mcp_tool() -> MCPStreamableHTTPTool:
    """Create an MCPStreamableHTTPTool wired to the ADO remote MCP server.

    The MCP URL can be overridden via the ``ADO_MCP_URL`` app-config key.
    """
    url = cfg("ADO_MCP_URL", _DEFAULT_ADO_MCP_URL)

    http_client = httpx.AsyncClient(
        auth=_AzureDevOpsAuth(),
        headers={
            "X-MCP-Toolsets": "pipelines",
            "X-MCP-Readonly": "true",
        },
    )

    return MCPStreamableHTTPTool(
        name="ado-pipelines",
        url=url,
        description=(
            "Azure DevOps Pipelines MCP server for the azure-sdk organization. "
            "Use this tool to list pipeline runs, get build status, "
            "download build logs, and analyze pipeline failures."
        ),
        http_client=http_client,
    )