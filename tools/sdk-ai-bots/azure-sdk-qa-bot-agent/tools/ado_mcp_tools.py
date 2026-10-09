"""Azure DevOps pipeline tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps MCP server
via stdio (``npx @azure-devops/mcp``).  Exposes read-only pipeline
definition lookup so the agent can help users find release / CI pipeline
links.

Authentication: the MCP server is launched with
``-a env`` and acquires/refreshes ADO tokens itself via
its own Credential; it inherits our environment, so it resolves to
the agent identity in the hosted container and Azure CLI locally.
"""

from __future__ import annotations

import logging
import os

from agent_framework import MCPStdioTool

from config.app_config import get as cfg
from tools import truncating_mcp_parser

logger = logging.getLogger(__name__)

_DEFAULT_ADO_ORG = "azure-sdk"
# Pinned to match the copy baked into the image (Dockerfile ADO_MCP_VERSION)
# so `npx` resolves from cache instead of hitting the registry on cold start.
_ADO_MCP_PACKAGE = os.environ.get("ADO_MCP_PACKAGE", "@azure-devops/mcp@2.7.0")

# Client-side read-only allow-list: the pipelines domain also exposes write
# tools (pipelines_run_pipeline, ...); restrict to reads.
_ADO_ALLOWED_TOOLS: list[str] = [
    # core (read-only)
    "core_list_projects",
    "core_list_project_teams",
    "core_get_identity_ids",
    # pipelines / builds (read-only) — pipeline definition & run lookup
    "pipelines_get_build_definitions",
    "pipelines_get_build_definition_revisions",
    "pipelines_get_builds",
    "pipelines_get_build_status",
    "pipelines_get_build_changes",
    "pipelines_get_build_log",
    "pipelines_get_build_log_by_id",
    "pipelines_get_run",
    "pipelines_list_runs",
    "pipelines_list_artifacts",
    "pipelines_download_artifact",
]


async def create_ado_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure DevOps MCP server.

    Read-only: pipeline definition and run lookup.
    """
    org = cfg("ADO_ORG", _DEFAULT_ADO_ORG) or _DEFAULT_ADO_ORG
    env = {**os.environ}

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
            "env",
        ],
        env=env,
        load_prompts=False,
        allowed_tools=_ADO_ALLOWED_TOOLS,
        approval_mode="never_require",
        parse_tool_results=truncating_mcp_parser,
        description=(
            "Read-only Azure DevOps MCP tools. Use to find release/CI "
            "pipeline definitions by name and get their links."
        ),
    )
