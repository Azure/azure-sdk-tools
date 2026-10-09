"""Lookup table from KB chunk-source folder → GitHub issue target.

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
from typing import Optional
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
class KbTarget:
    """GitHub issue target for a knowledge-base folder."""

    owner: str
    repo: str
    branch: str
    path: str  # path inside the repo this folder covers
    scope: str  # human-friendly scope label (folder name)
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


def _build_targets(config: dict) -> dict[str, tuple[KbTarget, ...]]:
    targets: dict[str, list[KbTarget]] = {}
    sources = config.get("sources") or []
    for src in sources:
        repo_block = src.get("repository") or {}
        url = repo_block.get("url") or ""
        branch = repo_block.get("branch") or "main"
        owner_repo = _parse_github_url(url)
        for path_entry in src.get("paths") or []:
            folder = path_entry.get("folder")
            if not folder:
                continue
            path = path_entry.get("path") or ""
            if owner_repo is None:
                targets.setdefault(folder, [])
            else:
                owner, repo = owner_repo
                targets.setdefault(folder, []).append(
                    KbTarget(
                        owner=owner,
                        repo=repo,
                        branch=branch,
                        path=path,
                        scope=folder,
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
            # No cache to fall back to → return empty dict so callers degrade
            # gracefully (resolve_kb_target → None → caller falls back to
            # the default KB repo).
            return {}


async def get_kb_targets(folder: str) -> tuple[KbTarget, ...]:
    """Return every GitHub source path registered for a KB folder."""
    cache = await _get_cache()
    return cache.get(folder, ())


def select_kb_target(
    folder: str,
    blob_path: str | None,
    targets: tuple[KbTarget, ...],
) -> Optional[KbTarget]:
    """Select the source path that contains an exact KB blob.

    Knowledge-sync blob names preserve the configured repository path with
    ``#`` separators, for example ``folder/doc#guide.md`` for ``/doc``.
    Prefer the longest matching path when configured roots are nested.
    """
    if not targets:
        return None
    if blob_path is None:
        return targets[0] if len(targets) == 1 else None

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
) -> Optional[KbTarget]:
    """Return the GitHub target containing ``blob_path``, or ``None``.

    Returns ``None`` when:
      - The folder is unknown.
      - The folder's repository is not a GitHub HTTPS URL (ADO, SSH, etc.).
      - Multiple paths share the folder and ``blob_path`` is omitted.
      - The blob does not belong to a configured path.
    """
    targets = await get_kb_targets(folder)
    return select_kb_target(folder, blob_path, targets)
