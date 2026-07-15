"""Azure DevOps pipeline tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps MCP server
via stdio (``npx @azure-devops/mcp``).  Exposes pipeline definition
lookup so the agent can help users find release / CI pipeline links.

Authentication: Azure DevOps does not accept Foundry agent identities
as organization members, so the hosted agent cannot mint an ADO token
directly. An out-of-band job (a UAMI that IS an org member) refreshes
a usable ADO credential into Key Vault, and this module reads it from
there and injects it as ``ADO_MCP_AUTH_TOKEN`` so the MCP server
(launched with ``-a envvar``) can authenticate API calls.
"""

from __future__ import annotations

import logging
import os

from agent_framework import MCPStdioTool

from config.app_config import get as cfg
from tools import truncating_mcp_parser
from utils.ado_token import resolve_token

logger = logging.getLogger(__name__)

_DEFAULT_ADO_ORG = "azure-sdk"
# Environment variable read by the ADO MCP server in ``-a envvar`` auth mode.
_ADO_TOKEN_ENV = "ADO_MCP_AUTH_TOKEN"
# Pinned to match the copy baked into the image (Dockerfile ADO_MCP_VERSION)
# so `npx` resolves from cache instead of hitting the registry on cold start.
_ADO_MCP_PACKAGE = os.environ.get("ADO_MCP_PACKAGE", "@azure-devops/mcp@2.7.0")


async def create_ado_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure DevOps MCP server.

    Only exposes pipeline definition lookup tools to the agent.
    """
    org = cfg("ADO_ORG", _DEFAULT_ADO_ORG) or _DEFAULT_ADO_ORG
    env = {**os.environ}

    # Pull the ADO credential via the shared resolver (KV-first, with
    # JIT caching) and inject it for the MCP server's envvar auth mode.
    try:
        token = await resolve_token()
        env[_ADO_TOKEN_ENV] = token
    except Exception:
        logger.warning(
            "Failed to resolve ADO token; ADO MCP server will start " "without %s",
            _ADO_TOKEN_ENV,
            exc_info=True,
        )

    logger.info("ADO MCP tool configured (org=%s)", org)

    return MCPStdioTool(
        name="ado-mcp-tools",
        command="npx",
        args=[
            "-y",
            _ADO_MCP_PACKAGE,
            org,
            "-d",
            "core",
            "pipelines",
            "-a",
            "envvar",
        ],
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
