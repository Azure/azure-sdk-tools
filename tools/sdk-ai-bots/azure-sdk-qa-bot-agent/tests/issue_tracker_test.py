"""Tests for provider-neutral issue state polling."""

from __future__ import annotations

from unittest.mock import AsyncMock, patch

import httpx
import pytest

from tools.ado_mcp_tools import get_ado_work_item_state
from tools.issue_tracker import get_issue_state


@pytest.mark.asyncio
async def test_get_ado_work_item_state() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/azure-sdk/internal/_apis/wit/workitems/456"
        assert request.url.params["fields"] == "System.State"
        assert request.headers["Authorization"].startswith("Bearer ")
        assert request.headers["Authorization"].endswith("ado-token")
        return httpx.Response(200, json={"fields": {"System.State": "Closed"}})

    client = httpx.AsyncClient(transport=httpx.MockTransport(handler))
    with (
        patch(
            "tools.ado_mcp_tools.resolve_token",
            new=AsyncMock(return_value="ado-token"),
        ),
        patch("tools.ado_mcp_tools.cfg", return_value="Closed"),
        patch("tools.ado_mcp_tools.httpx.AsyncClient", return_value=client),
    ):
        state = await get_ado_work_item_state(
            "https://dev.azure.com/azure-sdk/internal/_workitems/edit/456"
        )

    assert state == "closed"


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
