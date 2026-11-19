"""Offline regressions for the Azure SDK MCP tool boundary."""

import json
from unittest.mock import patch

import pytest
from mcp import types as mcp_types

from tools import truncating_mcp_parser
from tools.azsdk_mcp_tools import create_azsdk_mcp_tool


@pytest.mark.asyncio
async def test_azsdk_mcp_preserves_stdio_configuration_and_read_only_allowlist():
    with (
        patch("tools.azsdk_mcp_tools.MCPStdioTool") as tool_type,
        patch.dict("os.environ", {"TEST_MCP_ENV": "preserved"}, clear=True),
    ):
        tool = await create_azsdk_mcp_tool()

    assert tool is tool_type.return_value
    tool_type.assert_called_once_with(
        name="azsdk-mcp-tools",
        command="azsdk",
        args=["mcp"],
        env={"TEST_MCP_ENV": "preserved"},
        load_prompts=False,
        allowed_tools=[
            "azsdk_analyze_pipeline",
            "azsdk_get_pipeline_status",
            "azsdk_get_pipeline_llm_artifacts",
            "azsdk_get_release_plan",
            "azsdk_package_get_approval_status",
            "azsdk_apiview_get_review_url",
        ],
        parse_tool_results=truncating_mcp_parser,
        description=tool_type.call_args.kwargs["description"],
    )


@pytest.mark.asyncio
async def test_azsdk_mcp_excludes_review_requests_and_release_mutations():
    with patch("tools.azsdk_mcp_tools.MCPStdioTool") as tool_type:
        await create_azsdk_mcp_tool()

    allowed = set(tool_type.call_args.kwargs["allowed_tools"])
    assert allowed.isdisjoint({
        "azsdk_apiview_request_copilot_review",
        "azsdk_release_sdk",
        "azsdk_create_release_plan",
        "azsdk_update_release_plan",
    })


@pytest.mark.parametrize(
    "result",
    [
        {
            "isApproved": False,
            "finalSource": "APIView",
            "reason": "packageNameApproved",
            "apiView": {"statusCode": 201, "packageNameApproved": True},
        },
        {
            "isApproved": False,
            "finalSource": "None",
            "reason": "queryFailed",
            "apiView": {"statusCode": 403, "error": "Forbidden"},
        },
    ],
)
def test_mcp_parser_preserves_status_evidence_in_failed_responses(result):
    response = {
        "result": result,
        "response_error": "Release gate is not approved or could not be queried.",
        "operation_status": "Failed",
    }
    parsed = truncating_mcp_parser(mcp_types.CallToolResult(
        content=[mcp_types.TextContent(type="text", text=json.dumps(response))],
    ))
    assert json.loads(parsed) == response
