"""Azure DevOps pipeline tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps MCP server
via stdio (``npx @azure-devops/mcp``).  Exposes read-only pipeline
definition lookup and work-item reads so the agent can help users find
release / CI pipeline links and inspect release plans (work items in the
``Release`` project).

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

# Client-side read-only allow-list: the work-items domain also exposes write
# tools (wit_update_work_item, pipelines_run_pipeline, ...); restrict to reads.
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
    # work items (read-only) — release plan lookup
    "wit_query_by_wiql",
    "wit_get_work_item",
    "wit_get_work_items_batch_by_ids",
    "wit_list_work_item_comments",
    "wit_get_work_item_type",
]


async def create_ado_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure DevOps MCP server.

    Read-only: pipeline lookup and work-item/release-plan reads.
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
            "work-items",
            "-a",
            "envvar",
        ],
        env=env,
        load_prompts=False,
        allowed_tools=_ADO_ALLOWED_TOOLS,
        approval_mode="never_require",
        parse_tool_results=truncating_mcp_parser,
        description=(
            "Read-only Azure DevOps MCP tools. Use to (1) find release/CI "
            "pipeline definitions by name and get their links, and (2) read "
            "release plans — work items in the 'Release' project: resolve a "
            "dashboard release-plan id via WIQL on [Custom.ReleasePlanID], "
            "then read the work item and its API Spec / Package children."
        ),
    )
