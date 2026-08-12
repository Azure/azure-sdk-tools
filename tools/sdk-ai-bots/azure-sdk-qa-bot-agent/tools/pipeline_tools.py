"""Azure Pipelines failure-log tool (direct ADO REST, no MCP server).

Replaces the previous azsdk-cli MCP stdio approach.

Why a Key Vault hop instead of calling ADO with the agent identity:
Azure DevOps does not accept Foundry agent identities (Entra service
principals) as organization members, so the hosted agent cannot mint
an ADO token directly. An out-of-band job (a UAMI that IS an org
member) refreshes a usable AAD bearer token into Key Vault, and this
tool reads it from there at request time.

Credential source (refreshed JIT before expiry, mirroring the GitHub
MCP token-manager pattern):
  Key Vault secret named ``ado-token`` in the vault configured via
  ``KEYVAULT_ENDPOINT`` (App Configuration). The secret value must be
  an AAD bearer token (JWT) for the ADO resource
  ``499b84ac-1321-427f-aa17-267ca6975798``; PATs are not supported.
"""

from __future__ import annotations

import logging
from typing import Annotated
from urllib.parse import parse_qs, urlparse

import httpx
from pydantic import BaseModel, Field

from tools import tool
from utils.ado_token import resolve_token

logger = logging.getLogger(__name__)

_ADO_BASE_URL = "https://dev.azure.com/azure-sdk"
_PUBLIC_PROJECT = "public"
_INTERNAL_PROJECT = "internal"
_API_VERSION = "7.1"

_HTTP_TIMEOUT_SECONDS = 30.0
_MAX_LOG_TAIL_CHARS = 4000
_MAX_TOTAL_LOG_CHARS = 32000


class FailedTask(BaseModel):
    """A single failed pipeline task with the tail of its log."""

    name: str
    log_id: int
    log_excerpt: str


class PipelineAnalysisResult(BaseModel):
    """Result for ``azsdk_analyze_pipeline``."""

    success: bool
    build_id: int
    project: str
    pipeline_url: str = ""
    status: str = ""
    result: str = ""
    failed_tasks: list[FailedTask] = Field(default_factory=list)
    error: str | None = None
    notes: str = ""


def _build_auth_header(token: str) -> dict[str, str]:
    """Build the Authorization header for an AAD bearer token."""
    return {"Authorization": f"Bearer {token}"}


def _parse_build_identifier(identifier: str) -> tuple[int, str | None]:
    """Extract ``(build_id, project_hint)`` from a raw id or ADO URL.

    Accepts either a bare integer (e.g. ``"5094469"``) or a results URL
    (e.g. ``https://dev.azure.com/azure-sdk/internal/_build/results?buildId=5094469``).
    """
    s = (identifier or "").strip()
    if not s:
        raise ValueError("Empty pipeline identifier.")
    if s.isdigit():
        return int(s), None

    parsed = urlparse(s)
    if not parsed.scheme or not parsed.netloc:
        raise ValueError(f"Invalid pipeline identifier: {identifier}")

    segments = [seg for seg in parsed.path.split("/") if seg]
    # /<org>/<project>/_build/...
    project = segments[1] if len(segments) >= 2 else None

    qs = parse_qs(parsed.query)
    build_id_str = (qs.get("buildId") or [None])[0]
    if not build_id_str or not build_id_str.isdigit():
        raise ValueError(f"Could not extract buildId from: {identifier}")
    return int(build_id_str), project


def _is_test_step(name: str | None) -> bool:
    """Match the C# heuristic: 'test' in name, except 'deploy test resources'."""
    if not name:
        return False
    lower = name.lower()
    if "deploy test resources" in lower:
        return False
    return "test" in lower


async def _get_json(client: httpx.AsyncClient, url: str) -> dict | None:
    response = await client.get(url)
    # ADO returns 203 with a sign-in HTML page when the request is unauthorized.
    if response.status_code == 203:
        raise PermissionError(f"Not authorized: {url}")
    if response.status_code == 404:
        return None
    response.raise_for_status()
    return response.json()


async def _resolve_project(
    client: httpx.AsyncClient, build_id: int, hint: str | None
) -> str:
    """Locate which ADO project a build belongs to."""
    candidates = [hint] if hint else [_PUBLIC_PROJECT, _INTERNAL_PROJECT]
    last_error: Exception | None = None
    for proj in candidates:
        url = (
            f"{_ADO_BASE_URL}/{proj}/_apis/build/builds/{build_id}"
            f"?api-version={_API_VERSION}"
        )
        try:
            data = await _get_json(client, url)
        except (PermissionError, httpx.HTTPStatusError) as e:
            last_error = e
            continue
        if data:
            return data.get("project", {}).get("name") or proj
    raise RuntimeError(
        f"Could not locate build {build_id} in projects {candidates}"
        + (f" (last error: {last_error})" if last_error else "")
    )


async def _get_failed_log_ids(
    client: httpx.AsyncClient, project: str, build_id: int
) -> list[tuple[str, int]]:
    """Return ``(task_name, log_id)`` for each failed non-test Task."""
    url = (
        f"{_ADO_BASE_URL}/{project}/_apis/build/builds/{build_id}/timeline"
        f"?api-version={_API_VERSION}"
    )
    data = await _get_json(client, url)
    if not data:
        return []

    seen: set[int] = set()
    out: list[tuple[str, int]] = []
    for rec in data.get("records") or []:
        if rec.get("result") != "failed" or rec.get("type") != "Task":
            continue
        name = rec.get("name")
        if _is_test_step(name):
            continue
        log = rec.get("log") or {}
        log_id = log.get("id")
        if not isinstance(log_id, int) or log_id <= 0 or log_id in seen:
            continue
        seen.add(log_id)
        out.append((name or f"task-{log_id}", log_id))
    return out


async def _get_log_tail(
    client: httpx.AsyncClient, project: str, build_id: int, log_id: int
) -> str:
    """Fetch a task log and return its tail (errors are usually at the end)."""
    url = (
        f"{_ADO_BASE_URL}/{project}/_apis/build/builds/{build_id}/logs/{log_id}"
        f"?api-version={_API_VERSION}"
    )
    response = await client.get(url)
    if response.status_code == 203:
        raise PermissionError(f"Not authorized: {url}")
    response.raise_for_status()
    text = response.text or ""
    if len(text) > _MAX_LOG_TAIL_CHARS:
        return "... [head truncated]\n" + text[-_MAX_LOG_TAIL_CHARS:]
    return text


class PipelineTools:
    """Tools for analyzing Azure DevOps pipeline runs in the azure-sdk org."""

    @tool
    async def azsdk_analyze_pipeline(
        self,
        *,
        pipeline: Annotated[
            str,
            (
                "Azure Pipelines build link "
                "(e.g. https://dev.azure.com/azure-sdk/internal/_build/results?buildId=5094469) "
                "or just the numeric build ID."
            ),
        ],
        project: Annotated[
            str,
            (
                "Optional ADO project name override ('public' or 'internal'). "
                "If empty, auto-detected from the link or by probing both projects."
            ),
        ] = "",
    ) -> PipelineAnalysisResult:
        """Fetch failure logs for an Azure SDK pipeline run.

        Returns the build status plus the tail of each failed
        non-test task's log so the agent can diagnose the failure.
        """
        try:
            build_id, project_from_link = _parse_build_identifier(pipeline)
        except ValueError as e:
            return PipelineAnalysisResult(
                success=False, build_id=0, project="", error=str(e)
            )

        hint = (project or project_from_link or "").strip() or None
        try:
            token = await resolve_token()
        except Exception as e:
            return PipelineAnalysisResult(
                success=False, build_id=build_id, project=hint or "", error=str(e)
            )
        auth = _build_auth_header(token)

        async with httpx.AsyncClient(
            timeout=httpx.Timeout(_HTTP_TIMEOUT_SECONDS),
            headers={"Accept": "application/json", **auth},
            follow_redirects=False,
        ) as client:
            try:
                resolved_project = await _resolve_project(client, build_id, hint)
            except (PermissionError, RuntimeError) as e:
                return PipelineAnalysisResult(
                    success=False,
                    build_id=build_id,
                    project=hint or "",
                    error=str(e),
                )
            except httpx.HTTPError as e:
                return PipelineAnalysisResult(
                    success=False,
                    build_id=build_id,
                    project=hint or "",
                    error=f"HTTP error resolving project: {e}",
                )

            pipeline_url = (
                f"{_ADO_BASE_URL}/{resolved_project}"
                f"/_build/results?buildId={build_id}&view=results"
            )

            status, result = "", ""
            try:
                build = await _get_json(
                    client,
                    f"{_ADO_BASE_URL}/{resolved_project}/_apis/build/builds/"
                    f"{build_id}?api-version={_API_VERSION}",
                )
                if build:
                    status = build.get("status") or ""
                    result = build.get("result") or ""
            except Exception:
                logger.debug("build summary fetch failed", exc_info=True)

            try:
                failed = await _get_failed_log_ids(client, resolved_project, build_id)
            except PermissionError as e:
                return PipelineAnalysisResult(
                    success=False,
                    build_id=build_id,
                    project=resolved_project,
                    pipeline_url=pipeline_url,
                    status=status,
                    result=result,
                    error=str(e),
                )
            except httpx.HTTPError as e:
                return PipelineAnalysisResult(
                    success=False,
                    build_id=build_id,
                    project=resolved_project,
                    pipeline_url=pipeline_url,
                    status=status,
                    result=result,
                    error=f"HTTP error fetching timeline: {e}",
                )

            failed_tasks: list[FailedTask] = []
            total_chars = 0
            truncated = False
            for name, log_id in failed:
                remaining = _MAX_TOTAL_LOG_CHARS - total_chars
                if remaining <= 0:
                    truncated = True
                    break
                try:
                    excerpt = await _get_log_tail(
                        client, resolved_project, build_id, log_id
                    )
                except Exception as e:
                    excerpt = f"[failed to fetch log: {e}]"
                if len(excerpt) > remaining:
                    excerpt = excerpt[-remaining:]
                    truncated = True
                total_chars += len(excerpt)
                failed_tasks.append(
                    FailedTask(name=name, log_id=log_id, log_excerpt=excerpt)
                )

            notes = ""
            if truncated:
                notes = (
                    f"Output truncated to ~{_MAX_TOTAL_LOG_CHARS} chars across "
                    f"{len(failed_tasks)} failed task log(s)."
                )

            return PipelineAnalysisResult(
                success=True,
                build_id=build_id,
                project=resolved_project,
                pipeline_url=pipeline_url,
                status=status,
                result=result,
                failed_tasks=failed_tasks,
                notes=notes,
            )
