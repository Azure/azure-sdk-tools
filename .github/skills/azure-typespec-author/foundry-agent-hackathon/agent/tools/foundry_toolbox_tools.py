"""Foundry **Toolbox** (WorkIQ) MCP tool for the Self-Evolving Agent.

Wires a Microsoft Foundry *toolbox* into the agent as a local MCP tool
(:class:`MCPStreamableHTTPTool`). A toolbox (e.g. the WorkIQ ``read-internal-docs``
toolbox) exposes tools that can read **internal / SharePoint / OneDrive documents
online** on the agent's behalf — including the user-telemetry **Excel workbook** that
``read_prompt_excel`` cannot download directly because private SharePoint sharing
links are auth-gated.

With this tool the agent can be handed a **SharePoint Excel path/URL** and read it
online through the toolbox, instead of requiring the workbook to be downloaded and
passed as a local file.

Auth mirrors the Foundry toolbox sample: an AAD bearer token minted from the ambient
identity (``DefaultAzureCredential`` — ``az login`` locally or the container's managed
identity in Foundry) for the ``https://ai.azure.com/.default`` scope, plus the
``Foundry-Features: Toolboxes=V1Preview`` preview header. A fresh token is injected on
every request via an httpx event hook (matching ``github_mcp_tools.py``), so the initial
MCP ``initialize`` / ``tools/list`` handshake is authenticated too.

Configuration (environment variables / ``.env``):

* ``FOUNDRY_TOOLBOX_MCP_URL`` — full toolbox MCP URL. If set, used verbatim, e.g.
  ``https://{account}.services.ai.azure.com/api/projects/{project}/toolboxes/{name}/mcp?api-version=v1``
* Otherwise the URL is built from ``AI_FOUNDRY_PROJECT_ENDPOINT`` +
  ``FOUNDRY_TOOLBOX_NAME`` (default ``read-internal-docs``) and, when set,
  ``FOUNDRY_TOOLBOX_VERSION`` (adds a ``/versions/{n}`` segment).

Returns ``None`` when no project endpoint / toolbox is configured, so the caller can
simply skip the tool and fall back to ``read_prompt_excel`` (local path / public URL).
"""

from __future__ import annotations

import logging
import os
from pathlib import Path

import httpx

logger = logging.getLogger(__name__)

_FOUNDRY_TOKEN_SCOPE = "https://ai.azure.com/.default"
_FOUNDRY_FEATURES_HEADER = {"Foundry-Features": "Toolboxes=V1Preview"}
_DEFAULT_TOOLBOX_NAME = "read-internal-docs"
_TOOLBOX_API_VERSION = "v1"

# Look for a .env next to the agent package (agent/.env) and one level up.
_ENV_CANDIDATES = (
    Path(__file__).resolve().parent.parent / ".env",
    Path(__file__).resolve().parent.parent.parent / ".env",
)

_MCP_REQUEST_TIMEOUT_SECS = 120
_MCP_MAX_OUTPUT_CHARS = 12000


def _load_env_files() -> None:
    """Load a ``.env`` file (best-effort) so local config is picked up."""
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


def _resolve_toolbox_url() -> str:
    """Resolve the toolbox MCP URL from the environment / ``.env``.

    Returns an empty string when neither an explicit URL nor a project endpoint
    is configured.
    """
    explicit = (os.environ.get("FOUNDRY_TOOLBOX_MCP_URL") or "").strip()
    if explicit:
        return explicit

    endpoint = (os.environ.get("AI_FOUNDRY_PROJECT_ENDPOINT") or "").strip()
    if not endpoint:
        return ""

    name = (os.environ.get("FOUNDRY_TOOLBOX_NAME") or _DEFAULT_TOOLBOX_NAME).strip()
    version = (os.environ.get("FOUNDRY_TOOLBOX_VERSION") or "").strip()

    base = endpoint.rstrip("/")
    if version:
        return f"{base}/toolboxes/{name}/versions/{version}/mcp?api-version={_TOOLBOX_API_VERSION}"
    return f"{base}/toolboxes/{name}/mcp?api-version={_TOOLBOX_API_VERSION}"


def _truncating_mcp_parser(result):
    """Parse MCP tool results and truncate oversized *text* content.

    Keeps large document payloads from blowing up the context window while
    still returning a single text result to the agent framework. Binary file
    reads must NOT go through this parser (truncation corrupts the base64) —
    use :func:`fetch_sharepoint_file_bytes` for those.
    """
    from mcp import types as mcp_types

    parts: list[str] = []
    for item in result.content:
        if isinstance(item, mcp_types.TextContent):
            text = item.text or ""
            if len(text) > _MCP_MAX_OUTPUT_CHARS:
                logger.info(
                    "Truncating toolbox MCP text content from %d to %d chars",
                    len(text),
                    _MCP_MAX_OUTPUT_CHARS,
                )
                text = text[:_MCP_MAX_OUTPUT_CHARS] + "\n... [truncated]"
            parts.append(text)
    return "\n".join(parts) if parts else "null"


def _build_toolbox_http_client():
    """Build an authenticated httpx client for the toolbox MCP endpoint.

    Returns ``(http_client, url, toolbox_name)`` or ``None`` when the toolbox is
    not configured / credentials cannot be built. A fresh bearer token + the
    preview feature header are injected on *every* request (so the MCP handshake
    is authenticated too), matching ``github_mcp_tools.py``.
    """
    _load_env_files()

    url = _resolve_toolbox_url()
    if not url:
        logger.warning(
            "Foundry toolbox disabled: set FOUNDRY_TOOLBOX_MCP_URL or "
            "AI_FOUNDRY_PROJECT_ENDPOINT (+ FOUNDRY_TOOLBOX_NAME) to enable it."
        )
        return None

    try:
        from azure.identity import DefaultAzureCredential, get_bearer_token_provider
    except Exception as ex:  # pragma: no cover
        logger.warning("azure-identity unavailable; Foundry toolbox disabled: %s", ex)
        return None

    try:
        credential = DefaultAzureCredential()
        token_provider = get_bearer_token_provider(credential, _FOUNDRY_TOKEN_SCOPE)
    except Exception as ex:
        logger.warning("Could not build Foundry credential; toolbox disabled: %s", ex)
        return None

    # Inject a fresh bearer token + the preview feature header on *every* request.
    # The MCP handshake (initialize / tools/list) does not pass through call_tool,
    # so injecting here (rather than only per tool call) keeps it authenticated.
    async def _inject_auth(request: httpx.Request) -> None:  # noqa: RUF029
        try:
            token = token_provider()
        except Exception as ex:  # pragma: no cover
            logger.error("Failed to acquire Foundry toolbox token: %s", ex)
            raise
        request.headers["Authorization"] = f"Bearer {token}"
        for key, value in _FOUNDRY_FEATURES_HEADER.items():
            request.headers[key] = value

    http_client = httpx.AsyncClient(
        follow_redirects=True,
        timeout=httpx.Timeout(_MCP_REQUEST_TIMEOUT_SECS, read=_MCP_REQUEST_TIMEOUT_SECS),
        event_hooks={"request": [_inject_auth]},
    )
    toolbox_name = (os.environ.get("FOUNDRY_TOOLBOX_NAME") or _DEFAULT_TOOLBOX_NAME).strip()
    return http_client, url, toolbox_name


def create_foundry_toolbox_tool():
    """Create a Foundry toolbox (WorkIQ) MCP tool, or ``None`` when unconfigured.

    The toolbox lets the agent read internal / SharePoint / OneDrive documents
    (including the user-telemetry Excel) *online*, using an AAD token minted from
    the ambient identity (``DefaultAzureCredential``). Grant that identity access
    to the toolbox / the SharePoint content it reads.
    """
    built = _build_toolbox_http_client()
    if built is None:
        return None
    http_client, url, toolbox_name = built

    from agent_framework import MCPStreamableHTTPTool

    tool = MCPStreamableHTTPTool(
        name=f"foundry_toolbox_{toolbox_name.replace('-', '_')}",
        url=url,
        description=(
            "Foundry toolbox (WorkIQ) tools that read INTERNAL documents online — "
            "including private SharePoint / OneDrive files. For a SharePoint EXCEL "
            "of user telemetry, prefer read_prompt_excel (it parses the workbook and "
            "handles SharePoint via this toolbox); use these tools directly to browse "
            "SharePoint or read other internal (Word/text) documents."
        ),
        approval_mode="never_require",
        load_prompts=False,
        request_timeout=_MCP_REQUEST_TIMEOUT_SECS,
        http_client=http_client,
        parse_tool_results=_truncating_mcp_parser,
    )
    logger.info("Foundry toolbox MCP tool configured (url=%s).", url)
    return tool


def _full_mcp_parser(result) -> str:
    """Tool-level parser: concatenate all text from a raw MCP CallToolResult.

    Unlike :func:`_truncating_mcp_parser` this does NOT truncate, so binary
    (base64) file payloads are returned intact.
    """
    from mcp import types as mcp_types

    return "".join(
        item.text or ""
        for item in result.content
        if isinstance(item, mcp_types.TextContent)
    )


def _content_list_to_text(result) -> str:
    """Post-invoke parser: concatenate text from the returned Content list/str."""
    if isinstance(result, str):
        return result
    parts: list[str] = []
    for item in result if isinstance(result, list) else [result]:
        text = getattr(item, "text", None)
        if text:
            parts.append(text)
    return "".join(parts)


async def fetch_sharepoint_file_bytes(url: str) -> bytes:
    """Download a SharePoint/OneDrive file's raw bytes via the Foundry toolbox.

    Two-step WorkIQ SharePoint flow (no local auth / no personal PAT needed —
    the toolbox reads the document on the caller's behalf):

    1. ``getFileOrFolderMetadataByUrl(fileOrFolderUrl=url)`` → resolve the sharing
       link to the file's ``id`` and its drive (``parentReference.driveId``).
    2. ``readSmallBinaryFile(fileId, documentLibraryId=driveId)`` → base64 content,
       decoded here to raw bytes.

    Uses a **non-truncating** parser so binary payloads are returned intact.
    Raises ``RuntimeError`` when the toolbox is unavailable and propagates any
    toolbox / decode error to the caller.
    """
    import base64
    import json as _json

    built = _build_toolbox_http_client()
    if built is None:
        raise RuntimeError(
            "Foundry toolbox is not configured (set FOUNDRY_TOOLBOX_MCP_URL or "
            "AI_FOUNDRY_PROJECT_ENDPOINT + FOUNDRY_TOOLBOX_NAME)."
        )
    http_client, toolbox_url, _ = built

    from agent_framework import MCPStreamableHTTPTool

    tool = MCPStreamableHTTPTool(
        name="foundry_toolbox_download",
        url=toolbox_url,
        load_prompts=False,
        request_timeout=_MCP_REQUEST_TIMEOUT_SECS,
        http_client=http_client,
        parse_tool_results=_full_mcp_parser,
    )

    async def _call(fn_name: str, args: dict) -> str:
        fn = next((f for f in tool.functions if f.name == fn_name), None)
        if fn is None:
            raise RuntimeError(f"Toolbox does not expose '{fn_name}'.")
        result = fn.invoke(arguments=args)
        if hasattr(result, "__await__"):
            result = await result
        return _content_list_to_text(result)

    async with tool:
        meta_text = await _call(
            "WorkIQSharePoint___getFileOrFolderMetadataByUrl",
            {"fileOrFolderUrl": url},
        )
        # The toolbox may return the metadata JSON followed by extra text, so
        # decode just the first JSON object rather than the whole string.
        try:
            meta, _end = _json.JSONDecoder().raw_decode(meta_text.lstrip())
        except ValueError as exc:
            raise RuntimeError(
                f"Could not parse SharePoint metadata response: {meta_text[:200]}"
            ) from exc
        file_id = meta.get("id")
        drive_id = (meta.get("parentReference") or {}).get("driveId")
        if not file_id or not drive_id:
            raise RuntimeError(
                f"Could not resolve SharePoint file id/drive from metadata: {meta_text[:200]}"
            )
        b64 = await _call(
            "WorkIQSharePoint___readSmallBinaryFile",
            {"fileId": file_id, "documentLibraryId": drive_id},
        )
        return base64.b64decode(b64)
