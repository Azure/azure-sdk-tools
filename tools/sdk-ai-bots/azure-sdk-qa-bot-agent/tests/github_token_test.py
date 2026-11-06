"""Tests for GitHub App installation selection and token refresh."""

from datetime import datetime, timedelta, timezone
from unittest.mock import AsyncMock, patch

import httpx
import pytest

from tools import github_mcp_tools as github


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("owner", "configured_owner", "expected_owner"),
    [
        (None, "Azure", "Azure"),
        (None, None, "Azure"),
        (None, "CustomOrg", "CustomOrg"),
        ("Microsoft", "Azure", "Microsoft"),
    ],
)
async def test_acquire_token_selects_installation(
    owner: str | None, configured_owner: str | None, expected_owner: str, monkeypatch
) -> None:
    settings = {
        "GITHUB_APP_ID": "app-id",
        "GITHUB_APP_KEY_NAME": "key-name",
        "GITHUB_APP_KEYVAULT_URL": "https://test.vault.azure.net",
    }
    if configured_owner is not None:
        settings["GITHUB_APP_INSTALLATION_OWNER"] = configured_owner
    monkeypatch.setattr(github, "cfg", settings.get)
    expires = datetime.now(timezone.utc) + timedelta(hours=1)
    with (
        patch.object(github, "_create_app_jwt", new=AsyncMock(return_value="jwt")),
        patch.object(
            github,
            "_get_installation_token",
            new=AsyncMock(return_value=("token", expires)),
        ) as mint,
    ):
        assert await github._acquire_github_app_token(owner) == ("token", expires)
    mint.assert_awaited_once_with("jwt", expected_owner)


@pytest.mark.asyncio
async def test_static_token_retains_precedence(monkeypatch) -> None:
    monkeypatch.setenv("GITHUB_TOKEN", "static-token")
    with patch.object(github, "_get_github_app_token", new=AsyncMock()) as mint:
        assert await github._get_github_token("Microsoft") == ("static-token", None)
    mint.assert_not_awaited()


@pytest.mark.asyncio
async def test_static_token_does_not_refresh() -> None:
    manager = github._GitHubTokenManager("static-token", None, {}, "Microsoft")
    with patch.object(github, "_acquire_github_app_token", new=AsyncMock()) as mint:
        headers = await manager.get_headers()
    assert headers["Authorization"] == "Bearer static-token"
    mint.assert_not_awaited()


@pytest.mark.asyncio
async def test_get_token_forwards_installation(monkeypatch) -> None:
    monkeypatch.delenv("GITHUB_TOKEN", raising=False)
    expires = datetime.now(timezone.utc) + timedelta(hours=1)
    with (
        patch.object(github, "_create_app_jwt", new=AsyncMock(return_value="jwt")),
        patch.object(
            github,
            "_get_installation_token",
            new=AsyncMock(return_value=("token", expires)),
        ) as mint,
        patch.object(
            github,
            "cfg",
            side_effect=lambda key, default=None: {
                "GITHUB_APP_ID": "app-id",
                "GITHUB_APP_KEY_NAME": "key-name",
                "GITHUB_APP_KEYVAULT_URL": "https://test.vault.azure.net",
            }.get(key, default),
        ),
    ):
        assert await github._get_github_token("Microsoft") == ("token", expires)
    mint.assert_awaited_once_with("jwt", "Microsoft")


@pytest.mark.asyncio
@pytest.mark.parametrize("owner", ["Azure", "Microsoft"])
async def test_tool_preserves_installation_on_refresh(owner: str, monkeypatch) -> None:
    monkeypatch.setattr(github, "cfg", lambda key, default=None: "Azure")
    expired = datetime.now(timezone.utc) - timedelta(minutes=1)
    refreshed = datetime.now(timezone.utc) + timedelta(hours=1)
    with (
        patch.object(
            github, "_get_github_token", new=AsyncMock(return_value=("old", expired))
        ) as initial,
        patch.object(
            github, "_validate_mcp_endpoint", new=AsyncMock(return_value=True)
        ),
        patch.object(github, "MCPStreamableHTTPTool") as tool_factory,
        patch.object(
            github,
            "_acquire_github_app_token",
            new=AsyncMock(return_value=("new", refreshed)),
        ) as refresh,
    ):
        if owner == "Azure":
            await github.create_github_mcp_tool()
        else:
            await github.create_github_mcp_tool(installation_owner=owner)
        client = tool_factory.call_args.kwargs["http_client"]
        try:
            request = httpx.Request("POST", github._GITHUB_MCP_URL)
            for hook in client.event_hooks["request"]:
                await hook(request)
            assert request.headers["Authorization"] == "Bearer new"
            for hook in client.event_hooks["request"]:
                await hook(request)
            initial.assert_awaited_once_with(owner)
            refresh.assert_awaited_once_with(owner)
        finally:
            await client.aclose()
