"""Unit tests for HostedAgentClient invocation, retry, and stream handling.

Hermetic: the OpenAI client and its response streams are stubbed, so no real
Azure AI Foundry, network, or LLM access is required.
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

from utils.azure_ai_foundry_agent import HostedAgentClient


class _FakeResponse:
    """Minimal stand-in for an OpenAI ``Response`` object."""

    def __init__(
        self, output_text: str = "", status: str = "completed", id: str = "resp"
    ):
        self.output_text = output_text
        self.status = status
        self.id = id


class _FakeEvent:
    """Minimal stand-in for a streaming response event."""

    def __init__(self, type: str, response: _FakeResponse | None = None):
        self.type = type
        self.response = response


async def _make_stream(events, item_delay: float = 0.0):
    """Return an async generator yielding the given events."""
    for event in events:
        if item_delay:
            await asyncio.sleep(item_delay)
        yield event


def _completed_stream(response: _FakeResponse):
    """A stream that emits a single ``response.completed`` event."""
    return _make_stream([_FakeEvent("response.completed", response)])


class _StreamWithHeaders:
    """Async-iterable stream stub exposing ``.response.headers`` and ``.close``."""

    def __init__(self, response: _FakeResponse, x_request_id: str = ""):
        self._events = [_FakeEvent("response.completed", response)]
        self.response = type(
            "_Resp", (), {"headers": {"x-request-id": x_request_id}}
        )()
        self.closed = False

    def __aiter__(self):
        return self._iter()

    async def _iter(self):
        for event in self._events:
            yield event

    async def close(self) -> None:
        self.closed = True


def _mock_client(create_side_effect):
    """Build a mock OpenAI client whose ``responses.create`` uses the side effect."""
    client = AsyncMock()
    client.responses.create = AsyncMock(side_effect=create_side_effect)
    return client


@pytest.mark.asyncio
async def test_invoke_returns_trace_id_and_closes_stream_on_success() -> None:
    """A completed response yields its trace id and the stream is closed."""
    resp = _FakeResponse(output_text="hello", status="completed", id="r1")
    stream = _StreamWithHeaders(resp, x_request_id="trace-123, trace-123")
    client = _mock_client([stream])

    trace_id, out = await HostedAgentClient(client, retry_delay=0).invoke(
        conversation_items=[],
        agent_ref={},
    )

    assert out is resp
    assert trace_id == "trace-123"
    assert stream.closed is True
    assert client.responses.create.await_count == 1


@pytest.mark.asyncio
async def test_invoke_retries_on_empty_response_then_succeeds() -> None:
    """An empty ``output_text`` is retried and a later non-empty response wins."""
    empty = _FakeResponse(output_text="", status="completed", id="r1")
    good = _FakeResponse(output_text="answer", status="completed", id="r2")
    client = _mock_client([_completed_stream(empty), _completed_stream(good)])

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
    client = _mock_client(
        lambda *a, **k: _completed_stream(_FakeResponse(output_text=""))
    )

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
async def test_invoke_retries_when_stream_ends_without_completion() -> None:
    """A stream ending without ``response.completed`` is retryable, not fatal."""

    def _incomplete_stream(*_a, **_k):
        return _make_stream(
            [_FakeEvent("response.created"), _FakeEvent("response.in_progress")]
        )

    good = _FakeResponse(output_text="answer", status="completed", id="r2")
    client = _mock_client([_incomplete_stream(), _completed_stream(good)])

    _, out = await HostedAgentClient(client, retry_delay=0).invoke(
        conversation_items=[],
        agent_ref={},
    )

    assert out is good
    assert client.responses.create.await_count == 2


@pytest.mark.asyncio
async def test_invoke_retries_on_stream_completion_timeout() -> None:
    """A stream that stalls past ``stream_timeout`` is abandoned and retried."""
    good = _FakeResponse(output_text="answer", status="completed", id="r2")

    async def _stalled_stream():
        await asyncio.sleep(5)  # far longer than the tiny timeout below
        yield _FakeEvent("response.completed", good)

    client = _mock_client([_stalled_stream(), _completed_stream(good)])

    _, out = await HostedAgentClient(
        client, retry_delay=0, stream_timeout=0.05
    ).invoke(
        conversation_items=[],
        agent_ref={},
    )

    assert out is good
    assert client.responses.create.await_count == 2


@pytest.mark.asyncio
async def test_consume_stream_returns_completed_response() -> None:
    """``_consume_stream`` returns the response carried by ``response.completed``."""
    resp = _FakeResponse(output_text="x")
    out = await HostedAgentClient(AsyncMock())._consume_stream(
        _completed_stream(resp), "conv"
    )
    assert out is resp


@pytest.mark.asyncio
async def test_consume_stream_raises_without_completed_event() -> None:
    """A stream that never completes raises a RuntimeError."""
    stream = _make_stream(
        [_FakeEvent("response.created"), _FakeEvent("response.in_progress")]
    )
    with pytest.raises(RuntimeError):
        await HostedAgentClient(AsyncMock())._consume_stream(stream, "conv")


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
        return _completed_stream(good)

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

