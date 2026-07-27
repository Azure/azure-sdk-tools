"""Streaming invocation lifecycle for Azure AI Foundry hosted agents.

``HostedAgentClient`` encapsulates the low-level I/O of driving a hosted
agent through the OpenAI Responses API: creating a responses stream,
consuming it to the ``response.completed`` event, polling for late-arriving
text, retrying transient failures with bounded backoff, and best-effort
stream cleanup.
"""

from __future__ import annotations

import asyncio
import logging
from typing import Any

from openai import (
    APIConnectionError,
    APIStatusError,
    APITimeoutError,
    AsyncOpenAI,
    BadRequestError,
    NotFoundError,
)
from openai.types.responses import Response as OpenAIResponse
from openai.types.responses.response_input_item_param import ResponseInputItemParam

from utils.azure_ai_foundry import set_stateless_session_id

logger = logging.getLogger(__name__)

# -- Retry / timeout tuning ------------------------------------------------
STREAM_CREATE_MAX_RETRIES = 3
STREAM_CREATE_RETRY_DELAY_SECS = 1.5
# Max time to wait for a stream to reach ``response.completed``.
STREAM_COMPLETE_TIMEOUT_SECS = 180.0

# -- Polling for late-arriving output_text ---------------------------------
POLL_MAX_RETRIES = 5
POLL_RETRY_DELAY_SECS = 3.0

# -- Stream event types ----------------------------------------------------
STREAM_EVENT_RESPONSE_COMPLETED = "response.completed"
STREAM_EVENT_RESPONSE_FAILED = "response.failed"
STREAM_EVENT_RESPONSE_INCOMPLETE = "response.incomplete"


class EmptyAgentResponseError(Exception):
    """Raised when the agent completes with empty ``output_text`` (retryable)."""


class HostedAgentClient:
    """Drives a Foundry hosted agent through the OpenAI Responses API.

    ``invoke`` performs a bounded-retry invocation and returns
    ``(trace_id, response)`` so the caller can access the AI Foundry trace id
    and map the response into its own domain shape.
    """

    def __init__(
        self,
        openai_client: AsyncOpenAI,
        *,
        max_retries: int = STREAM_CREATE_MAX_RETRIES,
        retry_delay: float = STREAM_CREATE_RETRY_DELAY_SECS,
        stream_timeout: float = STREAM_COMPLETE_TIMEOUT_SECS,
    ) -> None:
        self._client = openai_client
        self._max_retries = max_retries
        self._retry_delay = retry_delay
        self._stream_timeout = stream_timeout

    async def invoke(
        self,
        conversation_items: list[ResponseInputItemParam],
        agent_ref: dict[str, str],
        agent_conversation_id: str | None = None,
        agent_session_id: str | None = None,
    ) -> tuple[str | None, OpenAIResponse]:
        """Invoke the agent with bounded retries and return ``(trace_id, response)``.

        Threaded calls pass ``agent_conversation_id``; stateless calls pass a
        reused ``agent_session_id``. A cached session rejected by the platform
        (404/400) is dropped so the next attempt creates a fresh one. Empty
        responses and transient errors are retried.
        """
        last_error: Exception | None = None

        for attempt in range(1, self._max_retries + 1):
            extra_body: dict[str, Any] = {"agent_reference": agent_ref}
            kwargs: dict[str, Any] = {}
            if agent_conversation_id:
                kwargs["conversation"] = agent_conversation_id
            if agent_session_id:
                extra_body["agent_session_id"] = agent_session_id
            stream = None
            try:
                stream = await self._client.responses.create(
                    input=conversation_items,
                    store=True,
                    stream=True,
                    extra_body=extra_body,
                    **kwargs,
                )
                # Bound the wait for the stream to complete.
                response = await asyncio.wait_for(
                    self._consume_stream(stream, agent_conversation_id),
                    timeout=self._stream_timeout,
                )
                # Poll if completed with empty text (Foundry persistence delay).
                if response.status == "completed" and not response.output_text:
                    response = await self._poll_response(response)
                if not response.output_text:
                    raise EmptyAgentResponseError(
                        "Agent returned empty output_text "
                        f"(id={response.id}, status={response.status})"
                    )
                trace_id = self._extract_trace_id(stream)
                await self.close_stream(stream)
                return trace_id, response
            except (NotFoundError, BadRequestError) as ex:
                last_error = ex
                await self.close_stream(stream)
                # Rejected cached session: drop it and retry without one.
                if agent_session_id:
                    set_stateless_session_id(None)
                    agent_session_id = None
                    continue
                logger.warning(
                    "Failed to create agent stream (attempt %d/%d): "
                    "conversation=%s, error=%s",
                    attempt,
                    self._max_retries,
                    agent_conversation_id,
                    ex,
                    exc_info=True,
                )
            except (APIConnectionError, APITimeoutError, APIStatusError) as ex:
                last_error = ex
                await self.close_stream(stream)
                logger.warning(
                    "Failed to create agent stream (attempt %d/%d): "
                    "conversation=%s, error=%s",
                    attempt,
                    self._max_retries,
                    agent_conversation_id,
                    ex,
                    exc_info=True,
                )
            except asyncio.TimeoutError as ex:
                last_error = ex
                await self.close_stream(stream)
                logger.warning(
                    "Agent stream did not complete within %.0fs "
                    "(attempt %d/%d): conversation=%s",
                    self._stream_timeout,
                    attempt,
                    self._max_retries,
                    agent_conversation_id,
                )
            except (EmptyAgentResponseError, RuntimeError) as ex:
                # ``RuntimeError`` = stream ended without a completed event;
                # both are transient and retryable.
                last_error = ex
                await self.close_stream(stream)
                logger.warning(
                    "Agent returned no usable response (attempt %d/%d): "
                    "conversation=%s, error=%s",
                    attempt,
                    self._max_retries,
                    agent_conversation_id,
                    ex,
                )

            if attempt >= self._max_retries:
                break
            await asyncio.sleep(self._retry_delay * attempt)

        raise RuntimeError(
            f"Failed to obtain a non-empty agent response after "
            f"{self._max_retries} attempts (conversation={agent_conversation_id})"
        ) from last_error

    async def close_stream(self, stream) -> None:
        """Best-effort close of a responses stream; errors are swallowed."""
        if stream is None:
            return
        close = getattr(stream, "close", None)
        if close is None:
            return
        try:
            result = close()
            if asyncio.iscoroutine(result):
                await result
        except Exception:
            logger.debug("Failed to close agent stream", exc_info=True)

    @staticmethod
    def _extract_trace_id(stream) -> str | None:
        """Read the AI Foundry trace id from the stream's ``x-request-id`` header.

        The header may contain duplicated values separated by comma; the first
        one is returned. Returns ``None`` when the header is absent.
        """
        response = getattr(stream, "response", None)
        if response is None:
            return None
        x_request_id = response.headers.get("x-request-id", "")
        return x_request_id.split(",")[0].strip() if x_request_id else None

    async def _consume_stream(
        self,
        stream,
        agent_conversation_id: str | None,
    ) -> OpenAIResponse:
        """Consume a responses stream until the ``response.completed`` event."""
        response: OpenAIResponse | None = None
        last_event_type: str | None = None
        async for event in stream:
            logger.debug("Stream event: type=%s, content=%s", event.type, event)
            last_event_type = event.type
            if event.type == STREAM_EVENT_RESPONSE_COMPLETED:
                response = event.response
                break
            if event.type in (
                STREAM_EVENT_RESPONSE_FAILED,
                STREAM_EVENT_RESPONSE_INCOMPLETE,
            ):
                failed = getattr(event, "response", None)
                logger.error(
                    "Agent stream %s: error=%s, incomplete_details=%s, status=%s, "
                    "conversation=%s",
                    event.type,
                    getattr(failed, "error", None),
                    getattr(failed, "incomplete_details", None),
                    getattr(failed, "status", None),
                    agent_conversation_id,
                )

        if response is None:
            raise RuntimeError(
                "Agent stream ended without a response.completed event "
                f"(last_event={last_event_type})"
            )
        return response

    async def _poll_response(
        self,
        response: OpenAIResponse,
        max_retries: int = POLL_MAX_RETRIES,
        retry_delay: float = POLL_RETRY_DELAY_SECS,
    ) -> OpenAIResponse:
        """Poll ``responses.retrieve()`` until output_text appears."""
        for attempt in range(1, max_retries + 1):
            await asyncio.sleep(retry_delay)
            try:
                refreshed = await self._client.responses.retrieve(response.id)
                if refreshed.output_text:
                    logger.info(
                        "Poll retrieved text on attempt %d/%d: response=%s, "
                        "text_len=%d",
                        attempt,
                        max_retries,
                        response.id,
                        len(refreshed.output_text),
                    )
                    return refreshed
                logger.info(
                    "Poll attempt %d/%d: still no text, response=%s",
                    attempt,
                    max_retries,
                    response.id,
                )
            except Exception:
                logger.warning(
                    "Poll attempt %d/%d failed: response=%s",
                    attempt,
                    max_retries,
                    response.id,
                    exc_info=True,
                )
        logger.warning(
            "Poll exhausted %d retries without text: response=%s",
            max_retries,
            response.id,
        )
        return response
