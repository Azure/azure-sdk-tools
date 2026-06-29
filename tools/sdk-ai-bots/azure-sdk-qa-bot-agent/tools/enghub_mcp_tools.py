"""Engineering Hub (eng.ms) tools for the Azure SDK QA Bot Agent.

Provides a local MCP tool (MCPStreamableHTTPTool) that connects to the
Engineering Hub remote MCP server directly from the agent container.

Authentication uses Entra ID: a short-lived bearer token is minted from
the shared credential chain (managed identity / pipelines / CLI) and
refreshed automatically before expiry. A custom httpx event hook injects
the token on every outbound request (including the MCP handshake), so the
initial ``initialize`` / ``tools/list`` calls are authenticated too.

Config keys (App Configuration / ``.env``):

* ``ENGHUB_MCP_URL``    — MCP endpoint (default ``https://eng.ms/mcp``)
* ``ENGHUB_MCP_SCOPE``  — Entra token scope (e.g. ``api://<app-id>/.default``)
"""

from __future__ import annotations

import asyncio
import logging
import time as _time

import httpx
from agent_framework import MCPStreamableHTTPTool

from config.app_config import get as cfg
from tools import truncating_mcp_parser
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_DEFAULT_ENGHUB_MCP_URL = "https://eng.ms/mcp"
# eng.ms Entra app id (client_id from the eng.ms sign-in flow).
_DEFAULT_ENGHUB_MCP_SCOPE = "api://91c02195-fbbd-4bc8-8c69-5b75dadc5672/.default"
# Refresh the token 5 minutes before it expires.
_TOKEN_REFRESH_BUFFER_SECS = 5 * 60
_MCP_REQUEST_TIMEOUT_SECS = 60

# Client-side allowed tool names (defence in depth). Trim to what you need.
_ENGHUB_ALLOWED_TOOLS: list[str] | None = None


class _EntraTokenManager:
    """Caches an Entra bearer token and refreshes it before expiry."""

    def __init__(self, scope: str) -> None:
        self._scope = scope
        self._token = ""
        self._expires_at = 0.0
        self._lock = asyncio.Lock()

    async def get_headers(self, _kwargs: dict | None = None) -> dict[str, str]:
        if self._needs_refresh():
            await self._refresh_once()
        return {"Authorization": f"Bearer {self._token}"}

    def _needs_refresh(self) -> bool:
        return _time.time() >= self._expires_at - _TOKEN_REFRESH_BUFFER_SECS

    async def _refresh_once(self) -> None:
        async with self._lock:
            if not self._needs_refresh():
                return
            cred = get_credential()
            tok = await cred.get_token(self._scope)
            self._token = tok.token
            self._expires_at = tok.expires_on


async def create_enghub_mcp_tool() -> MCPStreamableHTTPTool | None:
    """Create a local MCP tool that connects to the Engineering Hub server."""
    url = cfg("ENGHUB_MCP_URL", _DEFAULT_ENGHUB_MCP_URL) or _DEFAULT_ENGHUB_MCP_URL
    scope = _DEFAULT_ENGHUB_MCP_SCOPE

    token_mgr = _EntraTokenManager(scope)

    async def _inject_auth(request: httpx.Request) -> None:  # noqa: RUF029
        for key, value in (await token_mgr.get_headers()).items():
            request.headers[key] = value

    http_client = httpx.AsyncClient(
        follow_redirects=True,
        timeout=httpx.Timeout(_MCP_REQUEST_TIMEOUT_SECS, read=_MCP_REQUEST_TIMEOUT_SECS),
        event_hooks={"request": [_inject_auth]},
    )

    logger.info("Engineering Hub MCP tool configured (url=%s)", url)

    return MCPStreamableHTTPTool(
        name="enghub",
        url=url,
        description=(
            "Engineering Hub MCP server. Use this tool to query eng.ms "
            "engineering docs, processes, and internal knowledge."
        ),
        approval_mode="never_require",
        allowed_tools=_ENGHUB_ALLOWED_TOOLS,
        load_prompts=False,
        request_timeout=_MCP_REQUEST_TIMEOUT_SECS,
        http_client=http_client,
        parse_tool_results=truncating_mcp_parser,
    )
