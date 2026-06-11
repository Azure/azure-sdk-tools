"""Unit tests for the GraphRAG endpoints (all under ``/graph``).

These tests avoid touching real blob storage or running Local Search by
monkeypatching ``KnowledgeGraphService`` on the singleton instance with
``unittest.mock``. Authentication is delegated to App Service EasyAuth
at the ingress, which the ``TestClient`` bypasses — so these tests
cover only application-level behaviour.

Covered contract:

``POST /graph/admin/reload``
- 409 when the underlying service is not enabled
- 500 when ``reload`` raises
- 200 + status dict on success

``GET /graph/admin/status``
- 200 + status dict

``POST /graph/query``
- 200 + empty references on blank query, disabled service, ``search_graph``
  exception, or ``None`` result (never 5xx — chat agent must degrade
  gracefully)
- 200 + happy-path response with dedup-by-title and snippet truncation
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

import server  # noqa: E402
from utils import knowledge_graph as kg  # noqa: E402


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
def client():
    with TestClient(server.app) as c:
        yield c


@pytest.fixture
def fake_service():
    """Patch the module-level ``get_knowledge_graph_service`` to return a fake."""
    service = AsyncMock()
    service.enabled = True
    service.get_status = lambda: _STATUS_PAYLOAD
    service.reload = AsyncMock(return_value=_STATUS_PAYLOAD)
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        yield service


# ---------------------------------------------------------------------------
# /graph/admin/reload
# ---------------------------------------------------------------------------


def test_reload_returns_409_when_disabled(client):
    service = AsyncMock()
    service.enabled = False
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post("/graph/admin/reload")
    assert resp.status_code == 409


def test_reload_success(client, fake_service):
    resp = client.post("/graph/admin/reload")
    assert resp.status_code == 200
    assert resp.json() == _STATUS_PAYLOAD
    fake_service.reload.assert_awaited_once()


def test_reload_propagates_failure_as_500(client):
    service = AsyncMock()
    service.enabled = True
    service.reload = AsyncMock(side_effect=RuntimeError("blob unavailable"))
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post("/graph/admin/reload")
    assert resp.status_code == 500
    assert "blob unavailable" in resp.json()["detail"]


# ---------------------------------------------------------------------------
# /graph/admin/status
# ---------------------------------------------------------------------------


def test_status_returns_payload(client, fake_service):
    resp = client.get("/graph/admin/status")
    assert resp.status_code == 200
    assert resp.json() == _STATUS_PAYLOAD


# ---------------------------------------------------------------------------
# /graph/query
# ---------------------------------------------------------------------------


def _make_source_ref(title: str, link: str, content: str, source: str):
    """Build a ``GraphSourceRef`` without importing the heavy graphrag stack."""
    from utils.knowledge_graph import GraphSourceRef

    return GraphSourceRef(title=title, link=link, content=content, source=source)


def test_graph_query_blank_query_returns_empty(client):
    """Whitespace-only query short-circuits to an empty result (no service call)."""
    service = AsyncMock()
    service.enabled = True
    service.search_graph = AsyncMock()
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post("/graph/query", json={"query": "   "})
    assert resp.status_code == 200
    assert resp.json() == {"references": [], "query": ""}
    service.search_graph.assert_not_called()


def test_graph_query_disabled_service_returns_empty(client):
    service = AsyncMock()
    service.enabled = False
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post("/graph/query", json={"query": "hello"})
    assert resp.status_code == 200
    assert resp.json() == {"references": [], "query": "hello"}


def test_graph_query_service_exception_returns_empty(client):
    service = AsyncMock()
    service.enabled = True
    service.search_graph = AsyncMock(side_effect=RuntimeError("graph oom"))
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post("/graph/query", json={"query": "hello"})
    # Never 5xx for query-side failures — chat agent must degrade gracefully.
    assert resp.status_code == 200
    assert resp.json() == {"references": [], "query": "hello"}


def test_graph_query_happy_path_with_dedup_and_truncation(client):
    long_body = "x" * 1500  # > _GRAPH_SNIPPET_MAX_CHARS (1200)
    refs = [
        _make_source_ref(
            title="doc1",
            link="https://example.com/doc1",
            content="short content",
            source="typespec_docs",
        ),
        # Same title → must be deduped (first occurrence wins).
        _make_source_ref(
            title="doc1",
            link="https://example.com/doc1-alt",
            content="other content",
            source="azure_sdk_docs",
        ),
        _make_source_ref(
            title="doc2",
            link="https://example.com/doc2",
            content=long_body,
            source="azure_sdk_docs",
        ),
    ]
    service = AsyncMock()
    service.enabled = True
    service.search_graph = AsyncMock(return_value=refs)
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post("/graph/query", json={"query": "what is X?"})

    assert resp.status_code == 200
    body = resp.json()
    assert body["query"] == "what is X?"
    titles = [r["title"] for r in body["references"]]
    assert titles == ["doc1", "doc2"], "dedup by title should keep first occurrence"

    doc1 = body["references"][0]
    assert doc1["source"] == "typespec_docs"
    assert doc1["snippet"] == "short content"

    doc2 = body["references"][1]
    assert doc2["snippet"].endswith("\n... [truncated]")
    # 1200 chars of body + truncation marker
    assert doc2["snippet"].startswith("x" * 1200)


def test_graph_query_search_returns_none(client):
    """``service.search_graph`` may return None when the engine is half-loaded."""
    service = AsyncMock()
    service.enabled = True
    service.search_graph = AsyncMock(return_value=None)
    with patch.object(kg, "get_knowledge_graph_service", return_value=service):
        resp = client.post("/graph/query", json={"query": "hello"})
    assert resp.status_code == 200
    assert resp.json() == {"references": [], "query": "hello"}

