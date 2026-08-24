"""Invocation lifecycle for Azure AI Foundry hosted agents.

``HostedAgentClient`` encapsulates the low-level I/O of driving a hosted
agent through the OpenAI Responses API: requesting a completed response,
polling for late-arriving text, and retrying transient failures with bounded
backoff.
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
from openai.types.responses import ResponseOutputMessage, ResponseOutputText
from openai.types.responses.response_input_item_param import ResponseInputItemParam

from utils.azure_ai_foundry import set_stateless_session_id

logger = logging.getLogger(__name__)

# -- Retry / timeout tuning ------------------------------------------------
AGENT_REQUEST_MAX_RETRIES = 3
AGENT_REQUEST_RETRY_DELAY_SECS = 1.5
AGENT_REQUEST_TIMEOUT_SECS = 180.0

# -- Polling for late-arriving output_text ---------------------------------
POLL_MAX_RETRIES = 5
POLL_RETRY_DELAY_SECS = 3.0

# -- Content safety --------------------------------------------------------
CONTENT_SAFETY_MESSAGE = (
    "I can't help with this request because it was flagged by "
    "our content safety policy. Please rephrase your message and try again."
)


class EmptyAgentResponseError(Exception):
    """Raised when the agent completes with empty ``output_text`` (retryable)."""


def _is_content_filter_error(ex: BadRequestError) -> bool:
    """Return True when a ``BadRequestError`` was caused by a content-safety block."""
    if getattr(ex, "code", None) == "content_filter":
        return True
    body = getattr(ex, "body", None)
    if isinstance(body, dict):
        error = body.get("error") or {}
        if isinstance(error, dict):
            return (
                error.get("code") == "content_filter"
                or error.get("type") == "content_safety_error"
            )
    return False


def _build_content_safety_response() -> OpenAIResponse:
    """Build a synthetic completed response carrying the content-safety message."""
    text = ResponseOutputText.model_construct(
        type="output_text",
        text=CONTENT_SAFETY_MESSAGE,
        annotations=[],
    )
    message = ResponseOutputMessage.model_construct(
        id="content-filter",
        type="message",
        role="assistant",
        status="completed",
        content=[text],
    )
    return OpenAIResponse.model_construct(
        id="content-filter",
        status="completed",
        output=[message],
        error=None,
        incomplete_details=None,
        usage=None,
    )


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
        max_retries: int = AGENT_REQUEST_MAX_RETRIES,
        retry_delay: float = AGENT_REQUEST_RETRY_DELAY_SECS,
        request_timeout: float = AGENT_REQUEST_TIMEOUT_SECS,
    ) -> None:
        self._client = openai_client
        self._max_retries = max_retries
        self._retry_delay = retry_delay
        self._request_timeout = request_timeout

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
            try:
                response = await asyncio.wait_for(
                    self._client.responses.create(
                        input=conversation_items,
                        store=True,
                        stream=False,
                        extra_body=extra_body,
                        **kwargs,
                    ),
                    timeout=self._request_timeout,
                )
                # Poll if completed with empty text (Foundry persistence delay).
                if response.status == "completed" and not response.output_text:
                    response = await self._poll_response(response)
                if not response.output_text:
                    raise EmptyAgentResponseError(
                        "Agent returned empty output_text "
                        f"(id={response.id}, status={response.status})"
                    )
                return getattr(response, "_request_id", None), response
            except (NotFoundError, BadRequestError) as ex:
                last_error = ex
                # Content-safety blocks are deterministic; retrying will not
                # help, so return a synthetic response with a safe message.
                if isinstance(ex, BadRequestError) and _is_content_filter_error(ex):
                    logger.error(
                        "Agent request blocked by content safety policy: "
                        "conversation=%s, error=%s",
                        agent_conversation_id,
                        ex,
                        exc_info=True,
                    )
                    return None, _build_content_safety_response()
                # Rejected cached session: drop it and retry without one.
                if agent_session_id:
                    set_stateless_session_id(None)
                    agent_session_id = None
                    continue
                logger.warning(
                    "Agent request rejected (attempt %d/%d): "
                    "conversation=%s, error=%s",
                    attempt,
                    self._max_retries,
                    agent_conversation_id,
                    ex,
                    exc_info=True,
                )
            except (APIConnectionError, APITimeoutError, APIStatusError) as ex:
                last_error = ex
                logger.warning(
                    "Agent request failed (attempt %d/%d): "
                    "conversation=%s, error=%s",
                    attempt,
                    self._max_retries,
                    agent_conversation_id,
                    ex,
                    exc_info=True,
                )
            except asyncio.TimeoutError as ex:
                last_error = ex
                logger.warning(
                    "Agent request did not complete within %.0fs "
                    "(attempt %d/%d): conversation=%s",
                    self._request_timeout,
                    attempt,
                    self._max_retries,
                    agent_conversation_id,
                )
            except EmptyAgentResponseError as ex:
                last_error = ex
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
            except (APIConnectionError, APITimeoutError, APIStatusError):
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
