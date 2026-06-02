"""Unit tests for the GraphRAG admin endpoints (reload / status).

These tests avoid touching real blob storage or running DRIFT by
monkeypatching ``KnowledgeGraphService`` on the singleton instance with
``unittest.mock``. The goal is to lock down the HTTP contract:
- 503 when ``GRAPHRAG_ADMIN_TOKEN`` is not configured
- 401 when the supplied token is missing or wrong
- 409 when the underlying service is not enabled
- 200 + status dict on success
- 500 when ``reload`` raises
"""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from fastapi.testclient import TestClient  # noqa: E402

import config.app_config as app_config  # noqa: E402
import server  # noqa: E402
from utils import knowledge_graph as kg  # noqa: E402


_TOKEN = "test-admin-token-12345"
_STATUS_PAYLOAD = {
    "enabled": True,
    "loaded": True,
    "source": {"source": "blob", "container": "graphrag-output", "prefix": "snap-1"},
    "community_level": 2,
    "version": {"manifest": {"build_id": "snap-1"}},
    "row_counts": {
        "entities": 10,
        "communities": 3,
        "community_reports": 3,
        "text_units": 25,
        "relationships": 15,
        "documents": 4,
    },
}


@pytest.fixture
def client(monkeypatch):
    """Yield a FastAPI test client with the admin token loaded into App Config."""
    # ``config.app_config.get`` reads from the module-level ``_settings``
    # dict that's populated by ``init()`` against Azure App Configuration.
    # We bypass init() here by injecting the dict directly so the admin
    # token check finds it via ``app_config.get('GRAPHRAG_ADMIN_TOKEN', '')``.
    monkeypatch.setattr(app_config, "_settings", {"GRAPHRAG_ADMIN_TOKEN": _TOKEN})
    with TestClient(server.app) as c:
        yield c


@pytest.fixture
def fake_service():
    """Patch the module-level ``get_knowledge_graph_service`` to return a fake."""
    service = AsyncMock()
    service.enabled = True
    service.get_status = lambda: _STATUS_PAYLOAD
    service.reload = AsyncMock(return_value=_STATUS_PAYLOAD)
    # The endpoint imports the helper lazily; patch the source module so
    # both server.py's import and any other consumer see the same fake.
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        yield service


def test_reload_requires_configured_token(monkeypatch):
    """Without GRAPHRAG_ADMIN_TOKEN the endpoint is hard-disabled (503)."""
    monkeypatch.setattr(app_config, "_settings", {})
    with TestClient(server.app) as client:
        resp = client.post("/admin/graphrag/reload", headers={"X-Admin-Token": "x"})
    assert resp.status_code == 503


def test_reload_rejects_wrong_token(client):
    resp = client.post("/admin/graphrag/reload", headers={"X-Admin-Token": "wrong"})
    assert resp.status_code == 401


def test_reload_rejects_missing_header(client):
    resp = client.post("/admin/graphrag/reload")
    assert resp.status_code == 401


def test_reload_returns_409_when_disabled(client):
    service = AsyncMock()
    service.enabled = False
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post(
            "/admin/graphrag/reload", headers={"X-Admin-Token": _TOKEN}
        )
    assert resp.status_code == 409


def test_reload_success(client, fake_service):
    resp = client.post(
        "/admin/graphrag/reload", headers={"X-Admin-Token": _TOKEN}
    )
    assert resp.status_code == 200
    assert resp.json() == _STATUS_PAYLOAD
    fake_service.reload.assert_awaited_once()


def test_reload_propagates_failure_as_500(client):
    service = AsyncMock()
    service.enabled = True
    service.reload = AsyncMock(side_effect=RuntimeError("blob unavailable"))
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post(
            "/admin/graphrag/reload", headers={"X-Admin-Token": _TOKEN}
        )
    assert resp.status_code == 500
    assert "blob unavailable" in resp.json()["detail"]


def test_status_requires_token(client):
    resp = client.get("/admin/graphrag/status", headers={"X-Admin-Token": "wrong"})
    assert resp.status_code == 401


def test_status_returns_payload(client, fake_service):
    resp = client.get("/admin/graphrag/status", headers={"X-Admin-Token": _TOKEN})
    assert resp.status_code == 200
    assert resp.json() == _STATUS_PAYLOAD
