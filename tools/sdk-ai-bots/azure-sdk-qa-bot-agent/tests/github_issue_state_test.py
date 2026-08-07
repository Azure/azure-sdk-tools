"""Tests for deterministic GitHub issue-state polling."""

from __future__ import annotations

from unittest.mock import AsyncMock, patch

import httpx
import pytest

from tools.github_mcp_tools import get_github_issue_state


@pytest.mark.asyncio
async def test_get_github_issue_state() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/repos/Azure/azure-sdk-pr/issues/123"
        assert request.headers["Authorization"] == "Bearer test-token"
        return httpx.Response(200, json={"state": "closed"})

    client = httpx.AsyncClient(transport=httpx.MockTransport(handler))
    with (
        patch(
            "tools.github_mcp_tools._get_github_token",
            new=AsyncMock(return_value=("test-token", None)),
        ),
        patch("tools.github_mcp_tools.httpx.AsyncClient", return_value=client),
    ):
        state = await get_github_issue_state(
            "https://github.com/Azure/azure-sdk-pr/issues/123"
        )

    assert state == "closed"


@pytest.mark.asyncio
async def test_get_github_issue_state_rejects_non_issue_url() -> None:
    with pytest.raises(ValueError):
        await get_github_issue_state(
            "https://github.com/Azure/azure-sdk-pr/pull/123"
        )
