"""Azure SDK MCP tools for the Azure SDK QA Bot Agent.

Provides an MCP-based tool that connects to the Azure SDK MCP server
via stdio (``azsdk mcp``).
"""

from __future__ import annotations

import logging
import os

from agent_framework import MCPStdioTool

from config.app_config import get as cfg
from tools import truncating_mcp_parser

logger = logging.getLogger(__name__)

_DEFAULT_AZSDK_ORG = "azure-sdk"


async def create_azsdk_mcp_tool() -> MCPStdioTool:
    """Create an MCPStdioTool that launches the Azure SDK MCP server.

    The AZSDK org value is retained for prompt context and compatibility.
    Only exposes read-only pipeline analysis and release plan tools.
    """
    org = _DEFAULT_AZSDK_ORG
    env = {**os.environ}

    logger.info("Azure SDK MCP tool configured (org=%s)", org)

    allowed_tools = [
        "azsdk_analyze_pipeline",
        "azsdk_get_pipeline_status",
        "azsdk_get_pipeline_llm_artifacts",
        "azsdk_get_release_plan",
    ]

    return MCPStdioTool(
        name="azsdk-mcp-tools",
        command="azsdk",
        args=["mcp"],
        env=env,
        load_prompts=False,
        allowed_tools=allowed_tools,
        parse_tool_results=truncating_mcp_parser,
        description=(
            "Read-only Azure SDK MCP server tools. Use to analyze Azure Pipelines "
            "failures, get pipeline status and LLM artifacts, and look up "
            "release plans (by work item ID, release plan ID, or spec PR URL)"
        ),
    )
