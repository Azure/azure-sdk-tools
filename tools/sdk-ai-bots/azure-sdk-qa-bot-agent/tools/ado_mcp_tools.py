"""Azure DevOps pipeline tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure DevOps MCP server
via its installed ``mcp-server-azuredevops`` executable. Exposes read-only pipeline
definition lookup and work-item reads so the agent can help users find
release / CI pipeline links and inspect release plans (work items in the
``Release`` project).

Authentication: the MCP server is launched with
``-a env`` and acquires/refreshes ADO tokens itself via
its own Credential; it inherits our environment, so it resolves to
the agent identity in the hosted container and Azure CLI locally. Direct
work-item state polling uses the same shared credential chain.
"""

from __future__ import annotations

import logging
import os
from urllib.parse import quote

from agent_framework import MCPStdioTool
import httpx

from config.app_config import get as cfg
from models.feedback import AzureDevOpsIssueReference, parse_issue_reference
from tools import truncating_mcp_parser
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_DEFAULT_ADO_ORG = "azure-sdk"
_ADO_MCP_COMMAND = "mcp-server-azuredevops"
_ADO_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"
_ADO_API_TIMEOUT_SECS = 10.0

# Client-side read-only allow-list: the pipelines domain also exposes write
# tools (pipelines_run_pipeline, ...); restrict to reads.
_ADO_ALLOWED_TOOLS = (
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
)
_ADO_EVOLUTION_TOOLS = (
    "wit_query_by_wiql",
    "wit_get_work_item",
    "wit_list_work_item_comments",
    "wit_create_work_item",
    "wit_add_work_item_comment",
)


async def create_ado_mcp_tool() -> MCPStdioTool:
    """Create the general read-only Azure DevOps MCP profile."""
    return await _create_ado_mcp_tool(
        allowed_tools=_ADO_ALLOWED_TOOLS,
        domains=("core", "pipelines"),
        description=(
            "Read-only Azure DevOps MCP tools. Use to find release/CI "
            "pipeline definitions by name and get their links."
        ),
    )


async def create_evolution_ado_mcp_tool() -> MCPStdioTool:
    """Create the issue-only Azure DevOps MCP profile for evolution."""
    return await _create_ado_mcp_tool(
        allowed_tools=_ADO_EVOLUTION_TOOLS,
        domains=("work-items",),
        description=(
            "Azure Boards issue tools for the chatbot evolution workflow. "
            "May query and read work items, list comments, create Issue work "
            "items, and add comments. Must not access pipelines, project "
            "identities, artifacts, tags, or assignments."
        ),
    )


async def _create_ado_mcp_tool(
    *,
    allowed_tools: tuple[str, ...],
    domains: tuple[str, ...],
    description: str,
) -> MCPStdioTool:
    org = cfg("ADO_ORG", _DEFAULT_ADO_ORG) or _DEFAULT_ADO_ORG
    env = {**os.environ}

    logger.info("ADO MCP tool configured (org=%s)", org)
    return MCPStdioTool(
        name="ado-mcp-tools",
        command=_ADO_MCP_COMMAND,
        args=[
            org,
            "-d",
            *domains,
            "-a",
            "env",
        ],
        env=env,
        load_prompts=False,
        allowed_tools=list(allowed_tools),
        approval_mode="never_require",
        parse_tool_results=truncating_mcp_parser,
        description=description,
    )


async def get_ado_work_item_state(issue_url: str) -> str:
    """Return ``open`` or ``closed`` for a canonical Azure Boards work item."""
    reference = parse_issue_reference(issue_url)
    if not isinstance(reference, AzureDevOpsIssueReference):
        raise ValueError(f"Not an Azure Boards work item URL: {issue_url}")

    access_token = await get_credential().get_token(_ADO_SCOPE)
    api_url = (
        f"https://dev.azure.com/{quote(reference.organization, safe='')}/"
        f"{quote(reference.project, safe='')}"
        f"/_apis/wit/workitems/{reference.work_item_id}"
        "?fields=System.State&api-version=7.1"
    )
    headers = {
        "Authorization": "Bearer " + access_token.token,
        "Accept": "application/json",
    }
    async with httpx.AsyncClient(timeout=_ADO_API_TIMEOUT_SECS) as client:
        response = await client.get(api_url, headers=headers)
        response.raise_for_status()
    payload = response.json()
    state = payload.get("fields", {}).get("System.State")
    if not isinstance(state, str) or not state:
        raise RuntimeError(f"ADO returned no state for {issue_url}")

    closed_state = cfg("ADO_ISSUE_CLOSED_STATE", "Closed") or "Closed"
    return "closed" if state.casefold() == closed_state.casefold() else "open"
