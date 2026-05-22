"""Azure DevOps pipeline tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps MCP server
via stdio (``npx @azure-devops/mcp``).  Exposes pipeline definition
lookup so the agent can help users find release / CI pipeline links.

Authentication: Azure DevOps does not accept Foundry agent identities
as organization members, so the hosted agent cannot mint an ADO token
directly. An out-of-band job (a UAMI that IS an org member) refreshes
a usable ADO credential into Key Vault, and this module reads it from
there and injects it as ``ADO_MCP_AUTH_TOKEN`` so the MCP server
(launched with ``-a env``) can authenticate API calls.
"""

from __future__ import annotations

import logging
import os

from agent_framework import MCPStdioTool

from config.app_config import get as cfg
from tools import truncating_mcp_parser
from utils.azure_credential import get_credential
from utils.azure_keyvault import get_secret

logger = logging.getLogger(__name__)

_DEFAULT_ADO_ORG = "azure-sdk"
# Environment variable read by the ADO MCP server in ``-a env`` auth mode.
_ADO_TOKEN_ENV = "ADO_MCP_AUTH_TOKEN"
# Key Vault secret holding the ADO bearer token (shared with pipeline_tools.py).
_TOKEN_SECRET_NAME = "ado-token"
# AAD resource ID for Azure DevOps; used to mint a token via the agent
# identity as a fallback when no KV secret is present.
_ADO_RESOURCE_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"


async def create_ado_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure DevOps MCP server.

    Only exposes pipeline definition lookup tools to the agent.
    """
    org = cfg("ADO_ORG", _DEFAULT_ADO_ORG) or _DEFAULT_ADO_ORG
    env = {**os.environ}

    # Pull the ADO credential from Key Vault and inject it for the MCP
    # server's envvar auth mode. If the secret is absent, fall back to
    # the agent identity (works once ADO accepts it as an org member —
    # simply delete the KV secret to switch over).
    token: str | None = None
    source: str = ""
    try:
        kv_value = await get_secret(_TOKEN_SECRET_NAME)
        if kv_value and kv_value.strip():
            token = kv_value.strip()
    except Exception:
        logger.warning(
            "Failed to read KV secret '%s'; will try agent identity",
            _TOKEN_SECRET_NAME,
            exc_info=True,
        )

    if token is None:
        try:
            credential = get_credential()
            access_token = await credential.get_token(_ADO_RESOURCE_SCOPE)
            token = access_token.token
        except Exception:
            logger.warning(
                "Failed to mint ADO token via agent identity; ADO MCP server "
                "will start without %s",
                _ADO_TOKEN_ENV,
                exc_info=True,
            )

    if token:
        env[_ADO_TOKEN_ENV] = token
    else:
        logger.warning(
            "No ADO token available (KV secret '%s' empty and identity "
            "fallback failed); ADO MCP server will start without %s",
            _TOKEN_SECRET_NAME,
            _ADO_TOKEN_ENV,
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
