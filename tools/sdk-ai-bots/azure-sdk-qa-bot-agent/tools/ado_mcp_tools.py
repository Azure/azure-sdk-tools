"""Azure DevOps pipeline tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps MCP server
via stdio (``npx @azure-devops/mcp``).  Exposes pipeline definition
lookup so the agent can help users find release / CI pipeline links.

Authentication: acquires a bearer token for Azure DevOps via the shared
Azure credential chain and injects it as ``ADO_MCP_AUTH_TOKEN`` so the
MCP server (launched with ``-a env``) can authenticate API calls.
"""

from __future__ import annotations

import logging
import os

from azure.core.credentials_async import AsyncTokenCredential
from agent_framework import MCPStdioTool

from config.app_config import get as cfg
from tools import truncating_mcp_parser
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_DEFAULT_ADO_ORG = "azure-sdk"
# Environment variable read by the ADO MCP server in ``-a env`` auth mode.
_ADO_TOKEN_ENV = "ADO_MCP_AUTH_TOKEN"
# Azure DevOps resource ID used as the OAuth2 scope for token acquisition.
_ADO_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"


async def _get_ado_bearer_token(credential: AsyncTokenCredential) -> str:
    """Acquire a bearer token for Azure DevOps using the shared credential."""
    token = await credential.get_token(_ADO_SCOPE)
    return token.token


async def create_ado_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure DevOps MCP server.

    Only exposes pipeline definition lookup tools to the agent.
    """
    org = cfg("ADO_ORG", _DEFAULT_ADO_ORG) or _DEFAULT_ADO_ORG
    env = {**os.environ}

    # Acquire a bearer token and inject it for the MCP server's envvar auth mode.
    if _ADO_TOKEN_ENV not in env:
        try:
            credential = get_credential()
            env[_ADO_TOKEN_ENV] = await _get_ado_bearer_token(credential)
            logger.info("ADO bearer token acquired via Azure credential chain")
        except Exception:
            logger.warning(
                "Failed to acquire ADO bearer token via Azure credential; "
                "the MCP server will start without ADO_MCP_AUTH_TOKEN",
                exc_info=True,
            )

    logger.info("ADO MCP tool configured (org=%s)", org)

    return MCPStdioTool(
        name="ado-mcp-tools",
        command="npx",
        args=["-y", "@azure-devops/mcp", org, "-d", "core", "pipelines", "-a", "env"],
        env=env,
        load_prompts=False,
        parse_tool_results=truncating_mcp_parser,
        description=(
            "Azure DevOps stdio MCP server tools for pipeline lookup. "
            "Use this tool to find release and CI pipeline definitions "
            "by name (e.g. 'go - armtestbase', 'python - azure-mgmt-testbase') "
            "and get pipeline links for users."
        ),
    )
