"""GitHub tools for the Azure SDK QA Bot Agent.

Provides a server-side MCP tool that connects to GitHub's remote MCP
server via the Foundry Agents API.  Authentication mirrors the Go
backend: a GitHub App JWT is signed via Azure Key Vault and exchanged
for a short-lived installation token, with automatic refresh before
expiry (same 5-minute buffer as the Go code).
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
from azure.keyvault.keys.aio import KeyClient
from azure.keyvault.keys.crypto.aio import CryptographyClient
from azure.keyvault.keys.crypto import SignatureAlgorithm
from agent_framework_azure_ai import AzureAIClient

from config.app_config import get as cfg
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_GITHUB_API = "https://api.github.com"
_GITHUB_MCP_URL = "https://api.githubcopilot.com/mcp/"
_DEFAULT_INSTALLATION_OWNER = "Azure"
# Refresh the token 5 minutes before it expires (matches Go backend).
_TOKEN_REFRESH_BUFFER_SECS = 5 * 60
# GitHub MCP server headers for toolset configuration.
# See https://github.com/github/github-mcp-server?tab=readme-ov-file#tool-configuration
_GITHUB_MCP_HEADERS = {
    "X-MCP-Toolsets": "repos,issues,actions,pull_requests",
    "X-MCP-Readonly": "true",
}


# -- helpers ---------------------------------------------------------------

def _b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


async def _sign_with_keyvault(vault_url: str, key_name: str, digest: bytes) -> bytes:
    """Sign *digest* with the RSA key in Key Vault using RS256."""
    credential = get_credential()
    key_client = KeyClient(vault_url=vault_url, credential=credential)
    try:
        key = await key_client.get_key(key_name)
        crypto = CryptographyClient(key.id, credential=credential)
        try:
            result = await crypto.sign(SignatureAlgorithm.rs256, digest)
            return result.signature
        finally:
            await crypto.close()
    finally:
        await key_client.close()


async def _create_app_jwt(vault_url: str, key_name: str, app_id: str) -> str:
    """Create a GitHub App JWT signed via Azure Key Vault (RS256)."""
    header = _b64url(json.dumps({"alg": "RS256", "typ": "JWT"}).encode())
    now = int(_time.time())
    payload = _b64url(json.dumps({"iat": now - 10, "exp": now + 600, "iss": app_id}).encode())
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
    async with httpx.AsyncClient() as client:
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
            expires_at = datetime.now(timezone.utc).replace(microsecond=0) + timedelta(hours=1)
        return token, expires_at


# -- auto-refreshing wrapper -----------------------------------------------

class _AutoRefreshGitHubMcpTool:
    """Wraps an ``McpTool`` and transparently refreshes the GitHub App
    installation token before it expires.

    The Foundry agent framework accesses ``definitions`` and ``resources``
    on each API request, so refreshing in those property accessors ensures
    the token is always valid when serialized to the API.
    """

    def __init__(
        self,
        inner,  # McpTool from azure.ai.agents.models
        *,
        vault_url: str,
        key_name: str,
        app_id: str,
        owner: str,
        token_expires_at: datetime,
    ) -> None:
        self._inner = inner
        self._vault_url = vault_url
        self._key_name = key_name
        self._app_id = app_id
        self._owner = owner
        self._token_expires_at = token_expires_at
        self._lock = asyncio.Lock()

    # -- token refresh logic (mirrors Go backend's getToken) ----------------

    def _needs_refresh(self) -> bool:
        buffer = timedelta(seconds=_TOKEN_REFRESH_BUFFER_SECS)
        return datetime.now(timezone.utc) >= self._token_expires_at - buffer

    async def _refresh_token(self) -> None:
        async with self._lock:
            if not self._needs_refresh():
                return  # Another coroutine already refreshed.
            logger.info("Refreshing GitHub App installation token...")
            jwt = await _create_app_jwt(self._vault_url, self._key_name, self._app_id)
            token, expires_at = await _get_installation_token(jwt, self._owner)
            self._inner.authorization = token
            self._token_expires_at = expires_at
            logger.info("GitHub token refreshed, expires at %s", expires_at.isoformat())

    async def ensure_valid_token(self) -> None:
        """Refresh the token if it is about to expire."""
        if self._needs_refresh():
            await self._refresh_token()

    # -- delegate everything the agent framework touches --------------------

    @property
    def definitions(self):
        # Best-effort synchronous refresh: schedule in the running loop.
        # `to_azure_ai_agent_tools` is called from an async context, so the
        # loop should be running.
        if self._needs_refresh():
            loop = asyncio.get_event_loop()
            if loop.is_running():
                loop.create_task(self._refresh_token())
        return self._inner.definitions

    @property
    def resources(self):
        return self._inner.resources

    # Pass through any other attribute to the inner McpTool.
    def __getattr__(self, name):
        return getattr(self._inner, name)


# -- public ----------------------------------------------------------------

async def create_github_mcp_tool(client: AzureAIClient):
    """Create a server-side MCP tool for GitHub with auto-refreshing auth.

    Supports two authentication modes (checked in order), both
    server-side so end-users are never prompted to sign in:

    1. **Environment token** — uses a ``GITHUB_TOKEN`` environment
       variable (e.g. a PAT) for shared, key-based authentication.

    2. **GitHub App JWT via Key Vault** — uses a GitHub App private key
       stored in Azure Key Vault to mint installation tokens, with
       automatic refresh before expiry.

    Args:
        client: The agent client (``AzureAIClient``).

    Config keys (from App Configuration / ``.env``):

    * ``GITHUB_APP_ID``
    * ``GITHUB_APP_KEY_NAME``
    * ``GITHUB_APP_KEYVAULT_URL``
    * ``GITHUB_APP_INSTALLATION_OWNER`` (default ``Azure``)
    """
    # --- Token from environment variable -----------------------------------
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        mcp_tool = client.get_mcp_tool(
            name="github",
            url=_GITHUB_MCP_URL,
            authorization=token,
            approval_mode="never_require",
            headers=_GITHUB_MCP_HEADERS,
        )
        logger.info(
            "GitHub MCP tool configured with token from environment variable",
        )
        return mcp_tool

    # --- Production: GitHub App JWT via Key Vault --------------------------
    app_id = cfg("GITHUB_APP_ID")
    key_name = cfg("GITHUB_APP_KEY_NAME")
    vault_url = cfg("GITHUB_APP_KEYVAULT_URL")
    owner = _DEFAULT_INSTALLATION_OWNER

    if app_id and key_name and vault_url:
        jwt = await _create_app_jwt(vault_url, key_name, app_id)
        token, expires_at = await _get_installation_token(jwt, owner)
        mcp_tool = client.get_mcp_tool(
            name="github",
            description="The GitHub MCP Server has the ability to read repositories and code files, manage issues and PRs, analyze code, and automate workflows.",
            url=_GITHUB_MCP_URL,
            authorization=token,
            approval_mode="never_require",
            headers=_GITHUB_MCP_HEADERS,
        )
        logger.info(
            "GitHub MCP tool configured with App installation token "
            "(owner=%s, expires=%s)", owner, expires_at.isoformat(),
        )
        return _AutoRefreshGitHubMcpTool(
            mcp_tool,
            vault_url=vault_url,
            key_name=key_name,
            app_id=app_id,
            owner=owner,
            token_expires_at=expires_at,
        )

    # --- Fallback: no auth -------------------------------------------------
    logger.warning(
        "GitHub App config incomplete (need GITHUB_APP_ID, GITHUB_APP_KEY_NAME, "
        "GITHUB_APP_KEYVAULT_URL); GitHub MCP will have limited access"
    )
    return client.get_mcp_tool(
        name="github",
        url=_GITHUB_MCP_URL,
        approval_mode="never_require",
        headers=_GITHUB_MCP_HEADERS,
    )
