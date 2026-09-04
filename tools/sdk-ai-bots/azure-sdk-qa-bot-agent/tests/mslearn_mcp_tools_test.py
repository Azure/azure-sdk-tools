"""Unit tests for Microsoft Learn MCP tool configuration."""

from unittest.mock import patch

import pytest

from tools.mslearn_mcp_tools import create_mslearn_mcp_tool


@pytest.mark.asyncio
async def test_create_mslearn_mcp_tool() -> None:
    with patch("tools.mslearn_mcp_tools.MCPStreamableHTTPTool") as tool_type:
        tool = await create_mslearn_mcp_tool()

    assert tool is tool_type.return_value
    tool_type.assert_called_once_with(
        name="microsoft-learn",
        url="https://learn.microsoft.com/api/mcp",
        description=(
            "Search and fetch public Azure MCP Server documentation only. "
            "Scope searches to learn.microsoft.com/azure/developer/azure-mcp-server/ "
            "and fetch only results under that path. Do not use this tool for "
            "internal onboarding, merge requirements, team process, support history, "
            "or prior decisions."
        ),
        allowed_tools=["microsoft_docs_search", "microsoft_docs_fetch"],
        approval_mode="never_require",
        load_prompts=False,
        request_timeout=30,
        parse_tool_results=tool_type.call_args.kwargs["parse_tool_results"],
    )