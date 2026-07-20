"""GitHub **MCP** tool for the Self-Evolving Agent.

Provides a local MCP tool (:class:`MCPStreamableHTTPTool`) that connects to
GitHub's remote MCP server (``api.githubcopilot.com/mcp/``) directly from the
agent container. This is the read/analysis half of the remote workflow — it
replaces the hand-rolled REST read helpers in ``github_tools.py`` with the
official GitHub MCP toolset (repo browsing, code/PR/issue search, and Actions
run/log inspection for benchmark analysis).

Modeled on ``tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools/github_mcp_tools.py``
but deliberately simplified:

* **Auth is a PAT read from a ``.env`` file** (``GITHUB_TOKEN`` / ``GH_TOKEN``).
  No Key Vault / GitHub App / JWT machinery — the self-evolving agent runs in a
  personal Foundry project with a single injected token.
* **The MCP tool is read-only** (``X-MCP-Readonly: true``). Writes (opening the
  *draft* PR, dispatching the ``skill-eval`` CI) stay in ``github_tools.py`` as
  custom Python tools so the hard "draft PRs only" guarantee cannot be bypassed
  by a model choosing ``create_pull_request`` with ``draft=false``.

Using a local MCP client (rather than server-side delegation) means the agent
framework traces every MCP request/response in the container.
"""

from __future__ import annotations

import logging
import os
from pathlib import Path

import httpx

logger = logging.getLogger(__name__)

_GITHUB_MCP_URL = "https://api.githubcopilot.com/mcp/"
_GITHUB_TOKEN_ENVS = ("GITHUB_TOKEN", "GH_TOKEN")
# Look for a .env next to the agent package (agent/.env) and one level up.
_ENV_CANDIDATES = (
    Path(__file__).resolve().parent.parent / ".env",
    Path(__file__).resolve().parent.parent.parent / ".env",
)
# GitHub MCP server toolset + read-only configuration.
# https://github.com/github/github-mcp-server?tab=readme-ov-file#tool-configuration
_GITHUB_MCP_HEADERS = {
    "X-MCP-Toolsets": "repos,issues,actions,pull_requests",
    "X-MCP-Readonly": "true",
}
# Restrict what the model may invoke (defence in depth on top of the headers).
# https://github.com/github/github-mcp-server for tool names.
_GITHUB_ALLOWED_TOOLS = [
    # repos (read-only)
    "get_file_contents",
    "search_repositories",
    "search_code",
    "list_branches",
    "get_commit",
    "list_commits",
    # issues (read-only)
    "issue_read",
    "list_issues",
    "search_issues",
    # pull_requests (read-only)
    "pull_request_read",
    "list_pull_requests",
    "search_pull_requests",
    # actions (read-only) — used to read skill-eval benchmark runs + logs
    "actions_list",
    "actions_get",
    "actions_get_job_logs",
]
_MCP_REQUEST_TIMEOUT_SECS = 60
_MCP_VALIDATION_TIMEOUT_SECS = 10.0
_MCP_MAX_OUTPUT_CHARS = 8000


def _load_env_files() -> None:
    """Load a ``.env`` file (best-effort) so a local PAT is picked up.

    ``python-dotenv`` is already a dependency; if it is somehow missing we
    fall back to a tiny manual parser so the tool still works.
    """
    try:
        from dotenv import load_dotenv

        for candidate in _ENV_CANDIDATES:
            if candidate.is_file():
                load_dotenv(candidate, override=False)
        return
    except Exception:
        pass
    for candidate in _ENV_CANDIDATES:
        if not candidate.is_file():
            continue
        for line in candidate.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, _, value = line.partition("=")
            os.environ.setdefault(key.strip(), value.strip().strip("'\""))


def _read_token() -> str:
    """Read a GitHub PAT from the environment / ``.env`` file."""
    _load_env_files()
    for name in _GITHUB_TOKEN_ENVS:
        value = (os.environ.get(name) or "").strip()
        if value:
            return value
    return ""


def _truncating_mcp_parser(result):
    """Parse MCP tool results and truncate oversized text content.

    Returns a ``str`` so the agent framework treats it as a single text
    result, keeping large GitHub payloads from inflating the context window.
    """
    from mcp import types as mcp_types

    parts: list[str] = []
    for item in result.content:
        if isinstance(item, mcp_types.TextContent):
            text = item.text or ""
            if len(text) > _MCP_MAX_OUTPUT_CHARS:
                logger.info(
                    "Truncating MCP text content from %d to %d chars",
                    len(text),
                    _MCP_MAX_OUTPUT_CHARS,
                )
                text = text[:_MCP_MAX_OUTPUT_CHARS] + "\n... [truncated]"
            parts.append(text)
    return "\n".join(parts) if parts else "null"


def _validate_mcp_endpoint(token: str) -> bool:
    """Best-effort check that the remote GitHub MCP endpoint accepts us.

    Sends the same ``initialize`` handshake the Foundry runtime uses so we
    catch auth (401/403) and protocol (415) failures early. Returns ``False``
    on any auth/transport failure — the caller then skips the MCP tool and
    falls back to the custom REST read tools.
    """
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/json, text/event-stream",
        "Content-Type": "application/json; charset=utf-8",
    }
    body = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {
            "protocolVersion": "2025-03-26",
            "capabilities": {},
            "clientInfo": {"name": "self-evolve-health-check", "version": "0.1.0"},
        },
    }
    try:
        with httpx.Client(timeout=_MCP_VALIDATION_TIMEOUT_SECS) as client:
            resp = client.post(_GITHUB_MCP_URL, headers=headers, json=body)
        if resp.status_code in (401, 403):
            logger.error("GitHub MCP auth failed (status=%s).", resp.status_code)
            return False
        if resp.status_code == 415:
            logger.error("GitHub MCP endpoint returned 415 — protocol incompatibility.")
            return False
        if resp.status_code >= 500:
            logger.error("GitHub MCP endpoint server error (status=%s).", resp.status_code)
            return False
        return True
    except Exception as ex:
        logger.warning("GitHub MCP endpoint health check failed: %s", ex)
        return False


def create_github_mcp_tool():
    """Create a read-only GitHub MCP tool authenticated with a PAT.

    Returns an ``MCPStreamableHTTPTool`` on success, or ``None`` when no token
    is configured or the endpoint is unreachable — so the caller can fall back
    to the custom REST read tools in ``github_tools.py``.

    The PAT is read from ``GITHUB_TOKEN`` / ``GH_TOKEN`` in the environment or a
    ``.env`` file next to the agent. The token needs only read scopes
    (``contents:read``, ``pull_requests:read``, ``actions:read``) because this
    tool is pinned read-only; the draft-PR write path stays in ``github_tools``.
    """
    token = _read_token()
    if not token:
        logger.warning(
            "No GitHub PAT found (checked %s and .env). GitHub MCP tool disabled; "
            "falling back to custom REST read tools.",
            ", ".join(_GITHUB_TOKEN_ENVS),
        )
        return None

    if not _validate_mcp_endpoint(token):
        logger.warning("GitHub MCP endpoint unavailable — falling back to REST read tools.")
        return None

    from agent_framework import MCPStreamableHTTPTool

    # Inject auth + toolset headers on *every* outbound request. The initial
    # MCP handshake (initialize / tools/list) does not pass through call_tool,
    # so a header_provider alone would leave it unauthenticated (401).
    async def _inject_auth(request: httpx.Request) -> None:  # noqa: RUF029
        request.headers["Authorization"] = f"Bearer {token}"
        for key, value in _GITHUB_MCP_HEADERS.items():
            request.headers[key] = value

    http_client = httpx.AsyncClient(
        follow_redirects=True,
        timeout=httpx.Timeout(_MCP_REQUEST_TIMEOUT_SECS, read=_MCP_REQUEST_TIMEOUT_SECS),
        event_hooks={"request": [_inject_auth]},
    )

    tool = MCPStreamableHTTPTool(
        name="github",
        url=_GITHUB_MCP_URL,
        description=(
            "Read-only GitHub MCP server tools. Browse repository files and "
            "directories, search code/PRs/issues, and inspect GitHub Actions "
            "runs and job logs (used to read skill-eval benchmark results). "
            "Cannot write — opening the draft PR and dispatching CI use the "
            "dedicated open_skill_pull_request / dispatch_workflow tools."
        ),
        approval_mode="never_require",
        allowed_tools=_GITHUB_ALLOWED_TOOLS,
        load_prompts=False,
        request_timeout=_MCP_REQUEST_TIMEOUT_SECS,
        http_client=http_client,
        parse_tool_results=_truncating_mcp_parser,
    )
    logger.info("GitHub MCP tool configured via PAT (read-only, toolsets=%s).",
                _GITHUB_MCP_HEADERS["X-MCP-Toolsets"])
    return tool
