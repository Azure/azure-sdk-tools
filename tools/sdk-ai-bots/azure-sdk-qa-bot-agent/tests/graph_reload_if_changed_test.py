"""Critical-path tests for ``KnowledgeGraphService.reload_if_changed``.

The daily manifest poll reloads only when ``latest.json``'s ``build_id``
changed. We bypass ``__init__`` with ``object.__new__`` so the test
exercises the poll decision without blob / AI Search / config deps.
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


def _make_service(loaded_build_id: str) -> KnowledgeGraphService:
    svc = object.__new__(KnowledgeGraphService)
    svc._enabled = True
    svc._loaded_version = {"manifest": {"build_id": loaded_build_id}}
    return svc


@pytest.mark.asyncio
async def test_skips_reload_when_build_id_unchanged():
    svc = _make_service("build-1")
    svc._load_manifest = AsyncMock(return_value={"build_id": "build-1"})
    svc.reload = AsyncMock()

    result = await svc.reload_if_changed()

    assert result is None
    svc.reload.assert_not_awaited()


@pytest.mark.asyncio
async def test_reloads_when_build_id_changed():
    svc = _make_service("build-1")
    svc._load_manifest = AsyncMock(return_value={"build_id": "build-2"})
    svc.reload = AsyncMock(return_value={"loaded": True})

    result = await svc.reload_if_changed()

    assert result == {"loaded": True}
    svc.reload.assert_awaited_once()
