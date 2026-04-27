"""GitHub tools for the Azure SDK QA Bot Agent.

Provides a local MCP tool (MCPStreamableHTTPTool) that connects to
GitHub's remote MCP server directly from the agent container.
Authentication mirrors the Go backend: a GitHub App JWT is signed via
Azure Key Vault and exchanged for a short-lived installation token,
with automatic refresh before expiry (same 5-minute buffer as the Go
code).

Using a local MCP client instead of server-side delegation means:
- Full tool-call logging in the container (agent framework traces every
  MCP request/response).
- Token refresh runs in-process — no reliance on Foundry proxy state.
"""

from __future__ import annotations

import asyncio
import base64
import hashlib
import json
import logging
import os
import time as _time
from datetime import datetime, timedelta, timezone

import httpx
from azure.keyvault.keys.crypto.aio import CryptographyClient
from azure.keyvault.keys.crypto import SignatureAlgorithm
from agent_framework import MCPStreamableHTTPTool

from config.app_config import get as cfg
from tools import truncating_mcp_parser
from utils.azure_credential import get_frontend_credential

logger = logging.getLogger(__name__)

_GITHUB_API = "https://api.github.com"
_GITHUB_MCP_URL = "https://api.githubcopilot.com/mcp/"
_DEFAULT_INSTALLATION_OWNER = "Azure"
_GITHUB_TOKEN_ENV = "GITHUB_TOKEN"
# Refresh the token 5 minutes before it expires.
_TOKEN_REFRESH_BUFFER_SECS = 5 * 60
# GitHub MCP server headers for toolset configuration.
# See https://github.com/github/github-mcp-server?tab=readme-ov-file#tool-configuration
_GITHUB_MCP_HEADERS = {
    "X-MCP-Toolsets": "repos,issues,actions,pull_requests",
    "X-MCP-Readonly": "true",
}
# Client-side allowed tool names (defence in depth).
# Server-side filtering is handled by X-MCP-Toolsets + X-MCP-Readonly headers.
# This list restricts what the *model* is allowed to invoke via the Foundry API.
# See https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/tool-best-practice
# and https://github.com/github/github-mcp-server for tool names.
_GITHUB_ALLOWED_TOOLS: list[str] = [
    # repos (read-only)
    "get_file_contents",
    "search_repositories",
    "search_code",
    "list_branches",
    "get_commit",
    "list_commits",
    # issues (read-only)
    "get_issue",
    "list_issues",
    "search_issues",
    "get_issue_comments",
    # pull_requests (read-only)
    "get_pull_request",
    "list_pull_requests",
    "get_pull_request_files",
    "get_pull_request_status",
    "get_pull_request_comments",
    "get_pull_request_reviews",
    # actions (read-only)
    "list_workflow_runs",
    "get_workflow_run",
    "get_workflow_run_logs",
]
# HTTP timeout for MCP endpoint validation and GitHub API calls.
_MCP_VALIDATION_TIMEOUT_SECS = 10.0
_GITHUB_API_TIMEOUT_SECS = 10.0
# JWT timing: clock-skew buffer and expiration (seconds).
_JWT_CLOCK_SKEW_SECS = 10
_JWT_EXPIRY_SECS = 600
# Fallback token lifetime when GitHub doesn't return expires_at.
_DEFAULT_TOKEN_LIFETIME_HOURS = 1
# Background token refresh interval (seconds).
_DEFAULT_REFRESH_INTERVAL_SECS = 30
# MCP request timeout (seconds) — GitHub MCP may take time for large repos.
_MCP_REQUEST_TIMEOUT_SECS = 60

# Keep strong references to background tasks so they are not garbage collected.
_background_refresh_tasks: list[asyncio.Task] = []


class _GitHubTokenManager:
    """Thread-safe, auto-refreshing GitHub installation token manager.

    Provides fresh auth headers to ``MCPStreamableHTTPTool`` via a
    ``header_provider`` callback.  Token refresh happens proactively in a
    background task so that MCP calls never block on token acquisition.
    """

    def __init__(self, token: str, expires_at: datetime | None) -> None:
        self._token = token
        self._expires_at = expires_at
        self._lock = asyncio.Lock()

    @property
    def is_static(self) -> bool:
        return self._expires_at is None

    def get_headers(self, _kwargs: dict | None = None) -> dict[str, str]:
        """Return current auth + toolset headers (called per-request)."""
        return {
            **_GITHUB_MCP_HEADERS,
            "Authorization": f"Bearer {self._token}",
        }

    def _needs_refresh(self) -> bool:
        if self._expires_at is None:
            return False
        buffer = timedelta(seconds=_TOKEN_REFRESH_BUFFER_SECS)
        return datetime.now(timezone.utc) >= self._expires_at - buffer

    async def _refresh_once(self) -> None:
        async with self._lock:
            if not self._needs_refresh():
                return
            logger.info("Refreshing GitHub App installation token...")
            token, new_expires_at = await _acquire_github_app_token()
            self._token = token
            self._expires_at = new_expires_at
            logger.info(
                "GitHub token refreshed, expires at %s",
                new_expires_at.isoformat(),
            )

    def start_background_refresh(
        self, interval_seconds: int = _DEFAULT_REFRESH_INTERVAL_SECS
    ) -> None:
        """Start a background task that proactively refreshes the token."""

        async def _loop() -> None:
            while True:
                try:
                    await self._refresh_once()
                except Exception as ex:
                    logger.exception("GitHub token background refresh failed: %s", ex)
                await asyncio.sleep(interval_seconds)

        task = asyncio.get_running_loop().create_task(_loop())
        _background_refresh_tasks.append(task)
        logger.info("Started GitHub token background refresh loop")


async def _validate_mcp_endpoint(token: str) -> bool:
    """Validate that the remote GitHub MCP endpoint is reachable.

    Sends a POST request that mimics what the Foundry runtime does when
    it enumerates MCP tools. This catches protocol-level failures
    (e.g. 415 UnsupportedMediaType) that a simple GET would miss.

    Returns ``False`` when the endpoint is unreachable, returns auth
    errors, or rejects the request with a transport error.
    """
    # Use the exact Content-Type that Foundry's internal MCP client sends.
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/json, text/event-stream",
        "Content-Type": "application/json; charset=utf-8",
    }
    # Minimal MCP "initialize" request — the response content doesn't
    # matter; we only care whether the endpoint accepts the request.
    body = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {
            "protocolVersion": "2025-03-26",
            "capabilities": {},
            "clientInfo": {"name": "health-check", "version": "0.1.0"},
        },
    }
    try:
        async with httpx.AsyncClient(timeout=_MCP_VALIDATION_TIMEOUT_SECS) as client:
            resp = await client.post(_GITHUB_MCP_URL, headers=headers, json=body)
        if resp.status_code in (401, 403):
            logger.error(
                "GitHub MCP auth validation failed (status=%s)",
                resp.status_code,
            )
            return False
        if resp.status_code == 415:
            logger.error(
                "GitHub MCP endpoint returned 415 UnsupportedMediaType — "
                "the remote server likely has a protocol incompatibility. "
                "GitHub MCP tool will be disabled until the endpoint is fixed."
            )
            return False
        if resp.status_code >= 500:
            logger.error(
                "GitHub MCP endpoint returned server error (status=%s)",
                resp.status_code,
            )
            return False
        return True
    except Exception as ex:
        logger.warning("GitHub MCP endpoint health check failed: %s", ex)
        return False


# -- helpers ---------------------------------------------------------------


def _b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


async def _sign_with_keyvault(vault_url: str, key_name: str, digest: bytes) -> bytes:
    """Sign *digest* with the RSA key in Key Vault using RS256.

    Constructs the key ID directly to avoid needing the ``keys/get``
    permission — only ``keys/sign`` is required (matches Go backend).
    """
    credential = get_frontend_credential()
    key_id = f"{vault_url.rstrip('/')}/keys/{key_name}"
    crypto = CryptographyClient(key_id, credential=credential)
    try:
        result = await crypto.sign(SignatureAlgorithm.rs256, digest)
        return result.signature
    finally:
        await crypto.close()


async def _create_app_jwt(vault_url: str, key_name: str, app_id: str) -> str:
    """Create a GitHub App JWT signed via Azure Key Vault (RS256)."""
    header = _b64url(json.dumps({"alg": "RS256", "typ": "JWT"}).encode())
    now = int(_time.time())
    payload = _b64url(
        json.dumps(
            {
                "iat": now - _JWT_CLOCK_SKEW_SECS,
                "exp": now + _JWT_EXPIRY_SECS,
                "iss": app_id,
            }
        ).encode()
    )
    unsigned = f"{header}.{payload}"
    digest = hashlib.sha256(unsigned.encode()).digest()
    sig = await _sign_with_keyvault(vault_url, key_name, digest)
    return f"{unsigned}.{_b64url(sig)}"


async def _get_installation_token(jwt: str, owner: str) -> tuple[str, datetime]:
    """Exchange a GitHub App JWT for an installation access token.

    Returns ``(token, expires_at)`` where *expires_at* is a timezone-aware
    UTC :class:`datetime`.
    """
    headers = {
        "Authorization": f"Bearer {jwt}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    async with httpx.AsyncClient(timeout=_GITHUB_API_TIMEOUT_SECS) as client:
        resp = await client.get(f"{_GITHUB_API}/app/installations", headers=headers)
        resp.raise_for_status()
        installations = resp.json()
        inst_id = None
        for inst in installations:
            if inst["account"]["login"].lower() == owner.lower():
                inst_id = inst["id"]
                break
        if inst_id is None:
            raise RuntimeError(f"No GitHub App installation found for owner '{owner}'")

        resp = await client.post(
            f"{_GITHUB_API}/app/installations/{inst_id}/access_tokens",
            headers=headers,
            json={},
        )
        resp.raise_for_status()
        body = resp.json()
        token = body["token"]
        try:
            expires_at = datetime.fromisoformat(body["expires_at"])
        except (KeyError, ValueError):
            # Fallback: assume 1 hour (default GitHub installation token lifetime).
            expires_at = datetime.now(timezone.utc).replace(microsecond=0) + timedelta(
                hours=_DEFAULT_TOKEN_LIFETIME_HOURS
            )
        return token, expires_at


async def _acquire_github_app_token() -> tuple[str, datetime]:
    """Acquire a new GitHub App installation token (no endpoint validation).

    Use this for background token refresh where we only need a fresh token
    from GitHub — endpoint validation is skipped so that a temporarily
    unreachable MCP server does not block token renewal.
    """
    app_id = cfg("GITHUB_APP_ID")
    key_name = cfg("GITHUB_APP_KEY_NAME")
    vault_url = cfg("GITHUB_APP_KEYVAULT_URL")
    owner = cfg("GITHUB_APP_INSTALLATION_OWNER", _DEFAULT_INSTALLATION_OWNER)
    if not app_id or not key_name or not vault_url:
        raise RuntimeError(
            "Missing GitHub App config. Need GITHUB_APP_ID, "
            "GITHUB_APP_KEY_NAME, and GITHUB_APP_KEYVAULT_URL."
        )

    jwt = await _create_app_jwt(vault_url, key_name, app_id)
    return await _get_installation_token(jwt, owner)


async def _get_github_app_token() -> tuple[str, datetime]:
    """Get a GitHub MCP token using GitHub App + Key Vault config.

    Acquires a fresh installation token.  Endpoint validation is left
    to the caller (``create_github_mcp_tool``).
    """
    return await _acquire_github_app_token()


async def _get_github_token() -> tuple[str, datetime | None]:
    """Get GitHub MCP token.

    Returns:
        (token, expires_at)
        - static `GITHUB_TOKEN` mode => `expires_at` is None
        - GitHub App mode => `expires_at` is token expiry UTC time
    """
    static_token = (os.environ.get(_GITHUB_TOKEN_ENV) or "").strip()
    if static_token:
        return static_token, None

    token, expires_at = await _get_github_app_token()
    return token, expires_at


# -- public ----------------------------------------------------------------


async def create_github_mcp_tool() -> MCPStreamableHTTPTool:
    """Create a local MCP tool for GitHub with auto-refreshing auth.

    The tool connects directly from the agent container to GitHub's
    remote MCP server (``api.githubcopilot.com``).  A custom httpx client
    with an event hook injects fresh auth and toolset headers on every
    outbound HTTP request (including the MCP handshake), so there is no
    reliance on Foundry-side proxy state.

    Supports two authentication modes (checked in order):

    1. **Environment token** — ``GITHUB_TOKEN`` env var (e.g. a PAT).
    2. **GitHub App JWT via Key Vault** — mints short-lived installation
       tokens with proactive background refresh.

    Config keys (from App Configuration / ``.env``):

    * ``GITHUB_APP_ID``
    * ``GITHUB_APP_KEY_NAME``
    * ``GITHUB_APP_KEYVAULT_URL``
    * ``GITHUB_APP_INSTALLATION_OWNER`` (default ``Azure``)
    """
    token, expires_at = await _get_github_token()
    if not token:
        raise RuntimeError("Failed to obtain GitHub token for MCP auth.")

    # Validate the endpoint once at startup.
    if not await _validate_mcp_endpoint(token):
        raise RuntimeError("GitHub MCP endpoint is unavailable (health check failed).")

    token_mgr = _GitHubTokenManager(token, expires_at)

    # Build a custom httpx client with an event hook that injects auth +
    # toolset headers on *every* outbound request.  This is necessary
    # because ``header_provider`` only fires during ``call_tool()`` — the
    # initial MCP handshake (``initialize``, ``tools/list``) does NOT go
    # through ``call_tool`` and would otherwise lack the Authorization
    # header, causing a 401.
    async def _inject_auth(request: httpx.Request) -> None:  # noqa: RUF029
        for key, value in token_mgr.get_headers().items():
            request.headers[key] = value

    http_client = httpx.AsyncClient(
        follow_redirects=True,
        timeout=httpx.Timeout(
            _MCP_REQUEST_TIMEOUT_SECS, read=_MCP_REQUEST_TIMEOUT_SECS
        ),
        event_hooks={"request": [_inject_auth]},
    )

    mcp_tool = MCPStreamableHTTPTool(
        name="github",
        url=_GITHUB_MCP_URL,
        description=(
            "The GitHub MCP Server has the ability to read repositories "
            "and code files, manage issues and PRs, analyze code, and "
            "automate workflows."
        ),
        approval_mode="never_require",
        allowed_tools=_GITHUB_ALLOWED_TOOLS,
        load_prompts=False,
        request_timeout=_MCP_REQUEST_TIMEOUT_SECS,
        http_client=http_client,
        parse_tool_results=truncating_mcp_parser,
    )

    if token_mgr.is_static:
        logger.info("GitHub MCP tool configured via GITHUB_TOKEN env (static)")
    else:
        logger.info(
            "GitHub MCP tool configured via GitHub App token (owner=%s, expires=%s)",
            cfg("GITHUB_APP_INSTALLATION_OWNER", _DEFAULT_INSTALLATION_OWNER),
            expires_at.isoformat() if expires_at else "unknown",
        )
        token_mgr.start_background_refresh()

    return mcp_tool
