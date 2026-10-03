"""Tests for deterministic GitHub issue-state polling."""

from __future__ import annotations

import json
from unittest.mock import AsyncMock, patch

import httpx
import pytest

from tools.github_mcp_tools import (
    assign_issue_to_copilot,
    get_github_issue_details,
    get_github_issue_state,
)


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
async def test_get_github_issue_details() -> None:
    def handler(_request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200,
            json={
                "title": "Fix guidance",
                "body": "Issue body",
                "state": "open",
                "labels": [{"name": "feedback-agent"}, "invalid"],
                "created_at": "2026-08-01T00:00:00Z",
                "updated_at": "2026-08-02T00:00:00Z",
                "closed_at": None,
            },
        )

    client = httpx.AsyncClient(transport=httpx.MockTransport(handler))
    with (
        patch(
            "tools.github_mcp_tools._get_github_token",
            new=AsyncMock(return_value=("test-token", None)),
        ),
        patch("tools.github_mcp_tools.httpx.AsyncClient", return_value=client),
    ):
        issue = await get_github_issue_details(
            "https://github.com/Azure/azure-sdk-pr/issues/123"
        )

    assert issue.title == "Fix guidance"
    assert issue.state == "open"
    assert issue.labels == ["feedback-agent"]


@pytest.mark.asyncio
async def test_get_github_issue_state_rejects_non_issue_url() -> None:
    with pytest.raises(ValueError):
        await get_github_issue_state(
            "https://github.com/Azure/azure-sdk-pr/pull/123"
        )


@pytest.mark.parametrize(
    ("issue_repository", "target_repository", "requested_branch", "target_branch"),
    [
        ("Azure/azure-sdk-pr", "Azure/azure-sdk-tools", "ignored", "main"),
        (
            "Azure/azure-sdk-for-java",
            "Azure/azure-sdk-for-java",
            "main",
            "main",
        ),
    ],
)
@pytest.mark.asyncio
async def test_assign_issue_to_copilot_enforces_target_repository(
    issue_repository: str,
    target_repository: str,
    requested_branch: str,
    target_branch: str,
) -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == f"/repos/{issue_repository}/issues/321/assignees"
        assert request.headers["Authorization"].startswith("Bearer ")
        assert request.headers["Authorization"].endswith("user-token")
        payload = json.loads(request.content)
        assert payload["assignees"] == ["copilot-swe-agent[bot]"]
        assert payload["agent_assignment"] == {
            "target_repo": target_repository,
            "base_branch": target_branch,
            "custom_instructions": "Apply the validated documentation fix.",
        }
        return httpx.Response(
            200,
            json={"assignees": [{"login": "copilot-swe-agent[bot]"}]},
        )

    client = httpx.AsyncClient(transport=httpx.MockTransport(handler))
    with (
        patch(
            "tools.github_mcp_tools.get_secret",
            new=AsyncMock(return_value="user-token"),
        ),
        patch(
            "tools.github_mcp_tools.cfg",
            return_value="copilot-token-secret",
        ),
        patch("tools.github_mcp_tools.httpx.AsyncClient", return_value=client),
    ):
        result = await assign_issue_to_copilot(
            issue_url=f"https://github.com/{issue_repository}/issues/321",
            base_branch=requested_branch,
            custom_instructions="Apply the validated documentation fix.",
        )

    assert result.assigned is True
