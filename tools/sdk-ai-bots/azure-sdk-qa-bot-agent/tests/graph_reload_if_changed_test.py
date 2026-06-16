"""Unit tests for ``KnowledgeGraphService.reload_if_changed``.

The daily manifest poll must only pay the (expensive) full parquet reload
when ``latest.json``'s ``build_id`` actually changed, and must never
raise — a transient blob read failure should keep the current snapshot
serving and be retried on the next tick.

We bypass ``__init__`` with ``object.__new__`` so the test exercises the
poll branch logic without any blob / AI Search / config dependencies.
"""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from utils.knowledge_graph import KnowledgeGraphService  # noqa: E402


def _make_service(
    *,
    enabled: bool = True,
    loaded_build_id: str | None = "build-1",
    loaded: bool = True,
) -> KnowledgeGraphService:
    """Construct a bare service with just the attributes the poll reads."""
    svc = object.__new__(KnowledgeGraphService)
    svc._enabled = enabled
    if loaded:
        svc._loaded_version = {
            "source": "blob",
            "manifest": (
                {"build_id": loaded_build_id} if loaded_build_id is not None else {}
            ),
        }
    else:
        svc._loaded_version = None
    return svc


@pytest.mark.asyncio
async def test_skips_reload_when_build_id_unchanged():
    svc = _make_service(loaded_build_id="build-1")
    svc._load_manifest = AsyncMock(return_value={"build_id": "build-1"})
    svc.reload = AsyncMock()

    result = await svc.reload_if_changed()

    assert result is None
    svc.reload.assert_not_awaited()


@pytest.mark.asyncio
async def test_reloads_when_build_id_changed():
    svc = _make_service(loaded_build_id="build-1")
    svc._load_manifest = AsyncMock(return_value={"build_id": "build-2"})
    svc.reload = AsyncMock(return_value={"loaded": True})

    result = await svc.reload_if_changed()

    assert result == {"loaded": True}
    svc.reload.assert_awaited_once()


@pytest.mark.asyncio
async def test_reloads_when_never_loaded():
    svc = _make_service(loaded=False)
    svc._load_manifest = AsyncMock(return_value={"build_id": "build-1"})
    svc.reload = AsyncMock(return_value={"loaded": True})

    result = await svc.reload_if_changed()

    assert result == {"loaded": True}
    svc.reload.assert_awaited_once()


@pytest.mark.asyncio
async def test_disabled_service_is_noop():
    svc = _make_service(enabled=False)
    svc._load_manifest = AsyncMock()
    svc.reload = AsyncMock()

    result = await svc.reload_if_changed()

    assert result is None
    svc._load_manifest.assert_not_awaited()
    svc.reload.assert_not_awaited()


@pytest.mark.asyncio
async def test_manifest_read_failure_keeps_current_snapshot():
    svc = _make_service(loaded_build_id="build-1")
    svc._load_manifest = AsyncMock(side_effect=RuntimeError("blob down"))
    svc.reload = AsyncMock()

    # Must not raise.
    result = await svc.reload_if_changed()

    assert result is None
    svc.reload.assert_not_awaited()


@pytest.mark.asyncio
async def test_missing_manifest_triggers_reload():
    # latest.json absent (manifest=None) while a build is loaded: build_id
    # can't be confirmed equal, so reload to be safe.
    svc = _make_service(loaded_build_id="build-1")
    svc._load_manifest = AsyncMock(return_value=None)
    svc.reload = AsyncMock(return_value={"loaded": True})

    result = await svc.reload_if_changed()

    assert result == {"loaded": True}
    svc.reload.assert_awaited_once()
