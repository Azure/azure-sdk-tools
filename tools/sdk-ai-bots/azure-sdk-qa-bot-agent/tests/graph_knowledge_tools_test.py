"""Unit tests for the HTTP-based ``GraphKnowledgeTools``.

These tests exercise the chat-agent side of route-A (HTTP delegation to
the backend server) by mocking ``httpx.AsyncClient`` — they do **not**
need a running backend, a warm GraphRAG service, or any blob/AI Search
access.
"""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import httpx
import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import config.app_config as app_config  # noqa: E402
from models.knowledge import GraphSearchResult  # noqa: E402
from tools.graph_knowledge_tools import GraphKnowledgeTools  # noqa: E402


_ENDPOINT = "https://backend.example.com/graph/query"
_AUDIENCE = "830f1656-8b36-4e8e-9781-87ccdd038644"
_BEARER = "stub-bearer-token"


@pytest.fixture
def configured(monkeypatch):
    """Hydrate App Config with the values the tool needs to make an HTTP call."""
    monkeypatch.setattr(
        app_config,
        "_settings",
        {
            "GRAPH_QUERY_URL": _ENDPOINT,
            "GRAPH_QUERY_AUDIENCE": _AUDIENCE,
        },
    )
    # Stub the Managed Identity → AAD token call so tests don't reach
    # azure-identity (which would fail outside Azure).
    fake_token = MagicMock()
    fake_token.token = _BEARER
    fake_cred = MagicMock()
    fake_cred.get_token = AsyncMock(return_value=fake_token)
    monkeypatch.setattr(
        "tools.graph_knowledge_tools.get_credential",
        lambda: fake_cred,
    )


def _make_async_client_mock(json_payload: dict | None = None, *, raises: Exception | None = None) -> MagicMock:
    """Build an httpx.AsyncClient mock that yields *json_payload* once when posted to."""
    response = MagicMock(spec=httpx.Response)
    response.raise_for_status = MagicMock()
    response.json = MagicMock(return_value=json_payload or {})
    if raises is not None:
        response.raise_for_status.side_effect = raises

    client = MagicMock(spec=httpx.AsyncClient)
    client.post = AsyncMock(return_value=response)
    client.__aenter__ = AsyncMock(return_value=client)
    client.__aexit__ = AsyncMock(return_value=False)
    return client


@pytest.mark.asyncio
async def test_blank_query_short_circuits(configured) -> None:
    tools = GraphKnowledgeTools()
    result = await tools.search_knowledge_graph(query="   ")
    assert result == GraphSearchResult(references=[], query="")


@pytest.mark.asyncio
async def test_missing_config_returns_empty(monkeypatch) -> None:
    monkeypatch.setattr(app_config, "_settings", {})
    tools = GraphKnowledgeTools()
    with patch("tools.graph_knowledge_tools.httpx.AsyncClient") as client_cls:
        result = await tools.search_knowledge_graph(query="hello")
    assert result.references == []
    assert result.query == "hello"
    client_cls.assert_not_called(), "must not attempt HTTP when config is missing"


@pytest.mark.asyncio
async def test_http_success_returns_parsed_payload(configured) -> None:
    payload = {
        "query": "trim me",
        "references": [
            {
                "title": "doc1",
                "link": "https://example.com/doc1",
                "content": "hello world",
                "source": "typespec_docs",
            },
            {
                "title": "doc2",
                "link": "https://example.com/doc2",
                "content": "x" * 50,
                "source": "azure_sdk_docs",
            },
        ],
    }
    client = _make_async_client_mock(payload)
    with patch("tools.graph_knowledge_tools.httpx.AsyncClient", return_value=client):
        tools = GraphKnowledgeTools()
        result = await tools.search_knowledge_graph(query=" trim me ")

    # Endpoint + auth header (EasyAuth bearer only — admin token was
    # dropped now that EasyAuth gates everything) + body shape.
    client.post.assert_awaited_once_with(
        _ENDPOINT,
        headers={"Authorization": f"Bearer {_BEARER}"},
        json={"query": "trim me"},
    )
    assert isinstance(result, GraphSearchResult)
    assert result.query == "trim me"
    assert [r.title for r in result.references] == ["doc1", "doc2"]
    assert result.references[0].source == "typespec_docs"


@pytest.mark.asyncio
async def test_http_status_error_returns_empty_result(configured) -> None:
    client = _make_async_client_mock(
        raises=httpx.HTTPStatusError(
            "boom",
            request=httpx.Request("POST", _ENDPOINT),
            response=httpx.Response(500),
        )
    )
    with patch("tools.graph_knowledge_tools.httpx.AsyncClient", return_value=client):
        tools = GraphKnowledgeTools()
        result = await tools.search_knowledge_graph(query="hi")

    assert result == GraphSearchResult(references=[], query="hi")


@pytest.mark.asyncio
async def test_http_timeout_returns_empty_result(configured) -> None:
    client = MagicMock(spec=httpx.AsyncClient)
    client.post = AsyncMock(side_effect=httpx.ConnectTimeout("backend slow"))
    client.__aenter__ = AsyncMock(return_value=client)
    client.__aexit__ = AsyncMock(return_value=False)
    with patch("tools.graph_knowledge_tools.httpx.AsyncClient", return_value=client):
        tools = GraphKnowledgeTools()
        result = await tools.search_knowledge_graph(query="hi")

    assert result == GraphSearchResult(references=[], query="hi")


@pytest.mark.asyncio
async def test_malformed_payload_returns_empty_result(configured) -> None:
    """A backend that returns non-conforming JSON should not crash the tool."""
    client = _make_async_client_mock({"unexpected_key": True, "references": "not-a-list"})
    with patch("tools.graph_knowledge_tools.httpx.AsyncClient", return_value=client):
        tools = GraphKnowledgeTools()
        result = await tools.search_knowledge_graph(query="hi")

    assert result == GraphSearchResult(references=[], query="hi")


@pytest.mark.asyncio
async def test_echoed_query_filled_when_backend_omits_it(configured) -> None:
    """The backend's ``query`` field is informational; if absent we backfill it."""
    payload = {"references": []}  # no 'query' key
    client = _make_async_client_mock(payload)
    with patch("tools.graph_knowledge_tools.httpx.AsyncClient", return_value=client):
        tools = GraphKnowledgeTools()
        result = await tools.search_knowledge_graph(query="my-question")

    assert result.query == "my-question"


@pytest.mark.asyncio
async def test_custom_timeout_honoured(configured, monkeypatch) -> None:
    monkeypatch.setattr(
        app_config,
        "_settings",
        {
            "GRAPH_QUERY_URL": _ENDPOINT,
            "GRAPH_QUERY_AUDIENCE": _AUDIENCE,
            "GRAPH_QUERY_TIMEOUT_SECONDS": "5.0",
        },
    )
    client = _make_async_client_mock({"query": "x", "references": []})
    with patch(
        "tools.graph_knowledge_tools.httpx.AsyncClient", return_value=client
    ) as client_cls:
        tools = GraphKnowledgeTools()
        await tools.search_knowledge_graph(query="x")

    _args, kwargs = client_cls.call_args
    assert kwargs.get("timeout") == 5.0


@pytest.mark.asyncio
async def test_token_acquisition_failure_returns_empty(monkeypatch) -> None:
    """A failure to acquire the AAD bearer token must degrade gracefully."""
    monkeypatch.setattr(
        app_config,
        "_settings",
        {
            "GRAPH_QUERY_URL": _ENDPOINT,
            "GRAPH_QUERY_AUDIENCE": _AUDIENCE,
        },
    )
    failing_cred = MagicMock()
    failing_cred.get_token = AsyncMock(side_effect=RuntimeError("MI not available"))
    monkeypatch.setattr(
        "tools.graph_knowledge_tools.get_credential",
        lambda: failing_cred,
    )
    with patch("tools.graph_knowledge_tools.httpx.AsyncClient") as client_cls:
        tools = GraphKnowledgeTools()
        result = await tools.search_knowledge_graph(query="hi")

    assert result == GraphSearchResult(references=[], query="hi")
    client_cls.assert_not_called(), "must not attempt HTTP when AAD token fails"

