"""Unit tests for HostedAgentClient invocation and retry behavior.

Hermetic: the OpenAI client and responses are stubbed, so no real Azure AI
Foundry, network, or LLM access is required.
"""

from __future__ import annotations

import asyncio
import sys
from pathlib import Path
from unittest.mock import AsyncMock, patch

import httpx
import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from openai import BadRequestError, NotFoundError

from utils.azure_ai_foundry_agent import CONTENT_SAFETY_MESSAGE, HostedAgentClient


class _FakeResponse:
    """Minimal stand-in for an OpenAI ``Response`` object."""

    def __init__(
        self,
        output_text: str = "",
        status: str = "completed",
        id: str = "resp",
        request_id: str | None = None,
    ):
        self.output_text = output_text
        self.status = status
        self.id = id
        self._request_id = request_id


def _mock_client(create_side_effect):
    """Build a mock OpenAI client whose ``responses.create`` uses the side effect."""
    client = AsyncMock()
    client.responses.create = AsyncMock(side_effect=create_side_effect)
    return client


@pytest.mark.asyncio
async def test_invoke_returns_completed_response_and_trace_id() -> None:
    resp = _FakeResponse(
        output_text="hello",
        status="completed",
        id="r1",
        request_id="trace-123",
    )
    client = _mock_client([resp])

    trace_id, out = await HostedAgentClient(client, retry_delay=0).invoke(
        conversation_items=[],
        agent_ref={},
    )

    assert out is resp
    assert trace_id == "trace-123"
    assert client.responses.create.await_count == 1
    assert client.responses.create.await_args.kwargs["stream"] is False


@pytest.mark.asyncio
async def test_invoke_retries_on_empty_response_then_succeeds() -> None:
    """An empty ``output_text`` is retried and a later non-empty response wins."""
    empty = _FakeResponse(output_text="", status="completed", id="r1")
    good = _FakeResponse(output_text="answer", status="completed", id="r2")
    client = _mock_client([empty, good])

    # Keep the empty-text poll fast: return the response unchanged.
    with patch.object(
        HostedAgentClient, "_poll_response", AsyncMock(side_effect=lambda r: r)
    ):
        _, out = await HostedAgentClient(client, retry_delay=0).invoke(
            conversation_items=[],
            agent_ref={},
        )

    assert out is good
    assert client.responses.create.await_count == 2


@pytest.mark.asyncio
async def test_invoke_raises_after_empty_responses_exhaust_retries() -> None:
    """When every attempt is empty, retries exhaust and a RuntimeError is raised."""
    client = _mock_client(lambda *a, **k: _FakeResponse(output_text=""))

    with patch.object(
        HostedAgentClient, "_poll_response", AsyncMock(side_effect=lambda r: r)
    ):
        with pytest.raises(RuntimeError):
            await HostedAgentClient(client, max_retries=2, retry_delay=0).invoke(
                conversation_items=[],
                agent_ref={},
            )

    assert client.responses.create.await_count == 2


@pytest.mark.asyncio
async def test_invoke_retries_on_request_timeout() -> None:
    good = _FakeResponse(output_text="answer", status="completed", id="r2")
    attempts = 0

    async def _create(*_args, **_kwargs):
        nonlocal attempts
        attempts += 1
        if attempts == 1:
            await asyncio.sleep(1)
        return good

    client = _mock_client(_create)

    _, out = await HostedAgentClient(
        client, retry_delay=0, request_timeout=0.01
    ).invoke(
        conversation_items=[],
        agent_ref={},
    )

    assert out is good
    assert client.responses.create.await_count == 2


def _api_error(error_cls, status_code: int):
    """Build a real OpenAI ``APIStatusError`` subclass instance for tests."""
    request = httpx.Request("POST", "https://example.test/v1/responses")
    response = httpx.Response(status_code, request=request)
    return error_cls("rejected", response=response, body=None)


@pytest.mark.parametrize(
    "error_cls, status_code",
    [(NotFoundError, 404), (BadRequestError, 400)],
)
@pytest.mark.asyncio
async def test_invoke_drops_rejected_session_and_retries_without_it(
    error_cls, status_code
) -> None:
    """A cached session rejected with 404/400 is dropped; the retry omits it."""
    good = _FakeResponse(output_text="answer", status="completed", id="r2")
    captured_extra_bodies: list[dict] = []

    def _create(*_a, **kwargs):
        captured_extra_bodies.append(kwargs.get("extra_body", {}))
        if len(captured_extra_bodies) == 1:
            raise _api_error(error_cls, status_code)
        return good

    client = _mock_client(_create)

    with patch(
        "utils.azure_ai_foundry_agent.set_stateless_session_id"
    ) as mock_set:
        _, out = await HostedAgentClient(client, retry_delay=0).invoke(
            conversation_items=[],
            agent_ref={},
            agent_session_id="stale-session",
        )

    assert out is good
    assert client.responses.create.await_count == 2
    # The rejected session is cleared so a fresh one is created next time.
    mock_set.assert_called_once_with(None)
    # First attempt carried the stale session; the retry dropped it.
    assert captured_extra_bodies[0].get("agent_session_id") == "stale-session"
    assert "agent_session_id" not in captured_extra_bodies[1]


def _content_filter_error() -> BadRequestError:
    """Build a ``BadRequestError`` shaped like a content-safety block."""
    request = httpx.Request("POST", "https://example.test/v1/responses")
    response = httpx.Response(400, request=request)
    body = {
        "error": {
            "code": "content_filter",
            "message": "blocked at input stage",
            "type": "content_safety_error",
        }
    }
    return BadRequestError("blocked", response=response, body=body)


@pytest.mark.asyncio
async def test_invoke_returns_content_safety_response_without_retry() -> None:
    """A content-filter block returns a safe message and is not retried."""
    client = _mock_client(lambda *_a, **_k: (_ for _ in ()).throw(
        _content_filter_error()
    ))

    trace_id, out = await HostedAgentClient(client, retry_delay=0).invoke(
        conversation_items=[],
        agent_ref={},
    )

    assert trace_id is None
    assert out.output_text == CONTENT_SAFETY_MESSAGE
    # No retry: the deterministic content-safety block fails fast.
    assert client.responses.create.await_count == 1
