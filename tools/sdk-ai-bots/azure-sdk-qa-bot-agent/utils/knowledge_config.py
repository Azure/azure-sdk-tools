"""Lookup table from KB chunk-source folder to source and issue ownership.

Source-of-truth is the upstream ``knowledge-config.json`` in the
``azure-sdk-qa-bot-knowledge-sync`` repo. We fetch it on first access,
parse it into a folder-keyed dict, and cache in-process with a TTL.

"""

from __future__ import annotations

import asyncio
import json
import logging
import re
import time
from dataclasses import dataclass
from typing import Literal
from urllib.parse import urlparse

import httpx

logger = logging.getLogger(__name__)

# Upstream config URL (raw GitHub).
_CONFIG_URL = (
    "https://raw.githubusercontent.com/Azure/azure-sdk-tools/main/"
    "tools/sdk-ai-bots/azure-sdk-qa-bot-knowledge-sync/config/"
    "knowledge-config.json"
)

# Refresh the cache after this many seconds (~1h).
_CACHE_TTL_SECS = 60 * 60
_FETCH_TIMEOUT_SECS = 15


@dataclass(frozen=True)
class KbIssueTarget:
    """Configured issue destination for a knowledge repository."""

    provider: Literal["github", "azure-devops"]
    owner: str | None = None
    repo: str | None = None
    organization: str | None = None
    project: str | None = None


@dataclass(frozen=True)
class KbTarget:
    """Authoritative repository path and optional issue destination."""

    source_url: str
    branch: str
    path: str  # path inside the repo this folder covers
    scope: str  # human-friendly scope label (folder name)
    owner: str | None = None
    repo: str | None = None
    organization: str | None = None
    project: str | None = None
    ado_repository: str | None = None
    issue_target: KbIssueTarget | None = None
    relative_by_repo_path: bool = False


_cache: dict[str, tuple[KbTarget, ...]] | None = None
_cache_ts: float = 0.0
_lock = asyncio.Lock()


def _parse_github_url(url: str) -> tuple[str, str] | None:
    """Return ``(owner, repo)`` for a GitHub HTTPS URL, else ``None``.

    Only github.com HTTPS URLs are mapped; SSH, ADO, and other hosts are
    treated as non-issue-fileable (return ``None``).
    """
    try:
        parsed = urlparse(url)
    except Exception:
        return None
    if parsed.scheme not in {"http", "https"}:
        return None
    host = (parsed.hostname or "").lower()
    if host != "github.com":
        return None
    # /owner/repo(.git)?
    m = re.match(r"^/([^/]+)/([^/]+?)(?:\.git)?/?$", parsed.path)
    if not m:
        return None
    return m.group(1), m.group(2)


def _parse_ado_url(url: str) -> tuple[str, str, str] | None:
    """Return ``(organization, project, repository)`` for an ADO Git URL."""
    parsed = urlparse(url)
    if parsed.scheme != "https" or (parsed.hostname or "").lower() != "dev.azure.com":
        return None
    segments = [segment for segment in parsed.path.split("/") if segment]
    if len(segments) != 4 or segments[2].lower() != "_git":
        return None
    return segments[0], segments[1], segments[3]


def _parse_issue_tracker(value: object) -> KbIssueTarget | None:
    if value is None:
        return None
    if not isinstance(value, dict):
        raise ValueError("issueTracker must be an object")
    provider = value.get("provider")
    if provider == "github":
        repository = value.get("repository")
        if not isinstance(repository, str):
            raise ValueError("GitHub issueTracker.repository must be a string")
        parts = repository.split("/", 1)
        if len(parts) != 2 or not all(parts):
            raise ValueError(
                "GitHub issueTracker.repository must use owner/repository format"
            )
        return KbIssueTarget(provider="github", owner=parts[0], repo=parts[1])
    if provider == "azure-devops":
        organization = value.get("organization")
        project = value.get("project")
        if not isinstance(organization, str) or not organization:
            raise ValueError(
                "Azure DevOps issueTracker.organization must be a non-empty string"
            )
        if not isinstance(project, str) or not project:
            raise ValueError(
                "Azure DevOps issueTracker.project must be a non-empty string"
            )
        return KbIssueTarget(
            provider="azure-devops",
            organization=organization,
            project=project,
        )
    raise ValueError(f"Unsupported issueTracker provider: {provider!r}")


def _build_targets(config: dict) -> dict[str, tuple[KbTarget, ...]]:
    targets: dict[str, list[KbTarget]] = {}
    sources = config.get("sources") or []
    for src in sources:
        repo_block = src.get("repository") or {}
        url = repo_block.get("url") or ""
        branch = repo_block.get("branch") or "main"
        owner_repo = _parse_github_url(url)
        ado_repo = _parse_ado_url(url)
        issue_target = _parse_issue_tracker(repo_block.get("issueTracker"))
        for path_entry in src.get("paths") or []:
            folder = path_entry.get("folder")
            if not folder:
                continue
            path = path_entry.get("path") or ""
            owner, repo = owner_repo or (None, None)
            organization, project, ado_repository = ado_repo or (None, None, None)
            targets.setdefault(folder, []).append(
                KbTarget(
                    source_url=url,
                    owner=owner,
                    repo=repo,
                    organization=organization,
                    project=project,
                    ado_repository=ado_repository,
                    branch=branch,
                    path=path,
                    scope=folder,
                    issue_target=issue_target,
                    relative_by_repo_path=bool(
                        path_entry.get("relativeByRepoPath")
                    ),
                )
            )
    return {folder: tuple(values) for folder, values in targets.items()}


async def _fetch_config() -> dict:
    async with httpx.AsyncClient(timeout=_FETCH_TIMEOUT_SECS) as client:
        resp = await client.get(_CONFIG_URL)
        resp.raise_for_status()
        return json.loads(resp.text)


async def _refresh_cache() -> dict[str, tuple[KbTarget, ...]]:
    global _cache, _cache_ts
    config = await _fetch_config()
    _cache = _build_targets(config)
    _cache_ts = time.time()
    logger.info("Refreshed knowledge-config cache (%d folders)", len(_cache))
    return _cache


async def _get_cache() -> dict[str, tuple[KbTarget, ...]]:
    """Return the cached folder→target dict, refreshing if stale."""
    global _cache
    if _cache is not None and (time.time() - _cache_ts) < _CACHE_TTL_SECS:
        return _cache
    async with _lock:
        if _cache is not None and (time.time() - _cache_ts) < _CACHE_TTL_SECS:
            return _cache
        try:
            return await _refresh_cache()
        except Exception:
            logger.exception("Failed to refresh knowledge-config; using stale cache")
            if _cache is not None:
                return _cache
            raise


async def get_kb_targets(folder: str) -> tuple[KbTarget, ...]:
    """Return every source path registered for a KB folder."""
    cache = await _get_cache()
    return cache.get(folder, ())


def select_kb_target(
    folder: str,
    blob_path: str | None,
    targets: tuple[KbTarget, ...],
) -> KbTarget | None:
    """Select the source that owns a KB blob or unambiguous folder.

    Knowledge-sync blob names preserve the configured repository path with
    ``#`` separators, for example ``folder/doc#guide.md`` for ``/doc``.
    Prefer the longest matching path when configured roots are nested.
    Without a blob path, multiple configured paths are resolvable only when
    they share the same source, branch, and issue target.
    """
    if not targets:
        return None
    if blob_path is None:
        if len(targets) == 1:
            return targets[0]
        ownership = {
            (target.source_url, target.branch, target.issue_target)
            for target in targets
        }
        return targets[0] if len(ownership) == 1 else None

    prefix = f"{folder}/"
    if not blob_path.startswith(prefix):
        return None
    if len(targets) == 1:
        return targets[0]

    relative_blob_path = blob_path[len(prefix) :]
    path_matches = [
        target
        for target in targets
        if target.relative_by_repo_path
        and _blob_path_matches_target(relative_blob_path, target.path)
    ]
    if path_matches:
        return max(path_matches, key=lambda target: len(target.path))

    relative_targets = [
        target for target in targets if not target.relative_by_repo_path
    ]
    return relative_targets[0] if len(relative_targets) == 1 else None


def _blob_path_matches_target(relative_blob_path: str, target_path: str) -> bool:
    normalized_target = target_path.strip("/").removeprefix("./").replace("/", "#")
    if not normalized_target:
        return True
    return (
        relative_blob_path == normalized_target
        or relative_blob_path.startswith(f"{normalized_target}#")
    )


async def get_kb_target(
    folder: str,
    blob_path: str | None = None,
) -> KbTarget | None:
    """Return the configured source target containing ``blob_path``, or ``None``.

    Returns ``None`` when:
      - The folder is unknown.
      - Multiple repositories share the folder and ``blob_path`` is omitted.
      - The blob does not belong to a configured path.
    """
    targets = await get_kb_targets(folder)
    return select_kb_target(folder, blob_path, targets)
