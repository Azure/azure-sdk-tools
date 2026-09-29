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
from urllib.parse import quote

from agent_framework import MCPStdioTool
import httpx

from config.app_config import get as cfg
from models.feedback import AzureDevOpsIssueReference, parse_issue_reference
from tools import truncating_mcp_parser
from utils.ado_token import resolve_token

logger = logging.getLogger(__name__)

_DEFAULT_ADO_ORG = "azure-sdk"
# Environment variable read by the ADO MCP server in ``-a envvar`` auth mode.
_ADO_TOKEN_ENV = "ADO_MCP_AUTH_TOKEN"
# Pinned to match the copy baked into the image (Dockerfile ADO_MCP_VERSION)
# so `npx` resolves from cache instead of hitting the registry on cold start.
_ADO_MCP_PACKAGE = os.environ.get("ADO_MCP_PACKAGE", "@azure-devops/mcp@2.7.0")
_ADO_API_TIMEOUT_SECS = 10.0

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
_ADO_WRITE_TOOLS = (
    "wit_create_work_item",
    "wit_add_work_item_comment",
)


async def create_ado_mcp_tool(
    *,
    allow_issue_writes: bool = False,
) -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure DevOps MCP server.

    Read-only by default. Callers may opt into the narrowly allowed work-item
    write tools needed by the evolution agent.
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
    allowed_tools = list(_ADO_ALLOWED_TOOLS)
    description = (
        "Read-only Azure DevOps MCP tools. Use to (1) find release/CI "
        "pipeline definitions by name and get their links, and (2) read "
        "release plans — work items in the 'Release' project: resolve a "
        "dashboard release-plan id via WIQL on [Custom.ReleasePlanID], "
        "then read the work item and its API Spec / Package children."
    )
    if allow_issue_writes:
        allowed_tools.extend(_ADO_WRITE_TOOLS)
        description += (
            " This profile may also create Issue work items and add comments; "
            "it must not assign work items or update tags."
        )

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
        allowed_tools=allowed_tools,
        approval_mode="never_require",
        parse_tool_results=truncating_mcp_parser,
        description=description,
    )


async def get_ado_work_item_state(issue_url: str) -> str:
    """Return ``open`` or ``closed`` for a canonical Azure Boards work item."""
    reference = parse_issue_reference(issue_url)
    if not isinstance(reference, AzureDevOpsIssueReference):
        raise ValueError(f"Not an Azure Boards work item URL: {issue_url}")

    token = await resolve_token()
    api_url = (
        f"https://dev.azure.com/{quote(reference.organization, safe='')}/"
        f"{quote(reference.project, safe='')}"
        f"/_apis/wit/workitems/{reference.work_item_id}"
        "?fields=System.State&api-version=7.1"
    )
    headers = {
        "Authorization": "Bearer " + token,
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
