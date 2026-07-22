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


_cache: dict[str, Optional[KbTarget]] | None = None
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


def _build_targets(config: dict) -> dict[str, Optional[KbTarget]]:
    targets: dict[str, Optional[KbTarget]] = {}
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
                # Non-GitHub source (ADO, SSH, wiki): mark un-mappable.
                targets[folder] = None
            else:
                owner, repo = owner_repo
                targets[folder] = KbTarget(
                    owner=owner,
                    repo=repo,
                    branch=branch,
                    path=path,
                    scope=folder,
                )
    return targets


async def _fetch_config() -> dict:
    async with httpx.AsyncClient(timeout=_FETCH_TIMEOUT_SECS) as client:
        resp = await client.get(_CONFIG_URL)
        resp.raise_for_status()
        return json.loads(resp.text)


async def _refresh_cache() -> dict[str, Optional[KbTarget]]:
    global _cache, _cache_ts
    config = await _fetch_config()
    _cache = _build_targets(config)
    _cache_ts = time.time()
    logger.info("Refreshed knowledge-config cache (%d folders)", len(_cache))
    return _cache


async def _get_cache() -> dict[str, Optional[KbTarget]]:
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


async def get_kb_target(folder: str) -> Optional[KbTarget]:
    """Return the GitHub issue target for a KB folder, or ``None``.

    Returns ``None`` when:
      - The folder is unknown.
      - The folder's repository is not a GitHub HTTPS URL (ADO, SSH, etc.).
    """
    cache = await _get_cache()
    return cache.get(folder)
