"""Tests for provider-neutral issue state polling."""

from __future__ import annotations

import json
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import httpx
import pytest

from tools.ado_mcp_tools import (
    create_ado_issue,
    create_evolution_ado_mcp_tool,
    get_ado_work_item_state,
)
from tools.issue_tracker import get_issue_state
from utils.knowledge_config import KbIssueTarget, KbTarget

_ADO_SOURCE_ID = "internal_wiki"
_ADO_SOURCE_URL = (
    "https://azure-sdk@dev.azure.com/azure-sdk/internal/_git/internal.wiki"
)
_ADO_SOURCE_TARGET = KbTarget(
    source_url=_ADO_SOURCE_URL,
    branch="wikiMaster",
    path="",
    scope=_ADO_SOURCE_ID,
    issue_target=KbIssueTarget(
        provider="azure-devops",
        organization="azure-sdk",
        project="internal",
    ),
)


@pytest.mark.asyncio
async def test_get_ado_work_item_state() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/azure-sdk/internal/_apis/wit/workitems/456"
        assert request.url.params["fields"] == "System.State"
        assert request.headers["Authorization"].startswith("Bearer ")
        assert request.headers["Authorization"].endswith("ado-token")
        return httpx.Response(200, json={"fields": {"System.State": "Closed"}})

    client = httpx.AsyncClient(transport=httpx.MockTransport(handler))
    credential = AsyncMock()
    credential.get_token.return_value = SimpleNamespace(token="ado-token")
    with (
        patch(
            "tools.ado_mcp_tools.get_credential",
            return_value=credential,
        ),
        patch("tools.ado_mcp_tools.cfg", return_value="Closed"),
        patch("tools.ado_mcp_tools.httpx.AsyncClient", return_value=client),
    ):
        state = await get_ado_work_item_state(
            "https://dev.azure.com/azure-sdk/internal/_workitems/edit/456"
        )

    assert state == "closed"


@pytest.mark.asyncio
async def test_evolution_ado_profile_exposes_only_issue_tools() -> None:
    mcp_tool = object()
    with (
        patch("tools.ado_mcp_tools.cfg", return_value="azure-sdk"),
        patch(
            "tools.ado_mcp_tools.MCPStdioTool",
            return_value=mcp_tool,
        ) as constructor,
    ):
        result = await create_evolution_ado_mcp_tool()

    assert result is mcp_tool
    kwargs = constructor.call_args.kwargs
    assert kwargs["command"] == "mcp-server-azuredevops"
    assert kwargs["args"] == [
        "azure-sdk",
        "-d",
        "work-items",
        "-a",
        "env",
    ]
    assert kwargs["allowed_tools"] == [
        "wit_query_by_wiql",
        "wit_get_work_item",
        "wit_list_work_item_comments",
        "wit_add_work_item_comment",
    ]


@pytest.mark.asyncio
async def test_create_ado_issue_uses_configured_project_and_issue_type() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == (
            "/azure-sdk/internal/_apis/wit/workitems/$Issue"
        )
        assert request.headers["Authorization"].endswith("ado-token")
        assert request.headers["Content-Type"] == "application/json-patch+json"
        assert json.loads(request.content) == [
            {
                "op": "add",
                "path": "/fields/System.Title",
                "value": "Fix guidance",
            },
            {
                "op": "add",
                "path": "/fields/System.Description",
                "value": "Validated issue body.",
            },
            {
                "op": "add",
                "path": "/multilineFieldsFormat/System.Description",
                "value": "Markdown",
            },
        ]
        return httpx.Response(200, json={"id": 456})

    client = httpx.AsyncClient(transport=httpx.MockTransport(handler))
    credential = AsyncMock()
    credential.get_token.return_value = SimpleNamespace(token="ado-token")
    with (
        patch(
            "tools.ado_mcp_tools.get_kb_targets",
            new=AsyncMock(return_value=(_ADO_SOURCE_TARGET,)),
        ),
        patch("tools.ado_mcp_tools.get_credential", return_value=credential),
        patch("tools.ado_mcp_tools.httpx.AsyncClient", return_value=client),
    ):
        result = await create_ado_issue(
            source_id=_ADO_SOURCE_ID,
            source_url=_ADO_SOURCE_URL,
            title="Fix guidance",
            description="Validated issue body.",
        )

    assert result.issue_url == (
        "https://dev.azure.com/azure-sdk/internal/_workitems/edit/456"
    )


@pytest.mark.asyncio
async def test_create_ado_issue_rejects_unconfigured_source_url() -> None:
    with (
        patch(
            "tools.ado_mcp_tools.get_kb_targets",
            new=AsyncMock(return_value=(_ADO_SOURCE_TARGET,)),
        ),
        pytest.raises(ValueError, match="one configured issue target"),
    ):
        await create_ado_issue(
            source_id=_ADO_SOURCE_ID,
            source_url="https://example.com/unconfigured",
            title="Fix guidance",
            description="Validated issue body.",
        )


@pytest.mark.asyncio
async def test_get_issue_state_dispatches_to_ado() -> None:
    with patch(
        "tools.issue_tracker.get_ado_work_item_state",
        new=AsyncMock(return_value="open"),
    ) as get_state:
        state = await get_issue_state(
            "https://dev.azure.com/azure-sdk/internal/_workitems/edit/456"
        )

    assert state == "open"
    get_state.assert_awaited_once()


@pytest.mark.asyncio
async def test_get_issue_state_dispatches_to_github() -> None:
    with patch(
        "tools.issue_tracker.get_github_issue_state",
        new=AsyncMock(return_value="closed"),
    ) as get_state:
        state = await get_issue_state(
            "https://github.com/Azure/azure-sdk-tools/issues/321"
        )

    assert state == "closed"
    get_state.assert_awaited_once()
