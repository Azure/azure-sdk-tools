"""Microsoft Learn MCP tools for the Azure SDK QA Bot Agent."""

from __future__ import annotations

import logging

from agent_framework import MCPStreamableHTTPTool

from tools import truncating_mcp_parser

logger = logging.getLogger(__name__)

_MSLEARN_MCP_URL = "https://learn.microsoft.com/api/mcp"
_MCP_REQUEST_TIMEOUT_SECS = 30


async def create_mslearn_mcp_tool() -> MCPStreamableHTTPTool:  # noqa: RUF029
    """Create the public Microsoft Learn remote MCP tool."""
    tool = MCPStreamableHTTPTool(
        name="microsoft-learn",
        url=_MSLEARN_MCP_URL,
        description=(
            "Search and fetch current official Microsoft Learn documentation "
            "and Microsoft code samples."
        ),
        approval_mode="never_require",
        load_prompts=False,
        request_timeout=_MCP_REQUEST_TIMEOUT_SECS,
        parse_tool_results=truncating_mcp_parser,
    )
    logger.info("Microsoft Learn MCP tool configured")
    return tool